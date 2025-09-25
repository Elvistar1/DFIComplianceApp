// Views/AIReportOversightPage.xaml.cs – Full fixed version with expert system fallback
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DFIComplianceApp.ViewModels;

namespace DFIComplianceApp.Views
{
    public partial class AIReportOversightPage : ContentPage, INotifyPropertyChanged
    {
        private const int PageSize = 10;
        private bool _isLoadingMore;
        private int _currentPage = 0;
        private readonly IAppDatabase _db = App.Database;
        private readonly IAdviceService _ai = App.Services.GetRequiredService<IAdviceService>();
        private readonly ObservableCollection<ReportVM> _reports = new();
        private CancellationTokenSource? _analyseCts;
        private int _page = 0;
        private string _searchText = string.Empty;
        private string _statusFilter = string.Empty;
        private readonly IExpertSystemService _expertSystem = App.Services.GetRequiredService<IExpertSystemService>();
        private readonly Dictionary<Guid, int> _retryCounts = new();
        private readonly TimeSpan _retryCooldown = TimeSpan.FromMinutes(5);
        private readonly Dictionary<Guid, DateTime> _lastRetryTimes = new();
        private readonly FirebaseAuthService _firebaseService;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        public AIReportOversightPage()
        {
            InitializeComponent();
            ReportsCollectionView.ItemsSource = _reports;
            BindingContext = this;
            _firebaseService = new FirebaseAuthService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            App.Firebase.StartPendingReportListener();
            App.Firebase.PendingReportsChanged += OnPendingReportsChanged;
            await LoadAsync(reset: true);
            OnFilterChanged(StatusPicker, EventArgs.Empty);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            App.Firebase.PendingReportsChanged -= OnPendingReportsChanged;
            App.Firebase.StopPendingReportListener();
        }

