using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DFIComplianceApp.Helpers;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using Microsoft.ML;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DFIComplianceApp.ViewModels;

public partial class CompanyRiskPredictionViewModel : ObservableObject
{
    [ObservableProperty] private string riskLevel;
    [ObservableProperty] private string riskColor;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private Company selectedCompany;

    [ObservableProperty] private ObservableCollection<Company> companies = new();
    [ObservableProperty] private ObservableCollection<Company> filteredCompanies = new();
    [ObservableProperty] private ObservableCollection<RiskPredictionHistory> predictionHistory = new();
    [ObservableProperty] private ObservableCollection<RiskPredictionHistory> filteredPredictionHistory = new();

    [ObservableProperty] private string companySearchText;
    [ObservableProperty] private string historySearchText;

    [ObservableProperty]
    private ObservableCollection<string> sortOptions = new()
    {
        "Date (Newest)",
        "Date (Oldest)",
        "Risk Level"
    };

    [ObservableProperty] private string selectedSortOption = "Date (Newest)";
    [ObservableProperty] private DateTime fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime toDate = DateTime.Now;

    public CompanyRiskPredictionViewModel()
    {
        LoadCompaniesAsync();
        LoadPredictionHistoryAsync();
    }

    private async void LoadCompaniesAsync()
    {
        try
        {
            var allCompanies = await App.Firebase.GetCompaniesAsync();
            Companies = new ObservableCollection<Company>(allCompanies.OrderBy(c => c.Name));
            FilterCompanies();
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load companies from Firebase.\n{ex.Message}", "OK");
        }
    }

    private async void LoadPredictionHistoryAsync()
    {
        try
        {
            var history = await App.Firebase.GetAllRiskPredictionHistoryAsync();
            PredictionHistory = new ObservableCollection<RiskPredictionHistory>(history.OrderByDescending(h => DateTime.Parse(h.DatePredicted)));
            ApplyHistoryFilterAndSort();
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load prediction history from Firebase.\n{ex.Message}", "OK");
        }
    }

    partial void OnCompanySearchTextChanged(string value) => FilterCompanies();
    partial void OnHistorySearchTextChanged(string value) => ApplyHistoryFilterAndSort();
    partial void OnSelectedSortOptionChanged(string value) => ApplyHistoryFilterAndSort();
    partial void OnFromDateChanged(DateTime value) => ApplyHistoryFilterAndSort();
    partial void OnToDateChanged(DateTime value) => ApplyHistoryFilterAndSort();

    private void FilterCompanies()
    {
        if (string.IsNullOrWhiteSpace(CompanySearchText))
        {
            FilteredCompanies = new ObservableCollection<Company>(Companies);
        }
        else
        {
            var lower = CompanySearchText.ToLower();
            var filtered = Companies.Where(c =>
                (!string.IsNullOrEmpty(c.Name) && c.Name.ToLower().Contains(lower)) ||
                (!string.IsNullOrEmpty(c.Location) && c.Location.ToLower().Contains(lower))
            );
            FilteredCompanies = new ObservableCollection<Company>(filtered);
        }
    }

    private void ApplyHistoryFilterAndSort()
    {
        var filtered = PredictionHistory.AsEnumerable();

        // Filter by text
        if (!string.IsNullOrWhiteSpace(HistorySearchText))
        {
            var search = HistorySearchText.ToLower();
            filtered = filtered.Where(p =>
                (!string.IsNullOrWhiteSpace(p.CompanyName) && p.CompanyName.ToLower().Contains(search)) ||
                (!string.IsNullOrWhiteSpace(p.Location) && p.Location.ToLower().Contains(search))
            );
        }

        // Filter by date range
        filtered = filtered.Where(p =>
        {
            if (DateTime.TryParse(p.DatePredicted, out var date))
            {
                return date >= FromDate.Date && date <= ToDate.Date.AddDays(1).AddTicks(-1);
            }
            return false;
        });

        // Sort
        filtered = SelectedSortOption switch
        {
            "Date (Oldest)" => filtered.OrderBy(p => DateTime.Parse(p.DatePredicted)),
            "Risk Level" => filtered.OrderByDescending(p => p.RiskLevel),
            _ => filtered.OrderByDescending(p => DateTime.Parse(p.DatePredicted)),
        };

        FilteredPredictionHistory = new ObservableCollection<RiskPredictionHistory>(filtered);
    }

