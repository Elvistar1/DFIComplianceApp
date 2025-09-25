using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DFIComplianceApp.Views
{
    public partial class ScheduleAppointmentPage : ContentPage
    {
        private readonly FirebaseAuthService _firebaseService;
        private List<Company> _companies = new List<Company>();
        private List<Appointment> _allAppointments = new List<Appointment>();
        private List<string> _locations = new List<string>();
        private bool _isBusy;

        public ScheduleAppointmentPage()
        {
            InitializeComponent();
            _firebaseService = App.Services.GetRequiredService<FirebaseAuthService>();

            LoadCompanies();
            LoadAppointmentHistory(); // Load history on page load
        }

        private bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                LoadingIndicator.IsVisible = value;
                LoadingIndicator.IsRunning = value;
            }
        }

        private async void LoadCompanies()
        {
            try
            {
                IsBusy = true;
                _companies = await _firebaseService.GetCompaniesSafeAsync();
                CompanyPicker.ItemsSource = _companies.Select(c => c.Name).ToList();

                // Populate company filter picker with "All" at the top
                var companyNames = _companies
                    .Select(c => c.Name)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                companyNames.Insert(0, "All");
                CompanyFilterPicker.ItemsSource = companyNames;
                CompanyFilterPicker.SelectedIndex = 0;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnCompanySelected(object sender, EventArgs e)
        {
            if (CompanyPicker.SelectedIndex == -1) return;

            string selectedCompanyName = CompanyPicker.SelectedItem.ToString();

            _locations = _companies
                .Where(c => c.Name == selectedCompanyName)
                .Select(c => c.Location)
                .Distinct()
                .ToList();

            LocationPicker.ItemsSource = _locations;
            LocationPicker.SelectedIndex = -1;

            OccupierEntry.Text = "";
            EmailEntry.Text = "";
        }

        private void OnLocationSelected(object sender, EventArgs e)
        {
            if (LocationPicker.SelectedIndex == -1 || CompanyPicker.SelectedIndex == -1) return;

            string selectedCompanyName = CompanyPicker.SelectedItem.ToString();
            string selectedLocation = LocationPicker.SelectedItem.ToString();

            var company = _companies.FirstOrDefault(c =>
                c.Name == selectedCompanyName &&
                c.Location == selectedLocation);

            if (company != null)
            {
                OccupierEntry.Text = company.Occupier;
                EmailEntry.Text = company.Email;
            }
        }

        private async void OnScheduleClicked(object sender, EventArgs e)
        {
            if (CompanyPicker.SelectedIndex == -1 || LocationPicker.SelectedIndex == -1)
            {
                await DisplayAlert("Error", "Please select a company and location.", "OK");
                return;
            }

            string company = CompanyPicker.SelectedItem.ToString();
            string location = LocationPicker.SelectedItem.ToString();
            string occupier = OccupierEntry.Text;
            string email = EmailEntry.Text;
            string purpose = PurposeEntry.Text?.Trim();
            DateTime date = AppointmentDatePicker.Date;
            TimeSpan time = AppointmentTimePicker.Time;

            if (string.IsNullOrWhiteSpace(purpose))
            {
                await DisplayAlert("Error", "Please enter the purpose of the appointment.", "OK");
                return;
            }

            DateTime appointmentDateTime = date.Date + time;

            if (appointmentDateTime < DateTime.Now)
            {
                await DisplayAlert("Error", "Appointment date and time must be in the future.", "OK");
                return;
            }

            try
            {
                IsBusy = true;

                var appointment = new Appointment
                {
                    CompanyName = company,
                    Location = location,
                    Occupier = occupier,
                    EmailContact = email,
                    MeetingDate = appointmentDateTime,
                    Subject = purpose,
                    Source = "Manual"
                };

                await _firebaseService.SaveAppointmentAsync(appointment);

                if (!string.IsNullOrWhiteSpace(email))
                {
                    var emailService = App.Services.GetRequiredService<IEmailService>();
                    string subject = $"New Appointment Scheduled with {company}";
                    string body = $"Dear {occupier},\n\n" +
                                  $"An appointment has been scheduled on {appointmentDateTime:dd MMM yyyy hh:mm tt}.\n\n" +
                                  $"Purpose: {purpose}\n\n" +
                                  "Regards,\nDepartment of Factories Inspectorate";

                    await emailService.SendAsync(email, subject, body);
                }

                await DisplayAlert("Success",
                    $"Appointment scheduled for {company} on {appointmentDateTime:dd MMM yyyy hh:mm tt}",
                    "OK");

                await LoadAppointmentHistory();

                // ✅ Clear form after scheduling
                CompanyPicker.SelectedIndex = -1;
                LocationPicker.ItemsSource = null;
                LocationPicker.SelectedIndex = -1;
                OccupierEntry.Text = "";
                EmailEntry.Text = "";
                PurposeEntry.Text = "";
                AppointmentDatePicker.Date = DateTime.Today;
                AppointmentTimePicker.Time = new TimeSpan(9, 0, 0);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async System.Threading.Tasks.Task LoadAppointmentHistory()
        {
            try
            {
                IsBusy = true;
                _allAppointments = await _firebaseService.GetAppointmentsAsync();
                ApplyFilters();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilters()
        {
            var filtered = _allAppointments.AsEnumerable();

            if (CompanyFilterPicker.SelectedIndex > 0)
            {
                string selectedCompany = CompanyFilterPicker.SelectedItem.ToString();
                filtered = filtered.Where(a => a.CompanyName == selectedCompany);
            }

            if (DateFilterPicker.Date != DateTime.Today)
            {
                filtered = filtered.Where(a => a.MeetingDate.Date == DateFilterPicker.Date);
            }

            string searchText = SearchEntry.Text?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(a =>
                    a.Subject.ToLower().Contains(searchText) ||
                    a.Location.ToLower().Contains(searchText));
            }

            AppointmentHistoryView.ItemsSource = filtered
                .OrderByDescending(a => a.MeetingDate)
                .ToList();
        }

        private void OnCompanyFilterChanged(object sender, EventArgs e) => ApplyFilters();
        private void OnDateFilterChanged(object sender, DateChangedEventArgs e) => ApplyFilters();
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    }
}
