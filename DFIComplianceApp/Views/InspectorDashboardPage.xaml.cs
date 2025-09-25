using DFIComplianceApp.Helpers;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DFIComplianceApp.Views
{
    public partial class InspectorDashboardPage : ContentPage
    {
        private readonly FirebaseAuthService _firebaseService;
        private INotificationService _notificationService;
        public string InspectorUsername { get; }
        public int DueSoonCount { get; set; }
        public Color ReminderColour { get; set; }
        public string Greeting => $"Welcome, {InspectorUsername}!";
        public ISeries[] InspectorStatsSeries { get; set; }
        private IDispatcherTimer _refreshTimer;
        private int _selectedYear;

        public InspectorDashboardPage(string inspectorUsername)
        {
            InitializeComponent();
            _notificationService = new NotificationService(this);
            InspectorUsername = inspectorUsername ?? "Inspector";
            _firebaseService = App.Services.GetRequiredService<FirebaseAuthService>();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Populate Year Picker from Firebase
            var inspections = await _firebaseService.GetInspectionsAsync() ?? new List<Inspection>();
            var years = inspections
                .Select(i => i.CompletedDate?.Year ?? i.PlannedDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();
            years.Insert(0, 0); // "All Years" option
            YearPicker.ItemsSource = years.Select(y => y == 0 ? "All Years" : y.ToString()).ToList();
            YearPicker.SelectedIndex = 0;

            await RefreshReminderAsync();

            if (App.CurrentUser != null)
            {
                // Show only genuinely new upcoming inspections
                await ShowInspectorNotificationsAsync();

                await CheckFlaggedNotificationsAsync();
            }

            StartAutoRefresh();
        }

        private async Task ShowInspectorNotificationsAsync()
        {
            var scheduledInspections = await _firebaseService.GetAllScheduledInspectionsAsync() ?? new List<ScheduledInspection>();
            var completedInspections = await _firebaseService.GetInspectionsAsync() ?? new List<Inspection>();

            var today = DateTime.Today;
            var in7Days = today.AddDays(7);

            var completedScheduledIds = completedInspections
                .Where(i => i.ScheduledInspectionId != Guid.Empty)
                .Select(i => i.ScheduledInspectionId)
                .ToHashSet();

            var inspectorUsernames = new List<string> { InspectorUsername };
            if (!string.IsNullOrWhiteSpace(App.CurrentUser?.Username) && !inspectorUsernames.Contains(App.CurrentUser.Username))
                inspectorUsernames.Add(App.CurrentUser.Username);

            var upcoming = scheduledInspections
                .Where(x =>
                {
                    var usernames = System.Text.Json.JsonSerializer.Deserialize<List<string>>(x.InspectorUsernamesJson ?? "[]") ?? new();
                    bool isAssigned = usernames.Any(u => inspectorUsernames.Contains(u));
                    bool isNotCompleted = !completedScheduledIds.Contains(x.Id);
                    bool isUpcoming = x.ScheduledDate.Date >= today && x.ScheduledDate.Date <= in7Days;
                    return isAssigned && isNotCompleted && isUpcoming;
                })
                .OrderBy(x => x.ScheduledDate)
                .ToList();

            string username = App.CurrentUser?.Username ?? "unknown";
            string seenKey = $"seen_inspections_{username}";

            // Load seen inspection IDs (as strings)
            var seenIds = new HashSet<string>(
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(Preferences.Get(seenKey, "[]")) ?? new List<string>()
            );

            // Find new inspections
            var newInspections = upcoming.Where(x => !seenIds.Contains(x.Id.ToString())).ToList();

            if (newInspections.Any())
            {
                var msg = $"You have {newInspections.Count} inspection{(newInspections.Count == 1 ? "" : "s")} scheduled in the next 7 days.";
                await DisplayAlert("Upcoming Inspections", msg, "OK");

                // Mark these as seen
                foreach (var insp in newInspections)
                    seenIds.Add(insp.Id.ToString());

                Preferences.Set(seenKey, System.Text.Json.JsonSerializer.Serialize(seenIds));
            }
        }
        private async void OnYearChanged(object sender, EventArgs e)
        {
            var selectedText = YearPicker.SelectedItem?.ToString();
            if (selectedText == "All Years")
                _selectedYear = 0;
            else if (int.TryParse(selectedText, out var year))
                _selectedYear = year;

            await LoadChartDataAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopAutoRefresh();
        }

        private async Task CheckFlaggedNotificationsAsync()
        {
            if (_firebaseService == null || App.CurrentUser == null) return;

            var pendingMessages = await _firebaseService.GetPendingOutboxAsync() ?? new List<OutboxMessage>();
            var inspectorUsername = InspectorUsername;
            if (string.IsNullOrWhiteSpace(inspectorUsername)) return;

            string seenMsgKey = $"seen_messages_{inspectorUsername}";
            var seenMessages = new HashSet<string>(
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(Preferences.Get(seenMsgKey, "[]")) ?? new List<string>()
            );

            var inspectorMessages = pendingMessages
                .Where(m => m.Recipient.Equals(inspectorUsername, StringComparison.OrdinalIgnoreCase) && !seenMessages.Contains(m.FirebaseId.ToString()))
                .ToList();

            foreach (var message in inspectorMessages)
            {
                await DisplayAlert("Notification", message.Body, "OK");

                // Mark as seen
                seenMessages.Add(message.FirebaseId.ToString());
                await _firebaseService.MarkOutboxSentAsync(message.FirebaseId, DateTime.UtcNow);
            }

            // Save updated seen messages
            Preferences.Set(seenMsgKey, System.Text.Json.JsonSerializer.Serialize(seenMessages));
        }


        private void StartAutoRefresh()
        {
            if (_refreshTimer != null) return;
            _refreshTimer = Dispatcher.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(30);
            _refreshTimer.Tick += async (s, e) =>
            {
                await RefreshReminderAsync();
                await LoadChartDataAsync();
                await CheckFlaggedNotificationsAsync();
            };
            _refreshTimer.Start();
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            App.CurrentUser = null;

            // Remove sensitive login info, preserve seen inspection flags
            Preferences.Remove("session_token");
            await _firebaseService.LogoutAsync();

            await Navigation.PushAsync(new LoginPage());
            Navigation.RemovePage(this);
        }

        private void StopAutoRefresh()
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
        }

        private async Task DisplayToastAsync(string message)
        {
            await _notificationService.ShowToastAsync(message, CommunityToolkit.Maui.Core.ToastDuration.Long);
        }

        private async void OnConductInspectionClicked(object s, EventArgs e) =>
            await DisplayAlert("Access Denied", "Please access inspections through the Upcoming Inspections page.", "OK");

        private async void OnUpcomingClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new UpcomingInspectionsPage(App.CurrentUser));

        private async void OnHistoryClicked(object sender, EventArgs e)
        {
            if (App.CurrentUser == null) return;
            await Navigation.PushAsync(new InspectionHistoryPage());
        }

        private async void OnExportAllClicked(object sender, EventArgs e)
        {
            var inspections = await _firebaseService.GetInspectionsAsync() ?? new List<Inspection>();
            await ExportHelper.ExportInspectionsAsync(inspections);
        }

        private async void OnRegisterCompanyClicked(object sender, EventArgs e)
        {
            var sms = App.Services.GetRequiredService<ISmsSender>();
            await Navigation.PushAsync(new CompanyRegistrationPage(InspectorUsername, sms, _firebaseService));
        }

        private async void OnRenewCompanyClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new CompanyListPage("Inspector", InspectorUsername));

        private async Task RefreshReminderAsync()
        {
            if (App.CurrentUser == null) return;

            var today = DateTime.Today;
            var in7Days = today.AddDays(7);

            var scheduledInspections = await _firebaseService.GetAllScheduledInspectionsAsync() ?? new List<ScheduledInspection>();
            var completedInspections = await _firebaseService.GetInspectionsAsync() ?? new List<Inspection>();

            var completedScheduledIds = completedInspections
                .Where(i => i.ScheduledInspectionId != Guid.Empty)
                .Select(i => i.ScheduledInspectionId)
                .ToHashSet();

            DueSoonCount = scheduledInspections.Count(x =>
            {
                try
                {
                    var inspectorIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(x.InspectorIdsJson ?? "[]") ?? new();
                    var inspectorUsernames = System.Text.Json.JsonSerializer.Deserialize<List<string>>(x.InspectorUsernamesJson ?? "[]") ?? new();

                    bool isAssigned = inspectorIds.Contains(App.CurrentUser.Id.ToString()) ||
                                      inspectorUsernames.Contains(App.CurrentUser.Username);

                    bool isNotCompleted = !completedScheduledIds.Contains(x.Id);

                    return isAssigned &&
                           isNotCompleted &&
                           x.ScheduledDate.Date >= today &&
                           x.ScheduledDate.Date <= in7Days;
                }
                catch
                {
                    return false;
                }
            });

            ReminderColour = DueSoonCount == 0 ? Colors.DarkGreen : Colors.DarkRed;

            OnPropertyChanged(nameof(DueSoonCount));
            OnPropertyChanged(nameof(ReminderColour));
        }
        private async void OnAIOversightClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AIReportOversightPage());
        }
        private async Task LoadChartDataAsync()
        {
            var inspections = await _firebaseService.GetInspectionsAsync() ?? new List<Inspection>();
            var inspectorName = App.CurrentUser?.Username;

            if (_selectedYear != 0)
            {
                inspections = inspections
                    .Where(x =>
                        (x.CompletedDate?.Year == _selectedYear) ||
                        (x.CompletedDate == null && x.PlannedDate.Year == _selectedYear))
                    .ToList();
            }

            var completed = inspections.Count(x => x.InspectorName == inspectorName && x.CompletedDate != null);
            var pending = inspections.Count(x => x.InspectorName == inspectorName && x.CompletedDate == null);
            var overdue = inspections.Count(x => x.InspectorName == inspectorName && x.CompletedDate == null && x.PlannedDate < DateTime.Today);

            InspectorStatsSeries = new ISeries[]
            {
                new ColumnSeries<ObservableValue> { Values = new ObservableValue[] { new ObservableValue(completed) }, Name = "Completed" },
                new ColumnSeries<ObservableValue> { Values = new ObservableValue[] { new ObservableValue(pending) }, Name = "Pending" },
                new ColumnSeries<ObservableValue> { Values = new ObservableValue[] { new ObservableValue(overdue) }, Name = "Overdue" }
            };
            OnPropertyChanged(nameof(InspectorStatsSeries));
        }

        public async Task LogoutAsync()
        {
            SecureStorage.RemoveAll();
            App.CurrentUser = null;
            await Navigation.PopToRootAsync();
        }
    }
}
