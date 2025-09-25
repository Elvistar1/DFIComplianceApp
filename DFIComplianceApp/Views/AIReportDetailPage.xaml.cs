using CommunityToolkit.Maui.Alerts;
using DFIComplianceApp.Models;
using DFIComplianceApp.ViewModels;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DFIComplianceApp.Services;

namespace DFIComplianceApp.Views;

public partial class AIReportDetailPage : ContentPage, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private string _premises = "", _nature = "";
    private List<ChecklistItemView> _checklist = new();
    private Guid _reportId;
    private ReportVM _vm;

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

    private bool _hasChecklist;
    public bool HasChecklist
    {
        get => _hasChecklist;
        set
        {
            if (_hasChecklist != value)
            {
                _hasChecklist = value;
                OnPropertyChanged(nameof(HasChecklist));
            }
        }
    }

    public AIReportDetailPage(string premises,
                              string nature,
                              string status,
                              string advice,
                              string json)
    {
        InitializeComponent();

        _premises = premises ?? "";
        _nature = nature ?? "";

        PremisesLabel.Text = _premises;
        NatureLabel.Text = _nature;
        StatusLabel.Text = $"Status: {status ?? ""}";
        AdviceEditor.Text = string.IsNullOrWhiteSpace(advice)
                            ? "(No analysis available yet.)"
                            : advice;

        LoadChecklist(json);
    }

    public AIReportDetailPage(Guid reportId)
    {
        InitializeComponent();
        _reportId = reportId;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        App.Firebase.PendingReportsChanged += OnPendingReportsChanged;
        await LoadReportAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        App.Firebase.PendingReportsChanged -= OnPendingReportsChanged;
    }

    private async void OnPendingReportsChanged(object sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await LoadReportAsync();
        });
    }

    private async Task LoadReportAsync()
    {
        IsLoading = true;
        try
        {
            var pendingReport = await App.Firebase.GetPendingReportByIdAsync(_reportId);
            if (pendingReport == null)
            {
                await DisplayAlert("Not Found", "No report found for this report Id.", "OK");
                return;
            }

            _vm = new ReportVM(pendingReport);
            BindingContext = this;

            PremisesLabel.Text = _vm.Premises ?? "(No premises)";
            NatureLabel.Text = _vm.NatureOfWork ?? "(No nature)";
            StatusLabel.Text = $"Status: {_vm.Status ?? "(No status)"}";
            AdviceEditor.Text = ShouldShowAdvice(_vm) ? _vm.Advice : "(Not available until approved by director)";

            if (!string.IsNullOrWhiteSpace(_vm.Json))
            {
                LoadChecklist(_vm.Json);
            }
            else
            {
                await LoadChecklistFromFirebaseAsync(_vm.InspectionId);
            }

            OnPropertyChanged(nameof(SentStatusText)); // Ensure UI updates after loading
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadChecklistFromFirebaseAsync(Guid inspectionId)
    {
        var answers = await App.Firebase.GetAnswersForInspectionAsync(inspectionId);

        _checklist.Clear();

        foreach (var answer in answers)
        {
            var photos = await App.Firebase.GetPhotosForAnswerAsync(answer.Id);

            _checklist.Add(new ChecklistItemView
            {
                Question = answer.QuestionText,
                Compliant = answer.IsCompliant ? "Yes" : "No",
                Notes = string.IsNullOrWhiteSpace(answer.Notes) ? null : answer.Notes,
                PhotoPaths = photos.Select(p => !string.IsNullOrWhiteSpace(p.DownloadUrl) ? p.DownloadUrl : p.LocalPath).ToList()
            });
        }

        ChecklistView.ItemsSource = _checklist;
        HasChecklist = _checklist.Count > 0;
    }

    private void LoadChecklist(string json)
    {
        try
        {
            var report = JsonSerializer.Deserialize<AiReportJson>(json);
            if (report?.Answers == null)
            {
                _checklist.Clear();
                HasChecklist = false;
                return;
            }

            _checklist.Clear();

            foreach (var answer in report.Answers)
            {
                _checklist.Add(new ChecklistItemView
                {
                    Question = answer.Question ?? "",
                    Compliant = answer.Compliant ? "Yes" : "No",
                    Notes = string.IsNullOrWhiteSpace(answer.Notes) ? null : answer.Notes,
                    PhotoPaths = answer.PhotoPaths ?? new List<string>()
                });
            }

            ChecklistView.ItemsSource = _checklist;
            HasChecklist = _checklist.Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AIReportDetailPage] Failed to load checklist: {ex.Message}");
            _checklist.Clear();
            HasChecklist = false;
        }
    }

    private async void OnCopyClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AdviceEditor.Text)) return;

        await Clipboard.SetTextAsync(AdviceEditor.Text);
        await Toast.Make("Advice copied to clipboard").Show();
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AdviceEditor.Text)) return;

        await Share.RequestAsync(new ShareTextRequest
        {
            Text = AdviceEditor.Text,
            Title = "Share AI Advice"
        });
    }

    private async void OnExportPdfClicked(object sender, EventArgs e)
    {
        try
        {
            string filename = $"AIReport_{_premises}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            string filePath = Path.Combine(FileSystem.CacheDirectory, filename);

            using var doc = new PdfDocument();
            var page = doc.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var fontTitle = new XFont("Arial", 14, XFontStyle.Bold);
            var fontNormal = new XFont("Arial", 11);
            double y = 40;

            gfx.DrawString("DFI Compliance Inspection Report", fontTitle, XBrushes.Black, new XPoint(40, y));
            y += 30;

            gfx.DrawString($"Premises: {_premises}", fontNormal, XBrushes.Black, new XPoint(40, y)); y += 20;
            gfx.DrawString($"Nature of Work: {_nature}", fontNormal, XBrushes.Black, new XPoint(40, y)); y += 20;
            gfx.DrawString("Advice Summary:", fontNormal, XBrushes.Black, new XPoint(40, y)); y += 20;
            foreach (var line in BreakLines(AdviceEditor.Text, 90))
            {
                gfx.DrawString(line, fontNormal, XBrushes.Black, new XPoint(60, y));
                y += 18;
            }

            if (_checklist.Count > 0)
            {
                y += 30;
                gfx.DrawString("Checklist Review:", fontTitle, XBrushes.Black, new XPoint(40, y)); y += 25;

                foreach (var item in _checklist)
                {
                    if (y > page.Height - 60)
                    {
                        page = doc.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        y = 40;
                    }

                    gfx.DrawString($"• {item.Question}", fontNormal, XBrushes.Black, new XPoint(50, y)); y += 18;
                    gfx.DrawString($"   Compliant: {item.Compliant}", fontNormal, XBrushes.Black, new XPoint(60, y)); y += 16;

                    if (!string.IsNullOrWhiteSpace(item.Notes))
                    {
                        var noteLines = BreakLines(item.Notes, 85);
                        foreach (var note in noteLines)
                        {
                            gfx.DrawString($"   Note: {note}", fontNormal, XBrushes.Black, new XPoint(60, y));
                            y += 16;
                        }
                    }
                    y += 10;
                }
            }

            using var stream = File.Create(filePath);
            doc.Save(stream);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Exported AI Report",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to export PDF: {ex.Message}", "OK");
        }
    }

    private List<string> BreakLines(string text, int maxChars)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        string current = "";

        foreach (var word in words)
        {
            if ((current + word).Length > maxChars)
            {
                lines.Add(current.Trim());
                current = "";
            }
            current += word + " ";
        }

        if (!string.IsNullOrWhiteSpace(current))
            lines.Add(current.Trim());

        return lines;
    }

    private async void OnPrintClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Print", "Printing feature coming soon or handled by platform-specific code.", "OK");
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync();
        else if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    private void OnEditorCompleted(object sender, EventArgs e) => AdviceEditor?.Unfocus();

    private bool ShouldShowAdvice(ReportVM vm)
    {
        if (App.CurrentUser?.Role == "Director")
            return true;
        if (App.CurrentUser?.Role == "Inspector" && vm.Status == "Approved")
            return true;
        return false;
    }

    private async void OnSendRecommendationClicked(object sender, EventArgs e)
    {
        IsLoading = true; // Show loading indicator
        try
        {
            var companies = await App.Firebase.GetCompaniesSafeAsync();
            var company = companies.FirstOrDefault(c => c.Name == _vm.Premises || c.Id == _vm.CompanyId);
            var companyEmail = company?.Email ?? "";
            var companyName = company?.Name ?? _vm.Premises;

            if (string.IsNullOrWhiteSpace(companyEmail))
            {
                await DisplayAlert("No Email", "No company email found for this premises.", "OK");
                return;
            }

            bool confirm = await DisplayAlert(
                "Send Recommendation",
                $"Are you sure you want to send the advice to:\n\n{companyName}\n{companyEmail}\n\nMessage will include the current advice.",
                "Send", "Cancel"
            );

            if (!confirm)
                return;

            var emailService = App.Services.GetRequiredService<IEmailService>();
            string subject = $"Compliance Recommendation for {companyName}";
            string body = $"Dear Manager,\n\nPlease find below the compliance advice for your premises:\n\n{AdviceEditor.Text}\n\nRegards,\nDepartment of Factories Inspectorate";

            await emailService.SendAsync(companyEmail, subject, body, false);

            _vm.RecommendationSentAt = DateTime.UtcNow;
            _vm.RecommendationSentBy = App.CurrentUser?.Username ?? "Unknown";

            var row = _vm.ToRow();
            row.RecommendationSentAt = _vm.RecommendationSentAt;
            row.RecommendationSentBy = _vm.RecommendationSentBy;
            await App.Firebase.UpdatePendingReportAsync(row);

            OnPropertyChanged(nameof(SentStatusText));
            await DisplayAlert("Sent", $"Recommendation sent to {companyEmail}.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to send email: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false; // Hide loading indicator
        }
    }

    public string SentStatusText =>
        _vm != null && _vm.RecommendationSentAt.HasValue
            ? $"Recommendation sent by {_vm.RecommendationSentBy} on {_vm.RecommendationSentAt.Value:dd MMM yyyy, HH:mm}"
            : "Recommendation not yet sent.";
}