    [RelayCommand]
    private async Task PredictRiskAsync()
    {
        if (SelectedCompany == null)
        {
            RiskLevel = "Please select a company.";
            RiskColor = "Gray";
            return;
        }

        IsBusy = true;

        try
        {
            // Fetch data from Firebase
            var inspections = await App.Firebase.GetInspectionsByCompanyIdAsync(SelectedCompany.Id);
            var renewals = await App.Firebase.GetCompanyRenewalsByCompanyIdAsync(SelectedCompany.Id);

            // Calculate input features
            int violationCount = inspections.Sum(i => i.ViolationCount);
            int daysSinceLastInspection = inspections.Any()
                ? (DateTime.Now - inspections.Max(i => i.PlannedDate)).Days
                : 999;

            string renewalStatus = renewals.Any(r => r.RenewalYear == DateTime.Now.Year)
                ? "Renewed"
                : "NotRenewed";

            string companyType = MapNatureOfWorkToCompanyType(SelectedCompany.NatureOfWork);

            // Load ML model properly from MauiAsset
            var context = new MLContext();

            string localModelPath = Path.Combine(FileSystem.AppDataDirectory, "company_risk_model.zip");

            if (!File.Exists(localModelPath))
            {
                using var modelStream = await FileSystem.OpenAppPackageFileAsync("company_risk_model.zip");
                using var localFileStream = File.OpenWrite(localModelPath);
                await modelStream.CopyToAsync(localFileStream);
            }

            var model = context.Model.Load(localModelPath, out _);
            var predictionEngine = context.Model.CreatePredictionEngine<CompanyRiskInput, CompanyRiskPredictionOutput>(model);

            // Predict
            var input = new CompanyRiskInput
            {
                ViolationCount = violationCount,
                DaysSinceLastInspection = daysSinceLastInspection,
                RenewalStatus = renewalStatus,
                CompanyType = companyType
            };

            var prediction = predictionEngine.Predict(input);
            var level = prediction.RiskLevel;

            // Update UI and company
            RiskLevel = $"Predicted Risk Level: {level}";
            RiskColor = GetRiskColor(level);

            SelectedCompany.RiskLevel = level;
            await App.Firebase.UpdateCompanyAsync(SelectedCompany);

            // Create and save prediction record
            var predictionRecord = new RiskPredictionHistory
            {
                CompanyId = SelectedCompany.Id,
                CompanyName = SelectedCompany.Name,
                Location = SelectedCompany.Location,
                RiskLevel = level,
                ViolationCount = violationCount,
                DaysSinceLastInspection = daysSinceLastInspection,
                RenewalStatus = renewalStatus,
                CompanyType = companyType,
                DatePredicted = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            // Insert into Firebase
            await App.Firebase.InsertRiskPredictionHistoryAsync(predictionRecord);

            // Refresh the list visually
            PredictionHistory.Insert(0, predictionRecord); // Insert at top
            ApplyHistoryFilterAndSort();

            // Toast feedback
            await ShowToastAsync("Prediction added successfully!");
        }
        catch (Exception ex)
        {
            RiskLevel = $"Prediction Error: {ex.Message}";
            RiskColor = "Gray";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowToastAsync(string message)
    {
        var toast = Toast.Make(message, ToastDuration.Short, 14);
        await toast.Show();
    }

    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        if (PredictionHistory.Count == 0)
        {
            await Application.Current.MainPage.DisplayAlert("Export", "No data to export.", "OK");
            return;
        }

        await ExportHelper.ExportRiskPredictionHistoryToPdfAsync(PredictionHistory.ToList());
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        if (PredictionHistory.Count == 0)
        {
            await Application.Current.MainPage.DisplayAlert("Share", "No data to share.", "OK");
            return;
        }

        await ExportHelper.ShareRiskPredictionHistoryAsync(PredictionHistory.ToList());
    }

    [RelayCommand]
    private async Task ExportHistoryAsync()
    {
        await ExportHelper.ExportRiskPredictionHistoryToExcelAsync(PredictionHistory.ToList());
    }

    [RelayCommand]
    private async Task SharePredictionAsync()
    {
        if (SelectedCompany == null || string.IsNullOrWhiteSpace(RiskLevel))
            return;

        string message = $"Company: {SelectedCompany.Name}\n{RiskLevel}";

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Company Risk Prediction",
                Text = message
            });
        });
    }

    private string MapNatureOfWorkToCompanyType(string natureOfWork)
    {
        if (string.IsNullOrWhiteSpace(natureOfWork))
            return "Other";

        if (natureOfWork.Contains("oil", StringComparison.OrdinalIgnoreCase))
            return "Oil & Gas Stations";
        if (natureOfWork.Contains("food", StringComparison.OrdinalIgnoreCase))
            return "Food Processing Companies";
        if (natureOfWork.Contains("wood", StringComparison.OrdinalIgnoreCase))
            return "Wood Processing Companies";
        if (natureOfWork.Contains("office", StringComparison.OrdinalIgnoreCase))
            return "Offices";
        if (natureOfWork.Contains("warehouse", StringComparison.OrdinalIgnoreCase))
            return "Warehouses";
        if (natureOfWork.Contains("shop", StringComparison.OrdinalIgnoreCase))
            return "Shops";
        if (natureOfWork.Contains("sachet", StringComparison.OrdinalIgnoreCase))
            return "Sachet Water Production";
        if (natureOfWork.Contains("manufactur", StringComparison.OrdinalIgnoreCase))
            return "Manufacturing Companies";

        return "Other";
    }

    private string GetRiskColor(string level) => level switch
    {
        "High" => "Red",
        "Medium" => "Orange",
        "Low" => "Green",
        _ => "Gray"
    };
}