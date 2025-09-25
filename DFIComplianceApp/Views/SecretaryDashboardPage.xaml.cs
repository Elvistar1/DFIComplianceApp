using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace DFIComplianceApp.Views
{
    public partial class SecretaryDashboardPage : ContentPage, INotifyPropertyChanged
    {
        private readonly string _username;
        private IDispatcherTimer _refreshTimer;
        private readonly FirebaseAuthService _firebaseService;

        // Observable values
        private readonly ObservableValue _totalCompaniesValue = new(0);
        private readonly ObservableValue _totalInspectionsValue = new(0);
        private readonly ObservableValue _upcomingRenewalsValue = new(0);
        private readonly ObservableValue _overdueInspectionsValue = new(0);

        // Series collection for binding
        private ObservableCollection<ISeries> _series;
        public ObservableCollection<ISeries> Series
        {
            get => _series;
            set
            {
                if (_series != value)
                {
                    _series = value;
                    OnPropertyChanged(nameof(Series));
                }
            }
        }

        // Axes
        public Axis[] XAxes { get; }
        public Axis[] YAxes { get; }

        // Label properties
        public int TotalCompanies { get; private set; }
        public int TotalInspections { get; private set; }
        public int UpcomingRenewals { get; private set; }
        public int OverdueInspections { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public SecretaryDashboardPage(string username)
        {
            InitializeComponent();
            _username = username;
            BindingContext = this;

            _firebaseService = App.Services.GetRequiredService<FirebaseAuthService>();

            Series = new ObservableCollection<ISeries>();
            InitializeSeries();

            XAxes = new Axis[]
            {
                new Axis { Labels = new[] { "Companies", "Inspections", "Renewals", "Overdue" } }
            };
            YAxes = new Axis[] { new Axis { MinLimit = 0 } };
        }

        private void InitializeSeries()
        {
            Series.Clear();

            Series.Add(new ColumnSeries<ObservableValue>
            {
                Name = "Companies",
                Values = new List<ObservableValue> { _totalCompaniesValue },
                Fill = new SolidColorPaint(SKColors.CornflowerBlue),
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsSize = 14,
                MaxBarWidth = 40
            });
            Series.Add(new ColumnSeries<ObservableValue>
            {
                Name = "Inspections",
                Values = new List<ObservableValue> { _totalInspectionsValue },
                Fill = new SolidColorPaint(SKColors.SeaGreen),
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsSize = 14,
                MaxBarWidth = 40
            });
            Series.Add(new ColumnSeries<ObservableValue>
            {
                Name = "Renewals",
                Values = new List<ObservableValue> { _upcomingRenewalsValue },
                Fill = new SolidColorPaint(SKColors.Goldenrod),
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsSize = 14,
                MaxBarWidth = 40
            });
            Series.Add(new ColumnSeries<ObservableValue>
            {
                Name = "Overdue Inspections",
                Values = new List<ObservableValue> { _overdueInspectionsValue },
                Fill = new SolidColorPaint(SKColors.IndianRed),
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsSize = 14,
                MaxBarWidth = 40
            });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshDashboardDataAsync();
            StartAutoRefresh();
            this.BindingContext = this;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopAutoRefresh();
        }

        private void StartAutoRefresh()
        {
            if (_refreshTimer != null) return;
            _refreshTimer = Dispatcher.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(60);
            _refreshTimer.Tick += async (s, e) => await RefreshDashboardDataAsync();
            _refreshTimer.Start();
        }

        private void StopAutoRefresh()
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
        }

        private async Task RefreshDashboardDataAsync()
        {
            // Fetch data from Firebase
            var companies = await _firebaseService.GetCompaniesSafeAsync();
            var inspections = await _firebaseService.GetInspectionsAsync();

            // Calculate stats
            TotalCompanies = companies.Count;
            TotalInspections = inspections.Count;
            var today = DateTime.Today;

            // Calculate upcoming renewals
            UpcomingRenewals = companies.Count(c =>
            {
                var lastRenewed = c.LastRenewalDate ?? c.RegistrationDate;
                if (lastRenewed == default) return false;

                var renewalYear = lastRenewed.Year + 1;
                var renewalDate = new DateTime(renewalYear, 1, 1);

                var dueSoonStart = new DateTime(renewalYear, 1, 1);
                var dueSoonEnd = new DateTime(renewalYear, 3, 31);

                if (today >= dueSoonStart && today <= dueSoonEnd)
                    return true;

                if (today > dueSoonEnd)
                    return true;

                return false;
            });

            // Calculate overdue inspections
            OverdueInspections = inspections.Count(i =>
                i.PlannedDate.Date < today && !i.CompletedDate.HasValue);

            // Update observable values
            _totalCompaniesValue.Value = TotalCompanies;
            _totalInspectionsValue.Value = TotalInspections;
            _upcomingRenewalsValue.Value = UpcomingRenewals;
            _overdueInspectionsValue.Value = OverdueInspections;

            // Update series values directly (do NOT recreate series collection)
            UpdateSeriesValues();

            // Notify labels
            OnPropertyChanged(nameof(TotalCompanies));
            OnPropertyChanged(nameof(TotalInspections));
            OnPropertyChanged(nameof(UpcomingRenewals));
            OnPropertyChanged(nameof(OverdueInspections));

            // 🔔 Notifications
            await CheckSecretaryNotificationsAsync(companies, inspections);
        }

        private void UpdateSeriesValues()
        {
            foreach (var series in Series)
            {
                if (series is ColumnSeries<ObservableValue> colSeries)
                {
                    switch (colSeries.Name)
                    {
                        case "Companies":
                            colSeries.Values = new List<ObservableValue> { _totalCompaniesValue };
                            break;
                        case "Inspections":
                            colSeries.Values = new List<ObservableValue> { _totalInspectionsValue };
                            break;
                        case "Renewals":
                            colSeries.Values = new List<ObservableValue> { _upcomingRenewalsValue };
                            break;
                        case "Overdue":
                        case "Overdue Inspections":
                            colSeries.Values = new List<ObservableValue> { _overdueInspectionsValue };
                            break;
                    }
                }
            }
            OnPropertyChanged(nameof(Series));
        }

        // 🔔 Notification Logic
        private async Task CheckSecretaryNotificationsAsync(List<Company> companies, List<Inspection> inspections)
        {
            var today = DateTime.Today;

            // --- Seen renewals
            string renewalSeenKey = "SeenRenewals";
            var seenRenewals = new HashSet<string>(
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                    Preferences.Get(renewalSeenKey, "[]")) ?? new List<string>());

            var newRenewals = companies
                .Where(c =>
                {
                    var lastRenewed = c.LastRenewalDate ?? c.RegistrationDate;
                    if (lastRenewed == default) return false;

                    var renewalYear = lastRenewed.Year + 1;
                    var renewalDate = new DateTime(renewalYear, 1, 1);

                    // Renewal window: first quarter of the year
                    return today >= renewalDate && today <= renewalDate.AddMonths(3).AddDays(-1);
                })
                .Where(c => !seenRenewals.Contains(c.Id.ToString()))   // ✅ use Id
                .ToList();

            foreach (var c in newRenewals)
            {
                var toast = Toast.Make(
                    $"Upcoming Renewal: {c.Name}",   // ✅ use Name
                    ToastDuration.Short,
                    14);
                await toast.Show();

                seenRenewals.Add(c.Id.ToString());   // ✅ use Id
            }

            Preferences.Set(renewalSeenKey,
                System.Text.Json.JsonSerializer.Serialize(seenRenewals.ToList()));

            // --- Seen overdue inspections
            string overdueSeenKey = "SeenOverdueInspections";
            var seenOverdue = new HashSet<string>(
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                    Preferences.Get(overdueSeenKey, "[]")) ?? new List<string>());

            var newOverdues = inspections
                .Where(i => i.PlannedDate.Date < today && !i.CompletedDate.HasValue)
                .Where(i => !seenOverdue.Contains(i.Id.ToString()))
                .ToList();

            foreach (var i in newOverdues)
            {
                var toast = Toast.Make(
                    $"Overdue Inspection: {i.CompanyName} ({i.PlannedDate:d})",
                    ToastDuration.Short,
                    14);
                await toast.Show();

                seenOverdue.Add(i.Id.ToString());
            }

            Preferences.Set(overdueSeenKey,
                System.Text.Json.JsonSerializer.Serialize(seenOverdue.ToList()));
        }


        // Navigation handlers...
        private async void OnRenewalHistoryClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new RenewalHistoryPage());

        private async void OnScheduleClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new ScheduleInspectionPage());

        private async void OnNotesClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new InspectionHistoryPage());

        private async void OnCompaniesClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new CompanyListPage("Secretary", _username));

        private async void OnRegisterCompanyClicked(object sender, EventArgs e)
        {
            var sms = App.Services.GetRequiredService<ISmsSender>();
            await Navigation.PushAsync(new CompanyRegistrationPage(_username, sms, _firebaseService));
        }

        private async void OnExportCompaniesClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new CompanyListPage("Secretary", _username, autoExport: true));

        private async void OnUpcomingRenewalsClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new UpcomingRenewalsPage());

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Confirm Logout",
                                              "Are you sure you want to log out?",
                                              "Yes", "No");

            if (confirm)
            {
                App.CurrentUser = null;
                SecureStorage.RemoveAll();
                App.CurrentUser = null;
                await Navigation.PopToRootAsync();
                await _firebaseService.LogoutAsync();

                var toast = Toast.Make("You have been logged out successfully.",
                                       ToastDuration.Short,
                                       14);
                await toast.Show();
            }
        }
    }
}