        private async void OnPendingReportsChanged(object sender, EventArgs e)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await LoadAsync(reset: true); // Reload reports to reflect status changes
            });
        }

        private async void OnRefreshClicked(object sender, EventArgs _) => await LoadAsync(reset: true);
        private async void OnRefresh(object sender, EventArgs _) => await LoadAsync(reset: true);

        private void OnFilterChanged(object sender, EventArgs _)
        {
            if (SearchBar == null || StatusPicker == null) return;
            _searchText = SearchBar.Text?.Trim() ?? string.Empty;
            _statusFilter = StatusPicker.SelectedItem as string ?? "All";
            ApplyFilter();
        }

        private async Task<string> GetAdviceWithFallbackAsync(string json, CancellationToken ct)
        {
            try
            {
                var aiResult = await GetAdviceWithRetryAsync(json, ct);

                if (!string.IsNullOrWhiteSpace(aiResult) && !aiResult.StartsWith("{") && !aiResult.StartsWith("["))
                {
                    // Assume it's valid non-JSON AI result.
                    return aiResult;
                }
                else
                {
                    // If AI returned invalid or JSON when you expected text:
                    throw new Exception("AI result format unexpected.");
                }
            }
            catch
            {
                await DisplayAlert("Notice", "AI service unavailable or returned invalid format. Using Expert System fallback.", "OK");
                return _expertSystem.GenerateDetailedReport(json);
            }
        }

        private async void OnApproveClicked(object s, EventArgs e)
        {
            if (s is not Button { CommandParameter: ReportVM vm }) return;
            vm.SetStatus("Approved");
            vm.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(vm); // This should await the Firebase update
            Audit("Approve", vm);
            await LoadAsync(reset: true); // Only reload after update is confirmed
        }

        private async void OnFlagClicked(object s, EventArgs e)
        {
            if (s is not Button { CommandParameter: ReportVM vm }) return;
            string comment = await DisplayPromptAsync("Flag Report", "Enter reason for flagging:");
            if (string.IsNullOrWhiteSpace(comment))
            {
                await DisplayAlert("Cancelled", "Flagging cancelled. No reason provided.", "OK");
                return;
            }
            vm.SetStatus("Flagged");
            vm.UpdatedAt = DateTime.UtcNow;
            vm.ReviewerComment = comment;
            await SaveAsync(vm);
            await App.Firebase.SaveOutboxAsync(new OutboxMessage
            {
                Recipient = vm.InspectorUsername,
                Subject = $"Inspection Report Flagged – {vm.Premises}",
                Body = $"Your inspection report for {vm.Premises} has been flagged by the Director.\n\nReason: {comment}",
                CreatedAt = DateTime.UtcNow,
                Type = "Flagged" // <-- Add this line
            });
            Audit("Flag", vm);
            await DisplayAlert("Flagged", "The report was flagged and the inspector has been notified.", "OK");
            await LoadAsync(reset: true); // Ensure UI refresh after status change
        }

        private async void OnUnflagClicked(object s, EventArgs e)
        {
            if (s is not Button { CommandParameter: ReportVM vm }) return;
            vm.SetStatus("Approved"); // or "Completed" if that's your workflow
            vm.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(vm);

            // Notify inspector
            await App.Firebase.SaveOutboxAsync(new OutboxMessage
            {
                Recipient = vm.InspectorUsername,
                Subject = $"Inspection Report Unflagged – {vm.Premises}",
                Body = $"Your inspection report for {vm.Premises} has been reviewed and unflagged by the Director. No further action is required.",
                CreatedAt = DateTime.UtcNow,
                Type = "Info" // or "Unflagged" if you want, but make sure to include it in the filter
            });

            Audit("Unflag", vm);
            await DisplayAlert("Unflagged", "The report has been unflagged, marked as approved, and the inspector has been notified.", "OK");
            await LoadAsync(reset: true);
        }

        private async void OnReportTapped(object s, TappedEventArgs e)
        {
            if (s is not Frame { BindingContext: ReportVM vm }) return;
            await Navigation.PushModalAsync(new NavigationPage(new AIReportDetailPage(vm.Id)), true);
        }

        private async Task LoadAsync(bool reset = false)
        {
            IsLoading = true;
            try
            {
                if (reset)
                {
                    _reports.Clear();
                    _page = 0;
                }

                var rows = await App.Firebase.GetPendingReportsAsync();

                // Inspector restriction: only see own reports
                if (App.CurrentUser?.Role == "Inspector")
                {
                    rows = rows.Where(r => r.InspectorUsername == App.CurrentUser.Username).ToList();
                }

                var batch = rows.Skip(_page * PageSize).Take(PageSize);
                foreach (var r in batch)
                {
                    if (Guid.TryParse(r.Id, out var rGuid) && !_reports.Any(x => x.Id == rGuid))
                        _reports.Add(new ReportVM(r));
                }

                _page++;
                ApplyFilter();
                ReportsRefreshView.IsRefreshing = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            var filtered = _reports.Where(r =>
                (_searchText.Length == 0 || r.Premises.Contains(_searchText, StringComparison.OrdinalIgnoreCase)) &&
                (_statusFilter == "All" || r.Status.Equals(_statusFilter, StringComparison.OrdinalIgnoreCase)));

            // Inspector restriction: hide advice unless approved
            if (App.CurrentUser?.Role == "Inspector")
            {
                foreach (var vm in filtered)
                {
                    if (vm.Status != "Approved")
                        vm.Advice = "(Not available until approved by director)";
                }
            }

            ReportsCollectionView.ItemsSource = new ObservableCollection<ReportVM>(filtered);
        }

        private async Task<string> GetAdviceWithRetryAsync(string json, CancellationToken ct)
        {
            const int maxAttempts = 3;
            int delay = 2000;

            for (int i = 1; i <= maxAttempts; i++)
            {
                try
                {
                    var raw = await _ai.GetAdviceAsync(json, ct);

                    if (raw.Trim().StartsWith("{"))
                    {
                        var result = JsonSerializer.Deserialize<AiAdviceResult>(raw);
                        if (result?.Success == true && !string.IsNullOrWhiteSpace(result.Content))
                            return result.Content;

                        throw new Exception("AI service returned unsuccessful result.");
                    }
                    else
                    {
                        // Assume plain text fallback if not JSON
                        return raw;
                    }
                }
                catch when (i < maxAttempts)
                {
                    await Task.Delay(delay, ct);
                    delay *= 2;
                }
            }

            throw new Exception("AI service unreachable after retries");
        }

        private Task SaveAsync(ReportVM vm) => App.Firebase.UpdatePendingReportAsync(vm.ToRow());

        private static void Audit(string action, ReportVM vm) => App.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            Action = $"{action} AI report",
            Details = vm.Premises,
            PerformedBy = App.CurrentUser?.Username ?? "Unknown"
        });

        private static string BuildHeuristicAdvice(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Array)
                    return $"⚠️ No structured inspection data found. Raw advice:\n\n{json}";

                int total = 0, compliant = 0;
                var highRisk = new StringBuilder();

                foreach (var a in answers.EnumerateArray())
                {
                    total++;
                    if (!a.TryGetProperty("compliant", out var compliantProp) ||
                        !a.TryGetProperty("risk", out var riskProp) ||
                        !a.TryGetProperty("question", out var questionProp))
                        continue; // Skip incomplete entries

                    bool ok = compliantProp.GetBoolean();
                    if (ok) compliant++;
                    else if (riskProp.GetString()?.Equals("High", StringComparison.OrdinalIgnoreCase) == true)
                        highRisk.AppendLine("• " + questionProp.GetString());
                }

                double pct = total == 0 ? 0 : compliant * 100.0 / total;
                var sb = new StringBuilder().AppendLine($"Compliance rate: {pct:F0}% ({compliant}/{total})");

                if (highRisk.Length > 0)
                {
                    sb.AppendLine().AppendLine("High‑risk items needing urgent attention:").Append(highRisk);
                }
                else
                {
                    sb.AppendLine("No high‑risk non‑compliant items detected.");
                }

                sb.AppendLine().AppendLine("Recommended next steps:")
                  .AppendLine("• Address high‑risk items immediately.")
                  .AppendLine("• Schedule a follow‑up inspection once corrections are in place.")
                  .AppendLine("• Keep safety records up to date and train staff regularly.");

                return sb.ToString();
            }
            catch (JsonException)
            {
                return $"⚠️ Invalid or unstructured advice content. Raw advice:\n\n{json}";
            }
        }

        private async Task AnalyseAsync(ReportVM vm, bool aiOnly = false)
        {
            // Only directors can analyze
            if (App.CurrentUser?.Role != "Director")
            {
                await DisplayAlert("Access Denied", "Only directors can generate or analyze reports.", "OK");
                return;
            }

            _analyseCts?.Cancel();
            _analyseCts = new CancellationTokenSource();
            vm.SetAnalysing(true);

            try
            {
                string result = aiOnly
                    ? await GetAdviceWithRetryAsync(vm.Json, _analyseCts.Token)
                    : await GetAdviceWithFallbackAsync(vm.Json, _analyseCts.Token);

                if (!string.IsNullOrWhiteSpace(result))
                {
                    vm.Advice = result;
                    vm.SetStatus("Completed");
                }
                else
                {
                    vm.Advice = "⚠ AI service returned an empty response. Please retry or use Expert System fallback.";
                    vm.SetStatus("Failed");
                }

                vm.UpdatedAt = DateTime.UtcNow;

                // Save to Firebase (update existing PendingAiReport, not a new AIReport)
                await App.Firebase.UpdatePendingReportAsync(vm.ToRow());
            }
            catch (Exception ex)
            {
                vm.Advice = $"⚠ Analysis failed: {ex.Message}";
                vm.SetStatus("Failed");
                await DisplayAlert("Analysis Failed", ex.Message, "OK");
            }
            finally
            {
                vm.SetAnalysing(false);
                await SaveAsync(vm);
                await LoadAsync(reset: true); // Ensure UI refresh after analysis
            }
        }

        private static bool IsValidJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();
            if ((input.StartsWith("{") && input.EndsWith("}")) || (input.StartsWith("[") && input.EndsWith("]")))
            {
                try
                {
                    JsonDocument.Parse(input);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private async void OnAnalyseClicked(object sender, EventArgs e)
        {
            if (sender is not Button { CommandParameter: ReportVM vm }) return;
            await AnalyseAsync(vm, aiOnly: false);
        }

        private async void OnRetryAiOnlyClicked(object sender, EventArgs e)
        {
            if (sender is not Button { CommandParameter: ReportVM vm }) return;
            await AnalyseAsync(vm, aiOnly: true);
        }

        private async void OnLoadMore(object sender, EventArgs e)
        {
            if (_isLoadingMore) return;
            _isLoadingMore = true;

            try
            {
                var allItems = await App.Firebase.GetPendingReportsAsync();

                // Inspector restriction: only see own reports
                if (App.CurrentUser?.Role == "Inspector")
                {
                    allItems = allItems.Where(r => r.InspectorUsername == App.CurrentUser.Username).ToList();
                }

                var moreItems = allItems.Skip(_currentPage * PageSize).Take(PageSize).ToList();
                foreach (var item in moreItems) _reports.Add(new ReportVM(item));
                _currentPage++;
            }
            finally { _isLoadingMore = false; }
        }

        private async void OnSendForReanalysisClicked(object s, EventArgs e)
        {
            if (s is not Button { CommandParameter: ReportVM vm }) return;
            vm.SetStatus("Queued");
            vm.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(vm);

            // Notify inspector
            await App.Firebase.SaveOutboxAsync(new OutboxMessage
            {
                Recipient = vm.InspectorUsername,
                Subject = $"Inspection Report Sent for Re-analysis – {vm.Premises}",
                Body = $"Your inspection report for {vm.Premises} has been sent back for re-analysis. Please await further instructions.",
                CreatedAt = DateTime.UtcNow
            });

            Audit("Reanalysis", vm);
            await DisplayAlert("Re-analysis", "The report has been sent for re-analysis and the inspector has been notified.", "OK");
            await LoadAsync(reset: true);
        }

        // Add INotifyPropertyChanged implementation if not present
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}