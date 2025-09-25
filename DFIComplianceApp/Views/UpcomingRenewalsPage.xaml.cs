// Views/UpcomingRenewalsPage.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using System.IO;
using ClosedXML.Excel;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Microsoft.Maui.Storage;

namespace DFIComplianceApp.Views
{
    public partial class UpcomingRenewalsPage : ContentPage
    {
        private readonly IAppDatabase _db = App.Database;

        private readonly List<RenewalDisplayModel> _allRenewals = new();
        private List<RenewalDisplayModel> _filteredRenewals = new();

        public UpcomingRenewalsPage()
        {
            InitializeComponent();

            // Initialize pickers selected indexes
            StatusFilterPicker.SelectedIndex = 0; // "All"
            SortPicker.SelectedIndex = 0; // Name Ascending
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadRenewalsAsync();
        }

        private async Task LoadRenewalsAsync()
        {
            _allRenewals.Clear();
            ShowLoadingState(true);

            try
            {
                var companies = await App.Firebase.GetCompaniesAsync();
                var now = DateTime.Today;

                // Fetch all company renewals in parallel
                var renewalTasks = companies.Select(c => App.Firebase.GetCompanyRenewalsAsync(c.Id.ToString())
                    .ContinueWith(t => new { Company = c, Renewals = t.Result })).ToList();

                var results = await Task.WhenAll(renewalTasks);

                foreach (var res in results)
                {
                    var company = res.Company;
                    var renewals = res.Renewals;

                    var lastRenewed = renewals
                        .OrderByDescending(r => r.RenewalDate)
                        .FirstOrDefault()?.RenewalDate ?? company.RegistrationDate;

                    var renewalYear = lastRenewed.Year + 1;
                    var dueSoonStart = new DateTime(renewalYear, 1, 1);
                    var dueSoonEnd = new DateTime(renewalYear, 3, 31);

                    string status;
                    Color rowColor;
                    string daysRemaining;

                    if (now > dueSoonEnd)
                    {
                        status = "🔴 Overdue";
                        rowColor = Colors.MistyRose;
                        daysRemaining = "Overdue";
                    }
                    else if (now >= dueSoonStart && now <= dueSoonEnd)
                    {
                        int daysLeft = (dueSoonEnd - now).Days;
                        status = "🟡 Due Soon";
                        rowColor = Colors.LemonChiffon;
                        daysRemaining = $"{daysLeft} days remaining";
                    }
                    else
                    {
                        status = "✅ Up to Date";
                        rowColor = Colors.Honeydew;
                        daysRemaining = $"{(dueSoonStart - now).Days} days until due soon";
                    }

                    _allRenewals.Add(new RenewalDisplayModel
                    {
                        Name = company.Name,
                        Location = company.Location,
                        NatureOfWork = company.NatureOfWork,
                        Occupier = company.Occupier,
                        RenewalStatus = status,
                        NextRenewalDate = dueSoonEnd,
                        LastRenewalDisplay = $"Last Renewal: {lastRenewed:dd MMM yyyy}",
                        DaysRemainingDisplay = daysRemaining,
                        RowColor = rowColor
                    });
                }

                ApplyFiltersAndSort();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load upcoming renewals from Firebase.\n{ex.Message}", "OK");
            }
            finally
            {
                ShowLoadingState(false);
            }
        }

        // Add these methods for the loading indicator
        void ShowLoadingState(bool isLoading)
        {
            LoadingIndicator.IsRunning = isLoading;
            LoadingIndicator.IsVisible = isLoading;
            MainContent.IsVisible = !isLoading;
        }

        void HideLoadingState()
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            MainContent.IsVisible = true;
        }


        private void ApplyFiltersAndSort()
        {
            IEnumerable<RenewalDisplayModel> filtered = _allRenewals;

            // Filter by status
            if (StatusFilterPicker.SelectedIndex > 0)
            {
                string status = StatusFilterPicker.Items[StatusFilterPicker.SelectedIndex];
                filtered = filtered.Where(r => r.RenewalStatus.Contains(status));
            }

            // Filter by search text
            string searchText = SearchBar.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(r =>
                    (r.Name?.ToLower().Contains(searchText) ?? false) ||
                    (r.Occupier?.ToLower().Contains(searchText) ?? false) ||
                    (r.Location?.ToLower().Contains(searchText) ?? false));
            }

