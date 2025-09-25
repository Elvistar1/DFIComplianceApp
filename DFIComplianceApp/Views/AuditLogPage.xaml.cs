using ClosedXML.Excel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using DFIComplianceApp.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching; // For MainThread
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DFIComplianceApp.Messages;
using DFIComplianceApp.Services;
using DFIComplianceApp.Helpers;

namespace DFIComplianceApp.Views
{
    public partial class AuditLogPage : ContentPage
    {
        private readonly List<AuditLog> allLogs = new();
        private readonly ObservableCollection<AuditLog> filteredLogs = new();
        bool _isListenerAttached;
        private CancellationTokenSource? debounceCTS; // Nullable for CS8618
        private bool isLoadingLogs = false; // Prevent overlapping loads
        readonly IFirebaseAuthService _firebaseAuthService;

        // Local cache for temporary storage
        private readonly List<AuditLog> localCache = new();

        private bool logsLoadedOnce = false;

        public AuditLogPage()
        {
            InitializeComponent();

            // Subscribe to Firebase audit logs change event
            App.Firebase.AuditLogsChanged += OnAuditLogsChanged!; // Suppress CS8622 with !

            // Load logs initially
            _ = LoadAuditLogs();
        }

        private void OnAuditLogsChanged(object? sender, EventArgs e)
        {
            debounceCTS?.Cancel();
            debounceCTS = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, debounceCTS.Token);
                    await LoadAuditLogs(true);
                }
                catch (TaskCanceledException)
                {
                    // Expected cancellation, do nothing
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error in audit log debounce: {ex}");
                }
            });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            

            await LoadAuditLogs();

            if (_firebaseAuthService != null && !_isListenerAttached)
            {
                await Task.Run(() => _firebaseAuthService.StartAuditLogListener());
                _firebaseAuthService.UsersChanged += OnFirebaseAuditLogsChanged;
                _isListenerAttached = true;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            App.Firebase.StopAuditLogListener();

            if (_firebaseAuthService != null && _isListenerAttached)
            {
                _firebaseAuthService.UsersChanged -= OnFirebaseAuditLogsChanged;
                _isListenerAttached = false;
            }
            
        }

        private async Task LoadAuditLogs(bool forceReload = false)
        {
            if (isLoadingLogs) return;

            isLoadingLogs = true;
            try
            {
                await ShowLoadingAsync(true);

                List<AuditLog> logs = await Task.Run(async () =>
                {
                    if (!logsLoadedOnce || forceReload)
                    {
                        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                        {
                            try
                            {
                                var fetchedLogs = await App.Firebase.GetAuditLogsSafeAsync();
                                return fetchedLogs
                                    .Where(l => !string.IsNullOrWhiteSpace(l.Username) &&
                                                !string.IsNullOrWhiteSpace(l.Role) &&
                                                !string.IsNullOrWhiteSpace(l.Action))
                                    .OrderByDescending(l => l.Timestamp)
                                    .Take(50) // Limit for performance
                                    .ToList();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Audit Log Error (Firebase): {ex.Message}");
                            }
                        }
                    }
                    return new List<AuditLog>(localCache);
                });

                if (!logsLoadedOnce || forceReload)
                {
                    localCache.Clear();
                    localCache.AddRange(logs);
                    logsLoadedOnce = true;
                }

                // Update UI on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    allLogs.Clear();
                    allLogs.AddRange(logs);

                    filteredLogs.Clear();
                    foreach (var log in logs)
                        filteredLogs.Add(log);

                    auditLogCollectionView.ItemsSource = null;
                    auditLogCollectionView.ItemsSource = filteredLogs;
                    UpdateEmptyState();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Audit Log Error: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                    DisplayAlert("Error", $"Failed to load audit logs:\n{ex.Message}", "OK"));
            }
            finally
            {
                await ShowLoadingAsync(false);
                isLoadingLogs = false;
            }
        }

        private void UpdateEmptyState()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                emptyLabel.IsVisible = filteredLogs.Count == 0;
            });
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string query = e.NewTextValue?.ToLower() ?? "";

            _ = Task.Run(() =>
            {
                var filtered = allLogs.Where(log =>
                    (log.Username?.ToLower().Contains(query) ?? false) ||
                    (log.Role?.ToLower().Contains(query) ?? false) ||
                    (log.Action?.ToLower().Contains(query) ?? false)
                ).ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    filteredLogs.Clear();
                    foreach (var log in filtered)
                        filteredLogs.Add(log);
                    UpdateEmptyState();
                });
            });
        }

        private async void OnExportToPdfClicked(object sender, EventArgs e)
        {
            try
            {
                string filePath = Path.Combine(FileSystem.CacheDirectory, "AuditLogs.pdf");

                using var pdf = new PdfDocument();
                var page = pdf.AddPage();
                var gfx = XGraphics.FromPdfPage(page);
                var fontTitle = new XFont("Arial", 16, XFontStyle.Bold);
                var font = new XFont("Arial", 12);
                double margin = 40;
                double y = margin;

                gfx.DrawString("Audit Logs Report", fontTitle, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 30),
                    XStringFormats.TopLeft);
                y += 40;

                foreach (var log in filteredLogs)
                {
                    string line = $"[{log.Timestamp:yyyy-MM-dd HH:mm}] {log.Role} ({log.Username}) - {log.Action}";
                    gfx.DrawString(line, font, XBrushes.Black,
                        new XRect(margin, y, page.Width - 2 * margin, 20),
                        XStringFormats.TopLeft);
                    y += 20;

                    if (y > page.Height - margin)
                    {
                        page = pdf.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        y = margin;
                    }
                }

                using var stream = File.Create(filePath);
                pdf.Save(stream);
                await Toast.Make("✅ PDF exported successfully", ToastDuration.Short).Show();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"PDF export failed:\n{ex.Message}", "OK");
            }
        }

        private async void OnExportToExcelClicked(object sender, EventArgs e)
        {
            await ExportToExcelAsync();
        }

        private async Task ExportToExcelAsync()
        {
            try
            {
                var rows = (auditLogCollectionView.ItemsSource as IEnumerable<AuditLog>)?.ToList() ?? new();
                if (!rows.Any())
                {
                    await DisplayAlert("Export", "Nothing to export.", "OK");
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = Path.Combine(FileSystem.CacheDirectory, $"AuditLogs_{timestamp}.xlsx");

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Audit Logs");

                ws.Cell("A1").Value = "Timestamp";
                ws.Cell("B1").Value = "Username";
                ws.Cell("C1").Value = "Role";
                ws.Cell("D1").Value = "Action";

                int rowIndex = 2;
                foreach (var log in rows)
                {
                    ws.Cell(rowIndex, 1).Value = log.Timestamp.ToString("yyyy-MM-dd HH:mm");
                    ws.Cell(rowIndex, 2).Value = log.Username;
                    ws.Cell(rowIndex, 3).Value = log.Role;
                    ws.Cell(rowIndex, 4).Value = log.Action;
                    rowIndex++;
                }

                using var stream = File.Create(path);
                wb.SaveAs(stream);

                await DisplayAlert("Export", $"Excel saved to\n{path}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Excel export failed:\n{ex.Message}", "OK");
            }
        }

        private async void OnRefreshRequested(object sender, EventArgs e)
        {
            await LoadAuditLogs(true);
            if (refreshView.IsRefreshing)
                refreshView.IsRefreshing = false;
        }

        private async Task ShowRetry(string title, string message)
        {
            bool retry = await DisplayAlert(title, message, "Retry", "Cancel");
            if (retry)
                _ = LoadAuditLogs(true);
        }

        private async Task ShowLoadingAsync(bool show)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                loadingOverlay.IsVisible = show;
            });
        }

        // Firebase audit logs change event handler
        private async void OnFirebaseAuditLogsChanged(object sender, EventArgs e)
        {
            await LoadAuditLogs();
        }
    }
}