using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using DFIComplianceApp.Models;
using DFIComplianceApp.Notifications;
using DFIComplianceApp.Services;
using DFIComplianceApp.ViewModels;
using DFIComplianceApp.Views;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Maui;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace DFIComplianceApp.Views
{
    public partial class DirectorDashboardPage : ContentPage, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly AppDatabase _database;
        private string selectedCompanyType = null;
        private string selectedLocation = null;
        public List<string> CompanyTypes { get; set; } = new();







        private List<Company> _availableCompanies;
        public List<Company> AvailableCompanies
        {
            get => _availableCompanies;
            set { _availableCompanies = value; OnPropertyChanged(nameof(AvailableCompanies)); }
        }

        private List<int> _availableYears;
        public List<int> AvailableYears
        {
            get => _availableYears;
            set { _availableYears = value; OnPropertyChanged(nameof(AvailableYears)); }
        }


        private List<string> _availableLocations;
        public List<string> AvailableLocations
        {
            get => _availableLocations;
            set { _availableLocations = value; OnPropertyChanged(nameof(AvailableLocations)); }
        }


        private Company _selectedCompany;
        public Company SelectedCompany
        {
            get => _selectedCompany;
            set
            {
                if (_selectedCompany != value)
                {
                    _selectedCompany = value;
                    OnPropertyChanged(nameof(SelectedCompany));
                    LoadLocationsAsync(); // already here
                    _ = LoadRiskTrendChartAsync(); // ADD THIS
                    _ = LoadRiskChartAsync();         // Pie chart

                }
            }
        }

        private async void OnInboxBadgeTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new EmailQueuePage()); // Your inbox/queue page
        }



        private string _selectedLocation;
        public string SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (_selectedLocation != value)
                {
                    _selectedLocation = value;
                    OnPropertyChanged(nameof(SelectedLocation));

                    _ = OnLocationChangedAsync(); // Use an async handler to ensure correct order
                }
            }
        }

        // Add this async method to handle the logic in order
        private async Task OnLocationChangedAsync()
        {
            await LoadYearsAsync();

            // Optionally auto-select first year if available
            if (Years.Any())
            {
                SelectedYear = Years.First();
                YearPicker.SelectedItem = SelectedYear;
            }

            await UpdateCompanyListAsync();
            await LoadRiskTrendChartAsync();
            await LoadRiskChartAsync();
        }



        private int? _selectedYear;
        public int? SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (_selectedYear != value)
                {
                    _selectedYear = value;
                    OnPropertyChanged(nameof(SelectedYear));
                    _ = LoadRiskChartAsync(); // reload when changed
                    _ = LoadRiskTrendChartAsync(); // ✅ This loads the trend chart too

                }
            }
        }


        private List<Company> _filteredCompanies;
        public List<Company> FilteredCompanies
        {
            get => _filteredCompanies;
            set
            {
                _filteredCompanies = value;
                OnPropertyChanged(nameof(FilteredCompanies));
            }
        }



        public List<string> Locations
        {
            get => _locations;
            set { _locations = value; OnPropertyChanged(nameof(Locations)); }
        }
        private List<string> _locations;

        public List<int> Years
        {
            get => _years;
            set { _years = value; OnPropertyChanged(nameof(Years)); }
        }
        private List<int> _years;


        private async Task LoadCompaniesAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedCompanyType)) return;

            // Fetch directly from Firebase for always up-to-date data
            var allCompanies = await _firebaseService.GetCompaniesSafeAsync();
            var companies = allCompanies
                .Where(c => c.NatureOfWork == SelectedCompanyType)
                .ToList();

            FilteredCompanies = companies;
        }

        private IEnumerable<ISeries> _riskTrendSeries;
        public IEnumerable<ISeries> RiskTrendSeries
        {
            get => _riskTrendSeries;
            set
            {
                _riskTrendSeries = value;
                OnPropertyChanged(nameof(RiskTrendSeries));
            }
        }

        private Axis[] _riskTrendXAxis;
        public Axis[] RiskTrendXAxis
        {
            get => _riskTrendXAxis;
            set
            {
                _riskTrendXAxis = value;
                OnPropertyChanged(nameof(RiskTrendXAxis));
            }
        }

        private Axis[] _riskTrendYAxis;
        public Axis[] RiskTrendYAxis
        {
            get => _riskTrendYAxis;
            set
            {
                _riskTrendYAxis = value;
                OnPropertyChanged(nameof(RiskTrendYAxis));
            }
        }




        private async Task LoadLocationsAsync()
        {
            if (SelectedCompany == null) return;

            var locations = await _database.GetLocationsForCompanyAsync(SelectedCompany.Id);
            Locations = locations;
            AvailableLocations = locations;
            SelectedLocation = null;
            Years = new(); // assuming this resets available years based on new location/company
        }

        private async Task LoadYearsAsync()
        {
            if (SelectedCompany == null || string.IsNullOrEmpty(SelectedLocation)) return;
            var years = await _database.GetYearsForCompanyLocationAsync(SelectedCompany.NatureOfWork, SelectedLocation);
            Years = years;
            SelectedYear = null;
        }

        private void ClearFilterChain()
        {   
            SelectedCompany = null;
            FilteredCompanies = new();
            SelectedLocation = null;
            Locations = new();
            SelectedYear = null;
            Years = new();  
        }

        private string _selectedCompanyType;
        public string SelectedCompanyType
        {
            get => _selectedCompanyType;
            set
            {
                if (_selectedCompanyType != value)
                {
                    _selectedCompanyType = value;
                    OnPropertyChanged(nameof(SelectedCompanyType)); // ✅ Notify binding system

                    // ✅ Reset dependent filters
                    SelectedCompany = null;
                    SelectedLocation = null;
                    SelectedYear = null;

                    // ✅ Clear filter options and company list
                    FilteredCompanies = new();
                    Locations = new();
                    Years = new();

                    // ✅ Re-fetch based on new type
                    _ = LoadCompaniesAsync();
                    _ = LoadRiskChartAsync();         // Pie chart
                    _ = LoadRiskTrendChartAsync();    // ✅ Line chart
                }
            }
        }


        private async void OnCompanyTypeChanged(object sender, EventArgs e)
        {
            if (CompanyTypePicker.SelectedIndex == -1) return;

            SelectedCompanyType = CompanyTypePicker.SelectedItem as string;

            // Clear dependent filters
            SelectedCompany = null;
            FilteredCompanies = new();
            SelectedLocation = null;
            Locations = new();
            SelectedYear = null;
            Years = new();

            // Load companies for this type
            await UpdateCompanyListAsync();

            // Optionally auto-select first company
            if (FilteredCompanies.Any())
            {
                SelectedCompany = FilteredCompanies.First();
                CompanyPicker.SelectedItem = SelectedCompany;
            }

            await LoadRiskTrendChartAsync();
            await LoadRiskChartAsync();
        }


        private async void OnCompanyChanged(object sender, EventArgs e)
        {
            if (CompanyPicker.SelectedIndex == -1) return;

            SelectedCompany = CompanyPicker.SelectedItem as Company;

            // Clear dependent filters
            SelectedLocation = null;
            Locations = new();
            SelectedYear = null;
            Years = new();

            // Load locations for this company
            await LoadLocationsAsync();

            // Optionally auto-select first location
            if (Locations.Any())
            {
                SelectedLocation = Locations.First();
                LocationPicker.SelectedItem = SelectedLocation;
            }

            await LoadRiskTrendChartAsync(); // update trend chart
            await LoadRiskChartAsync();      // ✅ update risk chart
        }

        private async void OnYearChanged(object sender, EventArgs e)
        {
            if (YearPicker.SelectedIndex == -1) return;

            SelectedYear = (int)YearPicker.SelectedItem;

            // Ensure the chart updates after the year is set
            await LoadRiskTrendChartAsync();
            await LoadRiskChartAsync();
        }




        private async Task UpdateCompanyListAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SelectedCompanyType))
                    return;

                // Fetch all companies from Firebase
                var allCompanies = await _firebaseService.GetCompaniesSafeAsync();
                var companies = string.IsNullOrWhiteSpace(SelectedLocation)
                    ? allCompanies.Where(c => c.NatureOfWork == SelectedCompanyType)
                    : allCompanies.Where(c => c.NatureOfWork == SelectedCompanyType && c.Location == SelectedLocation);

                FilteredCompanies = companies.DistinctBy(c => c.Id).OrderBy(c => c.Name).ToList();
                SelectedCompany = null;
                OnPropertyChanged(nameof(FilteredCompanies));

                // If you store years in Firebase, fetch and filter them here.
                // Otherwise, you may need to aggregate from company or inspection data.
                // Example (pseudo-code, adjust as needed):
                // var years = await _firebaseService.GetAvailableYearsAsync();
                // AvailableYears = years.OrderByDescending(y => y).ToList();

                // Reset UI Pickers
                CompanyPicker.SelectedIndex = -1;
                YearPicker.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                await ShowToastAsync("❌ Failed to load companies or years.");
                System.Diagnostics.Debug.WriteLine($"[UpdateCompanyListAsync] Error: {ex.Message}");
            }
        }








        private string _riskSummaryText;
        public string RiskSummaryText
        {
            get => _riskSummaryText;
            set
            {
                _riskSummaryText = value;
                OnPropertyChanged(nameof(RiskSummaryText));
            }
        }


        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        static class DashboardPalette
        {
            public const string ScheduledColor = "#ff9800";
            public const string CompletedColor = "#4caf50";
            public static readonly SKColor ScheduledSKColor = SKColor.Parse(ScheduledColor);
            public static readonly SKColor CompletedSKColor = SKColor.Parse(CompletedColor);
            public const string FontFamily = "Arial";
        }

        private readonly string _currentUsername;
        private CancellationTokenSource? _cts;
        private bool _firstLoad = true;
        private readonly WeakReferenceMessenger _messenger = WeakReferenceMessenger.Default;

        // ✅ CHANGED: Make backing fields for properties so they can raise events
        private IEnumerable<ISeries> _chartSeries;
        public IEnumerable<ISeries> ChartSeries
        {
            get => _chartSeries;
            set
            {
                _chartSeries = value;
                OnPropertyChanged(nameof(ChartSeries));
            }
        }

        private Axis[] _chartXAxis;
        public Axis[] ChartXAxis
        {
            get => _chartXAxis;
            set
            {
                _chartXAxis = value;
                OnPropertyChanged(nameof(ChartXAxis));
            }
        }

        private Axis[] _chartYAxis;
        public Axis[] ChartYAxis
        {
            get => _chartYAxis;
            set
            {
                _chartYAxis = value;
                OnPropertyChanged(nameof(ChartYAxis));
            }
        }

        private IEnumerable<ISeries> _riskChartSeries;
        public IEnumerable<ISeries> RiskChartSeries
        {
            get => _riskChartSeries;
            set
            {
                _riskChartSeries = value;
                OnPropertyChanged(nameof(RiskChartSeries));
            }
        }


        private readonly FirebaseAuthService _firebaseService;

        public DirectorDashboardPage(string currentUsername)
        {
            BindingContext = this;
            InitializeComponent();
            _currentUsername = currentUsername;

            _database = App.Services.GetService<IAppDatabase>() as AppDatabase;
            _firebaseService = App.Services.GetRequiredService<FirebaseAuthService>();
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var notificationService = new DirectorNotificationService(App.Firebase);
            var notifications = await notificationService.GetStartupNotificationsAsync();

            if (notifications.Any())
            {
                string message = string.Join("\n", notifications);
                await DisplayAlert("Important Updates", message, "OK");
            }

            await Task.Delay(100); // Optional: help layout stabilize

            if (_firstLoad)
            {
                _messenger.Register<OutboxClearedMessage>(this, (_, m) =>
                {
                    MainThread.BeginInvokeOnMainThread(() => UpdateBadge(m.Value));
                    _ = ShowToastAsync("All queued emails have been sent.");
                });

                _cts = new();
                _ = ShowWelcomeToastAsync(_cts.Token);
            }

            try
            {
                if (_firstLoad)
                {
                    await LoadFilterOptionsAsync();
                    await UpdateCompanyListAsync();
                    await LoadRiskTrendChartAsync();
                    _firstLoad = false;
                }
                else
                {
                    await LoadRiskTrendChartAsync();
                }

                _ = LoadRiskChartAsync(); // Pie chart async fire-and-forget is OK

                await RefreshDashboardAsync(); // ✅ await ensures safe label update
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnAppearing] Error: {ex.Message}");
                await ShowToastAsync("Failed to load dashboard data.");
            }
        }




        private async void OnResetYearClicked(object sender, EventArgs e)
        {
            try
            {
                // ✅ Reset UI pickers
                CompanyTypePicker.SelectedIndex = -1;
                LocationPicker.SelectedIndex = -1;
                CompanyPicker.SelectedIndex = -1;

                // ✅ Reset backing variables
                SelectedCompanyType = null;
                SelectedLocation = null;
                SelectedCompany = null;

                // ✅ Handle missing or empty AvailableYears
                var currentYear = DateTime.Now.Year;

                if (AvailableYears == null || AvailableYears.Count == 0)
                {
                    AvailableYears = new List<int> { currentYear };
                    OnPropertyChanged(nameof(AvailableYears));
                    System.Diagnostics.Debug.WriteLine("⚠️ AvailableYears was empty, added current year.");
                }

                // ✅ Set SelectedYear to current year if it's in the list; fallback to first
                if (AvailableYears.Contains(currentYear))
                {
                    SelectedYear = currentYear;
                }
                else
                {
                    SelectedYear = AvailableYears.FirstOrDefault();
                }

                OnPropertyChanged(nameof(SelectedYear));

                // ✅ Update the YearPicker index safely
                if (SelectedYear.HasValue && AvailableYears != null)
                {
                    var index = AvailableYears.IndexOf(SelectedYear.Value);
                    YearPicker.SelectedIndex = index >= 0 ? index : 0;
                }
                else
                {
                    YearPicker.SelectedIndex = -1;
                }

                // ✅ Reload data based on reset filters
                await UpdateCompanyListAsync();
                await LoadRiskTrendChartAsync();
                await LoadRiskChartAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnResetYearClicked] Error: {ex}");
                await ShowToastAsync("Something went wrong while resetting filters.");
            }
        }


        private void CompanyTypePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedCompanyType = CompanyTypePicker.SelectedItem as string;
            _ = UpdateCompanyListAsync();
            _ = LoadRiskTrendChartAsync(); // 👈 refresh chart
            _ = LoadRiskChartAsync();
        }






        private async Task LoadRiskTrendChartAsync()
        {
            if (SelectedYear == null || SelectedYear < 1 || SelectedYear > 9999)
                SelectedYear = DateTime.Now.Year;

            var allHistory = await _firebaseService.GetAllRiskPredictionHistoryAsync();

            // Make filters optional: allow year-only selection
            var filtered = allHistory
                .Where(h =>
                    DateTime.TryParse(h.DatePredicted, out var dt) && dt.Year == SelectedYear &&
                    (string.IsNullOrEmpty(SelectedCompanyType) || h.CompanyType == SelectedCompanyType) &&
                    (string.IsNullOrEmpty(SelectedLocation) || h.Location == SelectedLocation)
                )
                // Only the latest prediction per company per month
                .GroupBy(h =>
                {
                    DateTime.TryParse(h.DatePredicted, out var dt);
                    return new { h.CompanyId, Month = dt.Month };
                })
                .Select(g => g.OrderByDescending(h => DateTime.Parse(h.DatePredicted)).First())
                .ToList();

            var allMonths = Enumerable.Range(1, 12)
                .Select(m => new DateTime(SelectedYear.Value, m, 1).ToString("MMM"))
                .ToList();

            var trendData = allMonths.Select((month, idx) =>
            {
                var monthNum = idx + 1;
                var monthData = filtered.Where(h =>
                    DateTime.TryParse(h.DatePredicted, out var dt) && dt.Month == monthNum);

                return new
                {
                    Month = month,
                    High = monthData.Count(h => h.RiskLevel == "High"),
                    Medium = monthData.Count(h => h.RiskLevel == "Medium"),
                    Low = monthData.Count(h => h.RiskLevel == "Low")
                };
            }).ToList();

            var labels = trendData.Select(d => d.Month).ToArray();
            var highValues = trendData.Select(d => d.High).ToList();
            var mediumValues = trendData.Select(d => d.Medium).ToList();
            var lowValues = trendData.Select(d => d.Low).ToList();

            RiskTrendChart.Series = new ISeries[]
            {
                new LineSeries<int> { Name = "High", Values = highValues, Stroke = new SolidColorPaint(SKColors.Red, 3), GeometrySize = 8, GeometryStroke = new SolidColorPaint(SKColors.Red), Fill = null },
                new LineSeries<int> { Name = "Medium", Values = mediumValues, Stroke = new SolidColorPaint(SKColors.Orange, 3), GeometrySize = 8, GeometryStroke = new SolidColorPaint(SKColors.Orange), Fill = null },
                new LineSeries<int> { Name = "Low", Values = lowValues, Stroke = new SolidColorPaint(SKColors.Green, 3), GeometrySize = 8, GeometryStroke = new SolidColorPaint(SKColors.Green), Fill = null }
            };

            RiskTrendChart.XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels, // ["Jan", "Feb", ..., "Dec"]
                    LabelsRotation = 45, // More readable on mobile
                    TextSize = 16, // Larger text for months
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    Name = "Month"
                }
            };
            RiskTrendChart.YAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0,
                    Labeler = value => value.ToString("N0"),
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    Name = "Predictions"
                }
            };
        }





        private async Task LoadFilterOptionsAsync()
        {
            try
            {
                // ✅ Ensure the database is initialized
                await _database.EnsureInitializedAsync();

                var years = await _database.GetAvailableInspectionYearsAsync();
                var locations = await _database.GetCompanyLocationsAsync();
                var companyTypes = await _database.GetCompanyTypesAsync();

                // ✅ Safely assign or fallback to defaults
                AvailableYears = years?.Where(y => y >= 1 && y <= 9999)
                                       .OrderByDescending(y => y)
                                       .ToList() ?? new();
                AvailableLocations = locations ?? new();
                CompanyTypes = companyTypes ?? new();

                OnPropertyChanged(nameof(AvailableYears));
                OnPropertyChanged(nameof(AvailableLocations));
                OnPropertyChanged(nameof(CompanyTypes));

                // ✅ Ensure at least one year exists
                if (AvailableYears == null || AvailableYears.Count == 0)
                {
                    var fallbackYear = DateTime.Now.Year;
                    AvailableYears = new List<int> { fallbackYear };
                    System.Diagnostics.Debug.WriteLine($"⚠️ No years found, using fallback: {fallbackYear}");
                }

                // ✅ Safely assign SelectedYear
                if (SelectedYear == null || !AvailableYears.Contains(SelectedYear.Value))
                {
                    SelectedYear = AvailableYears.First();
                }

                OnPropertyChanged(nameof(SelectedYear));

                // ✅ Reset selections for filters
                SelectedCompanyType = null;
                SelectedLocation = null;
                SelectedCompany = null;

                OnPropertyChanged(nameof(SelectedCompanyType));
                OnPropertyChanged(nameof(SelectedLocation));
                OnPropertyChanged(nameof(SelectedCompany));
            }
            catch (Exception ex)
            {
                await ShowToastAsync("Failed to load filter options.");
                System.Diagnostics.Debug.WriteLine($"[LoadFilterOptionsAsync] Error: {ex}");
            }
        }





        // Button Click Event Handler
        private void OnRefreshDashboardClicked(object sender, EventArgs e)
        {
            _ = RefreshDashboardAsync();
        }


        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _messenger.UnregisterAll(this);
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task ShowToastAsync(string message)
        {
            try
            {
                await Toast.Make(message, ToastDuration.Short).Show();
            }
            catch
            {
                await DisplayAlert("Info", message, "OK");
            }
        }



        private async Task ShowWelcomeToastAsync(CancellationToken token)
        {
            await ShowToastAsync("🎉 Welcome! You are now on the Director Dashboard.");
            try { await Task.Delay(15000, token); }
            catch (OperationCanceledException) { return; }

            var inspections = await App.Database.GetInspectionsAsync();
            if (inspections.Any(i => i.CompletedDate == null))
                await ShowToastAsync("⏰ Reminder: You have pending inspections to review.");
        }

        private async void OnNavigateToCompanyRiskPrediction(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new CompanyRiskPredictionPage());
        }






        private async Task RefreshOutboxBadgeAsync()
        {
            int unsent = await App.Database.GetUnsentOutboxCountAsync();
            UpdateBadge(unsent);
        }

        private void UpdateBadge(int unsent)
        {
            OutboxBadgeFrame.IsVisible = unsent > 0;
            OutboxBadgeLabel.Text = unsent.ToString();
        }

        private async void OnBadgeTapped(object? sender, EventArgs e)
        {
            if (!OutboxBadgeFrame.IsVisible) return;
            if (App.Services.GetService<OutboxPage>() is { } page)
                await Navigation.PushAsync(page);
        }

        private async Task RefreshDashboardAsync()
        {
            try
            {
                // Fetch from Firebase instead of local database
                var companies = await _firebaseService.GetCompaniesSafeAsync();
                var scheduledInspections = await _firebaseService.GetAllScheduledInspectionsAsync();
                var inspections = await _firebaseService.GetInspectionsAsync();
                // If you have a Firebase method for users, use it; otherwise, keep local as fallback
                var users = await _firebaseService.GetUsersAsync(); // Implement this in FirebaseAuthService if not present

                int scheduled = scheduledInspections?.Count(i =>
                    i.ScheduledDate >= DateTime.Today &&
                    (inspections == null || !inspections.Select(x => x.ScheduledInspectionId).Contains(i.Id))
                ) ?? 0;

                int completed = inspections?.Count(i => i.CompletedDate != null) ?? 0;
                int activeUsers = users?.Count(u => u.IsActive) ?? 0;
                int totalCompanies = companies?.Count ?? 0;

                // Thread-safe UI updates
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CompaniesCountLabel.Text = totalCompanies.ToString();
                    InspectionsCountLabel.Text = scheduled.ToString();
                    CompletedCountLabel.Text = completed.ToString();
                    UsersCountLabel.Text = activeUsers.ToString();

                    CompaniesEmptyLabel.IsVisible = totalCompanies == 0;
                    InspectionsEmptyLabel.IsVisible = scheduled == 0;
                    CompletedEmptyLabel.IsVisible = completed == 0;
                    UsersEmptyLabel.IsVisible = activeUsers == 0;
                });

                UpdateChartSeries(scheduled, completed);
                await RefreshOutboxBadgeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshDashboardAsync] Error: {ex.Message}");
                await ShowToastAsync("Failed to load dashboard data.");
            }
        }
        private void OnOutboxBadgeTapped(object sender, TappedEventArgs e)
        {
            if (App.Services.GetService<OutboxPage>() is { } page)
                Navigation.PushAsync(page);
        }
        private async void OnNewAppointmentClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new ScheduleAppointmentPage());
        }
        private async void OnInboxEmailBadgeTapped(object sender, EventArgs e)
        {
            // Navigate to InboxPage when the Inbox Email Badge is tapped
            await Navigation.PushAsync(new InboxPage());
        }


        private async void OnLocationChanged(object sender, EventArgs e)
        {
            if (LocationPicker.SelectedIndex == -1) return;

            SelectedLocation = LocationPicker.SelectedItem as string;

            // Clear dependent filter
            SelectedYear = null;
            Years = new();

            // Load years for this company/location
            await LoadYearsAsync();

            // Optionally auto-select first year
            if (Years.Any())
            {
                SelectedYear = Years.First();
                YearPicker.SelectedItem = SelectedYear;
            }

            await UpdateCompanyListAsync(); // refresh companies by new location
            await LoadRiskTrendChartAsync(); // update trend chart
            await LoadRiskChartAsync();      // ✅ update risk chart
        }
        private void UpdateChartSeries(int scheduled, int completed)
        {

            // ✅ Detect current theme
            bool isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
            var labelColor = isDarkTheme ? SKColors.White : SKColors.Black;

            ChartSeries = new ISeries[]
            {
        new ColumnSeries<int>
        {
            Name = "Scheduled",
            Values = new[] { scheduled },
            Fill = new SolidColorPaint(DashboardPalette.ScheduledSKColor),
            DataLabelsPaint = new SolidColorPaint(labelColor), // ✅ Theme-aware
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
            DataLabelsFormatter = point => point.Coordinate.PrimaryValue.ToString(),
            MaxBarWidth = 40 // ✅ Controls spacing
        },
        new ColumnSeries<int>
        {
            Name = "Completed",
            Values = new[] { completed },
            Fill = new SolidColorPaint(DashboardPalette.CompletedSKColor),
            DataLabelsPaint = new SolidColorPaint(labelColor), // ✅ Theme-aware
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
            DataLabelsFormatter = point => point.Coordinate.PrimaryValue.ToString(),
            MaxBarWidth = 40 // ✅ Controls spacing
        }
            };

            ChartXAxis = new Axis[]
            {
        new Axis
        {
            Labels = new[] { "Inspections" },
            LabelsRotation = 0,
            LabelsPaint = new SolidColorPaint(labelColor),       // ✅ Theme-aware
            SeparatorsPaint = new SolidColorPaint(labelColor),   // Optional: adjust if needed
                                              // ✅ Horizontal bar spacing
        }
            };

            ChartYAxis = new Axis[]
            {
        new Axis
        {
            MinLimit = 0,
            Name = "Count",
            LabelsPaint = new SolidColorPaint(labelColor),       // ✅ Theme-aware
            SeparatorsPaint = new SolidColorPaint(labelColor)    // Optional: adjust for better contrast
        }
            };

            OnPropertyChanged(nameof(ChartSeries));
            OnPropertyChanged(nameof(ChartXAxis));
            OnPropertyChanged(nameof(ChartYAxis));
        }

        private async Task LoadRiskChartAsync()
        {
            var allHistory = await _firebaseService.GetAllRiskPredictionHistoryAsync();

            // Filter by year, and optionally by type/location if selected
            var filtered = allHistory
                .Where(h =>
                    DateTime.TryParse(h.DatePredicted, out var dt) && dt.Year == SelectedYear &&
                    (string.IsNullOrEmpty(SelectedCompanyType) || h.CompanyType == SelectedCompanyType) &&
                    (string.IsNullOrEmpty(SelectedLocation) || h.Location == SelectedLocation)
                )
                .GroupBy(h => h.CompanyId)
                .Select(g => g.OrderByDescending(h => DateTime.Parse(h.DatePredicted)).First())
                .ToList();

            int high = filtered.Count(h => h.RiskLevel == "High");
            int medium = filtered.Count(h => h.RiskLevel == "Medium");
            int low = filtered.Count(h => h.RiskLevel == "Low");

            RiskChartSeries = new ISeries[]
            {
        new PieSeries<int> { Values = new[] { high }, Name = "High", Fill = new SolidColorPaint(SKColors.Red), DataLabelsPaint = new SolidColorPaint(SKColors.White), DataLabelsPosition = PolarLabelsPosition.Middle, DataLabelsFormatter = point => $"{point.Model}", IsVisible = high > 0 },
        new PieSeries<int> { Values = new[] { medium }, Name = "Medium", Fill = new SolidColorPaint(SKColors.Orange), DataLabelsPaint = new SolidColorPaint(SKColors.White), DataLabelsPosition = PolarLabelsPosition.Middle, DataLabelsFormatter = point => $"{point.Model}", IsVisible = medium > 0 },
        new PieSeries<int> { Values = new[] { low }, Name = "Low", Fill = new SolidColorPaint(SKColors.Green), DataLabelsPaint = new SolidColorPaint(SKColors.White), DataLabelsPosition = PolarLabelsPosition.Middle, DataLabelsFormatter = point => $"{point.Model}", IsVisible = low > 0 }
            };

            OnPropertyChanged(nameof(RiskChartSeries));
            RiskSummaryText = $"High: {high}, Medium: {medium}, Low: {low}";
        }

        private async void OnFilterChanged(object sender, EventArgs e)
        {
            // Optional: add null checks here if needed

            await LoadRiskChartAsync();
        }




        #region Navigation Handlers

        async void OnCreateUserClicked(object s, EventArgs e) =>
      await Navigation.PushAsync(
          new CreateUserPage(_currentUsername, App.Services.GetRequiredService<FirebaseAuthService>())
      );

        async void OnUpcomingRenewalsClicked(object s, EventArgs e) => await Navigation.PushAsync(new UpcomingRenewalsPage());
        async void OnManageUsersClicked(object s, EventArgs e) => await Navigation.PushAsync(new ManageUsersPage());
        async void OnScheduleInspectionClicked(object s, EventArgs e) => await Navigation.PushAsync(new ScheduleInspectionPage());
        async void OnConductInspectionClicked(object s, EventArgs e) => await DisplayAlert("Access Denied", "Please access inspections through the Upcoming Inspections page.", "OK");
        async void OnActivityLogClicked(object s, EventArgs e) => await Navigation.PushAsync(new AuditLogPage());
        async void OnSystemSettingsClicked(object s, EventArgs e) => await Navigation.PushAsync(new SystemSettingsPage());
        async void OnAIReportOversightClicked(object s, EventArgs e) => await Navigation.PushAsync(new AIReportOversightPage());
        async void OnInspectionHistoryClicked(object s, EventArgs e) => await Navigation.PushAsync(new InspectionHistoryPage());
        async void OnViewCompaniesClicked(object s, EventArgs e) => await Navigation.PushAsync(new CompanyListPage("Director", _currentUsername));

        async void OnRegisterCompanyClicked(object sender, EventArgs e)
        {
            try
            {
                var smsSender = App.Services.GetRequiredService<ISmsSender>();
                var firebaseService = App.Services.GetRequiredService<FirebaseAuthService>();
                await Dispatcher.DispatchAsync(async () =>
                    await Navigation.PushAsync(new CompanyRegistrationPage(_currentUsername, smsSender, firebaseService)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex}");
                await DisplayAlert("Navigation Error", "Sorry, we couldn’t open the registration screen. Please try again.", "OK");
            }
        }

        async void OnLogoutClicked(object s, EventArgs e)
        {
            if (await DisplayAlert("Log out", "Are you sure?", "Yes", "Cancel"))
                SecureStorage.RemoveAll();
            App.CurrentUser = null;
            await Navigation.PopToRootAsync();
            await _database.LogoutAsync();
        }

        #endregion

        #region Export Handlers

        async void OnExportToPdfClicked(object s, EventArgs e) => await ExportPdfAsync();
        async void OnExportToExcelClicked(object s, EventArgs e) => await ExportExcelAsync();

        async Task ExportPdfAsync()
        {
            string fileName = $"DashboardReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);

            var pdf = new PdfDocument();
            var font = new XFont(DashboardPalette.FontFamily, 14);
            double margin = 40, lineHeight = 22, usableY = 0, y = margin;

            XGraphics StartPage()
            {
                var p = pdf.AddPage();
                usableY = p.Height - margin;
                y = margin;
                return XGraphics.FromPdfPage(p);
            }

            XGraphics gfx = StartPage();
            string[] lines = {
                $"Director Dashboard — {DateTime.Now:dd MMM yyyy}",
                "",
                $"Total Companies        {CompaniesCountLabel.Text}",
                $"Scheduled Inspections  {InspectionsCountLabel.Text}",
                $"Completed Inspections  {CompletedCountLabel.Text}",
                $"Active Users           {UsersCountLabel.Text}"
            };

            foreach (string line in lines)
            {
                if (y + lineHeight > usableY)
                {
                    gfx.Dispose();
                    gfx = StartPage();
                }
                gfx.DrawString(line, font, XBrushes.Black, new XPoint(margin, y));
                y += lineHeight;
            }

            using var ms = new MemoryStream();
            pdf.Save(ms);
            await File.WriteAllBytesAsync(path, ms.ToArray());
            await Toast.Make($"PDF saved:\n{path}", ToastDuration.Short).Show();

        }

        async Task ExportExcelAsync()
        {
            string fileName = $"DashboardReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Dashboard");

            ws.Cell("A1").Value = "Metric";
            ws.Cell("B1").Value = "Value";
            ws.Cell("A2").Value = "Total Companies"; ws.Cell("B2").Value = CompaniesCountLabel.Text;
            ws.Cell("A3").Value = "Scheduled Inspections"; ws.Cell("B3").Value = InspectionsCountLabel.Text;
            ws.Cell("A4").Value = "Completed Inspections"; ws.Cell("B4").Value = CompletedCountLabel.Text;
            ws.Cell("A5").Value = "Active Users"; ws.Cell("B5").Value = UsersCountLabel.Text;

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            wb.SaveAs(fs);
            await Toast.Make($"Excel saved:\n{path}", ToastDuration.Short).Show();

        }

        #endregion

       
    }
}
