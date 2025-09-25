using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DFIComplianceApp.Views
{
    public partial class AdministratorDashboardPage : ContentPage
    {
        private readonly FirebaseAuthService _firebaseService;
        private readonly string _username;

        public ISeries[] BarSeries { get; set; }
        public string[] BarLabels { get; set; }
        public ISeries[] PieSeries { get; set; }

        public AdministratorDashboardPage(string username)
        {
            InitializeComponent();

            _username = username;
            _firebaseService = App.Services.GetRequiredService<FirebaseAuthService>();

            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDashboardStatisticsAsync();
            await CheckAdminNotificationsAsync();
        }

        private async Task LoadDashboardStatisticsAsync()
        {
            try
            {
                var users = await _firebaseService.GetAllUsersAsync();

                int totalUsers = users.Count;
                int activeUsers = users.Count(u => u.IsActive);
                int adminCount = users.Count(u => u.Role == "Administrator");

                BarLabels = new[] { "Total Users", "Active", "Admins" };

                BarSeries = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Values = new List<int> { totalUsers, activeUsers, adminCount },
                        Fill = new SolidColorPaint(SKColors.CornflowerBlue)
                    }
                };

                PieSeries = new ISeries[]
                {
                    new PieSeries<int> { Values = new[] { totalUsers }, Name = "Total", Fill = new SolidColorPaint(SKColors.SkyBlue) },
                    new PieSeries<int> { Values = new[] { activeUsers }, Name = "Active", Fill = new SolidColorPaint(SKColors.LightGreen) },
                    new PieSeries<int> { Values = new[] { adminCount }, Name = "Admins", Fill = new SolidColorPaint(SKColors.Orange) }
                };

                var axisLabelPaint = new SolidColorPaint(SKColors.DarkSlateGray);

                BarChart.XAxes = new[]
                {
                    new LiveChartsCore.SkiaSharpView.Axis
                    {
                        Labels = BarLabels,
                        LabelsRotation = 15,
                        TextSize = 14,
                        LabelsPaint = axisLabelPaint
                    }
                };

                OnPropertyChanged(nameof(BarSeries));
                OnPropertyChanged(nameof(BarLabels));
                OnPropertyChanged(nameof(PieSeries));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load statistics from Firebase: {ex.Message}", "OK");
            }
        }

        // 🔔 ADMIN NOTIFICATIONS
        private async Task CheckAdminNotificationsAsync()
        {
            try
            {
                var users = await _firebaseService.GetAllUsersAsync();

                // --- Seen keys ---
                string newUsersKey = "SeenNewUsers";
                string roleChangesKey = "SeenRoleChanges";

                var seenNewUsers = new HashSet<string>(
                    System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                        Preferences.Get(newUsersKey, "[]")) ?? new List<string>()
                );

                var seenRoleChanges = new HashSet<string>(
                    System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                        Preferences.Get(roleChangesKey, "[]")) ?? new List<string>()
                );

                // --- Detect new users ---
                var newUsers = users
                    .Where(u => !seenNewUsers.Contains(u.DFIUserId))
                    .ToList();

                foreach (var u in newUsers)
                {
                    var toast = Toast.Make(
                        $"New User Created: {u.Username} ({u.Role})",
                        ToastDuration.Short,
                        14);
                    await toast.Show();

                    seenNewUsers.Add(u.DFIUserId);
                }

                // --- Detect role changes (e.g. someone promoted to Admin) ---
                // Simplified: if role == Administrator and not seen before
                var promoted = users
                    .Where(u => u.Role == "Administrator" && !seenRoleChanges.Contains(u.DFIUserId))
                    .ToList();

                foreach (var u in promoted)
                {
                    var toast = Toast.Make(
                        $"Role Change: {u.Username} is now an Administrator",
                        ToastDuration.Short,
                        14);
                    await toast.Show();

                    seenRoleChanges.Add(u.DFIUserId);
                }

                // Save back
                Preferences.Set(newUsersKey, System.Text.Json.JsonSerializer.Serialize(seenNewUsers.ToList()));
                Preferences.Set(roleChangesKey, System.Text.Json.JsonSerializer.Serialize(seenRoleChanges.ToList()));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Notification Error", $"Could not load notifications: {ex.Message}", "OK");
            }
        }

        // === Existing button handlers ===
        private async void OnCreateUserClicked(object sender, EventArgs e)
        {
            var firebaseAuth = App.Services.GetRequiredService<FirebaseAuthService>();
            await Navigation.PushAsync(new CreateUserPage("Administrator", firebaseAuth));
        }

        private async void OnManageUsersClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new ManageUsersPage());
        }

        private async void OnActivityLogClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AuditLogPage());
        }

        private async void OnSystemSettingsClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SystemSettingsPage());
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            if (await DisplayAlert("Log out", "Are you sure you want to log out?", "Yes", "Cancel"))
            {
                SecureStorage.RemoveAll();
                App.CurrentUser = null;
                await Navigation.PopToRootAsync();
            }
        }

        private async void OnExportToPdfClicked(object sender, EventArgs e)
        {
            await ExportAdminReportToPdfAsync();
        }

        private async void OnAIReportOversightClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AIReportOversightPage());
        }

        private async void OnExportToExcelClicked(object sender, EventArgs e)
        {
            await ExportAdminReportToExcelAsync();
        }

        // === Existing Export methods (unchanged) ===
        private async Task ExportAdminReportToPdfAsync()
        {
            try
            {
                var users = await _firebaseService.GetAllUsersAsync();
                int totalUsers = users.Count;
                int activeUsers = users.Count(u => u.IsActive);
                int adminCount = users.Count(u => u.Role == "Administrator");

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(FileSystem.CacheDirectory, $"AdminDashboardReport_{timestamp}.pdf");

                var pdf = new PdfDocument();
                var fontTitle = new XFont("Arial", 16, XFontStyle.Bold);
                var font = new XFont("Arial", 12);
                double margin = 40;
                double y = margin;

                var page = pdf.AddPage();
                var gfx = XGraphics.FromPdfPage(page);

                gfx.DrawString("Administrator Dashboard Report", fontTitle, XBrushes.Black, new XPoint(margin, y));
                y += 30;
                gfx.DrawString($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm}", font, XBrushes.Black, new XPoint(margin, y));
                y += 30;

                gfx.DrawString("User Statistics:", font, XBrushes.Black, new XPoint(margin, y));
                y += 25;

                gfx.DrawString($"- Total Users: {totalUsers}", font, XBrushes.Black, new XPoint(margin + 20, y)); y += 20;
                gfx.DrawString($"- Active Users: {activeUsers}", font, XBrushes.Black, new XPoint(margin + 20, y)); y += 20;
                gfx.DrawString($"- Administrators: {adminCount}", font, XBrushes.Black, new XPoint(margin + 20, y)); y += 20;

                using var ms = new MemoryStream();
                pdf.Save(ms);
                pdf.Dispose();

                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                await fs.WriteAsync(ms.ToArray());

                await DisplayAlert("Exported", $"PDF report saved at:\n{filePath}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Error", $"Failed to export PDF: {ex.Message}", "OK");
            }
        }

        private async Task ExportAdminReportToExcelAsync()
        {
            try
            {
                var users = await _firebaseService.GetAllUsersAsync();
                int totalUsers = users.Count;
                int activeUsers = users.Count(u => u.IsActive);
                int adminCount = users.Count(u => u.Role == "Administrator");

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(FileSystem.CacheDirectory, $"AdminDashboardReport_{timestamp}.xlsx");

                using var wb = new ClosedXML.Excel.XLWorkbook();
                var ws = wb.Worksheets.Add("AdminDashboard");

                ws.Cell("A1").Value = "Administrator Dashboard Report";
                ws.Cell("A2").Value = $"Generated on {DateTime.Now:dd MMM yyyy HH:mm}";
                ws.Cell("A4").Value = "Feature";
                ws.Cell("B4").Value = "Count";

                ws.Cell("A5").Value = "Total Users"; ws.Cell("B5").Value = totalUsers;
                ws.Cell("A6").Value = "Active Users"; ws.Cell("B6").Value = activeUsers;
                ws.Cell("A7").Value = "Administrators"; ws.Cell("B7").Value = adminCount;

                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                wb.SaveAs(fs);

                await DisplayAlert("Exported", $"Excel report saved at:\n{filePath}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Error", $"Failed to export Excel: {ex.Message}", "OK");
            }
        }
    }
}