            // Sort by selected option
            switch (SortPicker.SelectedIndex)
            {
                case 0: // Name ↑
                    filtered = filtered.OrderBy(r => r.Name);
                    break;
                case 1: // Name ↓
                    filtered = filtered.OrderByDescending(r => r.Name);
                    break;
                case 2: // Renewal Date ↑
                    filtered = filtered.OrderBy(r => r.NextRenewalDate);
                    break;
                case 3: // Renewal Date ↓
                    filtered = filtered.OrderByDescending(r => r.NextRenewalDate);
                    break;
                default:
                    filtered = filtered.OrderBy(r => r.Name);
                    break;
            }

            _filteredRenewals = filtered.ToList();
            RenewalsView.ItemsSource = _filteredRenewals;
        }

        // Event handlers for UI controls

        private void OnFilterChanged(object sender, EventArgs e)
        {
            ApplyFiltersAndSort();
        }

        private void OnSearchBarTextChanged(object sender, EventArgs e)
        {
            ApplyFiltersAndSort();
        }

        private void OnSortChanged(object sender, EventArgs e)
        {
            ApplyFiltersAndSort();
        }

        // Export to PDF
        private async void OnExportPdfClicked(object sender, EventArgs e)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(FileSystem.CacheDirectory, $"UpcomingRenewals_{stamp}.pdf");

            var pdf = new PdfDocument();
            var font = new XFont("Arial", 14);
            double margin = 40, lineH = 22, y = margin, usableY;
            XGraphics StartPage()
            {
                var p = pdf.AddPage();
                usableY = p.Height - margin;
                y = margin;
                return XGraphics.FromPdfPage(p);
            }

            var gfx = StartPage();
            foreach (var r in _filteredRenewals)
            {
                if (y + lineH * 4 > usableY)
                {
                    gfx.Dispose();
                    gfx = StartPage();
                }
                gfx.DrawString(r.Name, font, XBrushes.Black, new XPoint(margin, y)); y += lineH;
                gfx.DrawString(r.RenewalStatus, font, XBrushes.Black, new XPoint(margin, y)); y += lineH;
                gfx.DrawString(r.LastRenewalDisplay, font, XBrushes.Black, new XPoint(margin, y)); y += lineH;
                gfx.DrawString(r.DaysRemainingDisplay, font, XBrushes.Black, new XPoint(margin, y)); y += lineH;
                y += 10;
            }

            using var ms = new MemoryStream();
            pdf.Save(ms);
            pdf.Dispose();
            await File.WriteAllBytesAsync(path, ms.ToArray());
            await DisplayAlert("Exported", $"PDF saved:\n{path}", "OK");
        }

        // Export to Excel
        private async void OnExportExcelClicked(object sender, EventArgs e)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(FileSystem.CacheDirectory, $"UpcomingRenewals_{stamp}.xlsx");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Renewals");
            ws.Cell("A1").Value = "Company";
            ws.Cell("B1").Value = "Status";
            ws.Cell("C1").Value = "Last Renewal";
            ws.Cell("D1").Value = "Remaining/Overdue";

            for (int i = 0; i < _filteredRenewals.Count; i++)
            {
                var r = _filteredRenewals[i];
                ws.Cell(i + 2, 1).Value = r.Name;
                ws.Cell(i + 2, 2).Value = r.RenewalStatus;
                ws.Cell(i + 2, 3).Value = r.LastRenewalDisplay;
                ws.Cell(i + 2, 4).Value = r.DaysRemainingDisplay;
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            wb.SaveAs(fs);
            await DisplayAlert("Exported", $"Excel saved:\n{path}", "OK");
        }

        // Renewal display model for UI binding
        public class RenewalDisplayModel
        {
            public string Name { get; set; }
            public string Location { get; set; }
            public string NatureOfWork { get; set; }
            public string Occupier { get; set; }
            public string RenewalStatus { get; set; }
            public DateTime? NextRenewalDate { get; set; }
            public string LastRenewalDisplay { get; set; }
            public string DaysRemainingDisplay { get; set; }
            public Color RowColor { get; set; }
        }
    }
}
