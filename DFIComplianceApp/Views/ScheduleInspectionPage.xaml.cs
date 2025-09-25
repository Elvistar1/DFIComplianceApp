using DFIComplianceApp.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DFIComplianceApp.Views
{
    public partial class ScheduleInspectionPage : ContentPage
    {
        private List<Company> _companies = new();
        private List<User> _inspectors = new();
        private List<string> _natureOfWorks = new();
        private List<Company> _selectedCompanyGroup = new();

        public ScheduleInspectionPage() => InitializeComponent();

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                await ShowLoadingAsync(true, "Loading data...");
                await LoadNatureOfWorkPickerAsync();
                await RefreshUpcomingListAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load data: {ex.Message}", "OK");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async Task LoadNatureOfWorkPickerAsync()
        {
            _companies = await App.Firebase.GetCompaniesAsync();

            _inspectors = (await App.Firebase.GetUsersAsync())
                .Where(u => u.Role == "Inspector" && u.IsActive)
                .ToList();

            _natureOfWorks = _companies.Select(c => c.NatureOfWork)
                                       .Distinct()
                                       .OrderBy(x => x)
                                       .ToList();

            NatureOfWorkPicker.ItemsSource = _natureOfWorks;
            NatureOfWorkPicker.SelectedIndex = -1;

            InspectorPicker.ItemsSource = _inspectors;
            InspectorPicker.SelectedIndex = -1;
        }

        private void OnNatureOfWorkSelected(object sender, EventArgs e)
        {
            if (NatureOfWorkPicker.SelectedIndex < 0) return;

            var selectedWork = NatureOfWorkPicker.SelectedItem?.ToString();
            var filteredCompanies = _companies
                .Where(c => c.NatureOfWork == selectedWork)
                .Select(c => c.Name)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            CompanyPicker.SelectedIndex = -1;
            CompanyPicker.ItemsSource = filteredCompanies;
        }

        private void OnCompanySelected(object sender, EventArgs e)
        {
            if (CompanyPicker.SelectedIndex < 0) return;

            var selectedCompanyName = CompanyPicker.SelectedItem.ToString();
            _selectedCompanyGroup = _companies
                .Where(c => c.Name == selectedCompanyName)
                .ToList();

            var fileNumbers = _selectedCompanyGroup
                .Select(c => c.FileNumber)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            FileNumberPicker.SelectedIndex = -1;
            FileNumberPicker.ItemsSource = fileNumbers;

            ClearCompanyDetails();
        }

        private void OnFileNumberSelected(object sender, EventArgs e)
        {
            if (FileNumberPicker.SelectedIndex < 0) return;

            var selectedFileNumber = FileNumberPicker.SelectedItem.ToString();
            var company = _selectedCompanyGroup.FirstOrDefault(c => c.FileNumber == selectedFileNumber);

            if (company != null)
            {
                LocationEntry.Text = company.Location;
                ContactEntry.Text = company.Contact;
                OccupierEntry.Text = company.Occupier;
            }
        }

        private void ClearCompanyDetails()
        {
            LocationEntry.Text = string.Empty;
            ContactEntry.Text = string.Empty;
            OccupierEntry.Text = string.Empty;
        }

        private async Task RefreshUpcomingListAsync()
        {
            try
            {
                await ShowLoadingAsync(true, "Loading upcoming inspections...");

                var today = DateTime.Today;
                var allScheduled = await App.Firebase.GetAllScheduledInspectionsAsync();
                var completed = await App.Firebase.GetInspectionsAsync();

                var completedIds = completed
                    .Where(x => x.ScheduledInspectionId != Guid.Empty)
                    .Select(x => x.ScheduledInspectionId)
                    .ToHashSet();

                var filtered = allScheduled
                    .Where(x => x.ScheduledDate >= today && !completedIds.Contains(x.Id))
                    .OrderBy(x => x.ScheduledDate)
                    .ToList();

                InspectionCollection.ItemsSource = filtered;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load upcoming inspections: {ex.Message}", "OK");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (NatureOfWorkPicker.SelectedIndex < 0 ||
                CompanyPicker.SelectedIndex < 0 ||
                FileNumberPicker.SelectedIndex < 0 ||
                InspectorPicker.SelectedIndex < 0)
            {
                await DisplayAlert("Missing data", "Please select all required fields.", "OK");
                return;
            }

            var selectedFileNumber = (string)FileNumberPicker.SelectedItem;
            var company = _selectedCompanyGroup.FirstOrDefault(c => c.FileNumber == selectedFileNumber);

            if (company == null)
            {
                await DisplayAlert("Validation", "Selected company file number not found.", "OK");
                return;
            }

            if (InspectorPicker.SelectedItem is not User inspector)
            {
                await DisplayAlert("Validation", "Please select an inspector.", "OK");
                return;
            }

            var date = DatePicker.Date;
            if (date < DateTime.Today)
            {
                await DisplayAlert("Invalid date", "Inspection date cannot be in the past.", "OK");
                return;
            }

            var scheduledInspection = new ScheduledInspection
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CompanyName = company.Name,
                FileNumber = company.FileNumber,
                Location = company.Location,
                Contact = company.Contact,
                Occupier = company.Occupier,
                InspectorUsername = inspector.Username,
                ScheduledDate = date,
                Notes = string.IsNullOrWhiteSpace(NotesEditor.Text) ? string.Empty : NotesEditor.Text.Trim(),
                InspectorIdsJson = JsonSerializer.Serialize(new List<string> { inspector.Id.ToString() }),
                InspectorUsernamesJson = JsonSerializer.Serialize(new List<string> { inspector.Username })
            };

            try
            {
                ShowLoading(true, "Saving inspection...");

                await App.Firebase.SaveScheduledInspectionAsync(scheduledInspection);

                await App.Firebase.PushAuditLogAsync(new AuditLog
                {
                    Username = App.CurrentUser?.Username ?? "Unknown",
                    Role = App.CurrentUser?.Role ?? "Director",
                    Action = $"Scheduled inspection for {company.Name} on {date:dd MMM yyyy} for inspector {inspector.Username}",
                    Timestamp = DateTime.Now
                });

                await DisplayAlert("Success", "Inspection scheduled successfully.", "OK");

                // Clear UI
                NatureOfWorkPicker.SelectedIndex = -1;
                CompanyPicker.SelectedIndex = -1;
                CompanyPicker.ItemsSource = null;
                FileNumberPicker.SelectedIndex = -1;
                FileNumberPicker.ItemsSource = null;
                InspectorPicker.SelectedIndex = -1;
                DatePicker.Date = DateTime.Today;
                NotesEditor.Text = string.Empty;
                ClearCompanyDetails();

                await RefreshUpcomingListAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not save: {ex.Message}", "OK");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void ShowLoading(bool isLoading, string message = "")
        {
            LoadingOverlay.IsVisible = isLoading;
            MainGrid.IsEnabled = !isLoading;
            LoadingLabel.Text = message;
        }

        private Task ShowLoadingAsync(bool isLoading, string message = "")
        {
            ShowLoading(isLoading, message);
            return Task.CompletedTask;
        }
    }
}
