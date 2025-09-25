using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using DFIComplianceApp.Models;
using Microsoft.Maui.Controls;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using ClosedXML.Excel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DFIComplianceApp.Views
{
    public partial class CompanyRenewalHistoryPage : ContentPage
    {
        readonly Company _company;
        readonly ObservableCollection<CompanyRenewal> _master = new();
        readonly ObservableCollection<CompanyRenewal> _view = new();

        /* toolbar commands */
        public Command ExportPdfCommand { get; }
        public Command ExportExcelCommand { get; }

        public CompanyRenewalHistoryPage(Company company)
        {
            InitializeComponent();
            BindingContext = this;

            _company = company;

            ExportPdfCommand = new(async () => await GuardAsync(ExportPdfAsync));
            ExportExcelCommand = new(async () => await GuardAsync(ExportExcelAsync));

            RenewalsCollectionView.ItemsSource = _view;

            _ = LoadAsync();
        }

        /* ─────────────────────────────  load + filter  ───────────────────────────── */

        async Task LoadAsync()
        {
            try
            {
                ShowLoadingState(true);

                // 1. Try Firebase as source of truth
                var renewals = await App.Firebase.GetCompanyRenewalsAsync(_company.Id.ToString());


                _master.Clear();
                foreach (var r in renewals.OrderByDescending(r => r.RenewalYear))
                    _master.Add(r);

                BuildYearChips();
                ApplyFilter();

                HideErrorState();
            }
            catch (Exception ex)
            {
                // Firebase failed → show retry screen
                ShowErrorState($"⚠️ Could not load renewal history.\n{ex.Message}");
            }
            finally
            {
                ShowLoadingState(false);
            }
        }

        void ShowErrorState(string message)
        {
            ErrorMessageLabel.Text = message;
            ErrorStateView.IsVisible = true;
            MainContent.IsVisible = false;
        }

        void HideErrorState()
        {
            ErrorStateView.IsVisible = false;
            MainContent.IsVisible = true; // use MainContent instead of MainContentView
        }


        void ShowLoadingState(bool isLoading)
{
    LoadingIndicator.IsRunning = isLoading;
    LoadingIndicator.IsVisible = isLoading;
}
        void OnRetryClicked(object sender, EventArgs e) => _ = LoadAsync();


        void BuildYearChips()
        {
            FilterChipStack.Children.Clear();

            /* “All” chip */
            var allBtn = MakeChip("All", null);
            FilterChipStack.Children.Add(allBtn);

            /* distinct years */
            foreach (int y in _master.Select(r => r.RenewalYear).Distinct().OrderByDescending(y => y))
                FilterChipStack.Children.Add(MakeChip(y.ToString(), y));
        }

        Button MakeChip(string text, int? year)
        {
            var btn = new Button
            {
                Text = text,
                CommandParameter = year,               // ← store the year here
                CornerRadius = 14,
                BackgroundColor = Color.FromArgb("#e0e0e0"),
                Padding = new Thickness(10, 2),
                HeightRequest = 30,
                FontSize = 13
            };

            btn.Clicked += OnFilterChipClicked;
            return btn;
        }

        int? _selectedYear;
        void ApplyFilter()
        {
            string q = SearchBar.Text?.Trim().ToLower() ?? "";

            var res = _master
                .Where(r => (_selectedYear == null || r.RenewalYear == _selectedYear)
                         && (r.RenewedBy?.ToLower().Contains(q) ?? false));

            _view.Clear();
            foreach (var r in res)
                _view.Add(r);
        }

        void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        /* NEW: Filter chip click handler */
        void OnFilterChipClicked(object? sender, EventArgs e)
        {
            if (sender is not Button btn)
                return;

            _selectedYear = btn.CommandParameter as int?;

            // Visual highlight: green for selected, grey otherwise
            foreach (var chip in FilterChipStack.Children.OfType<Button>())
                chip.BackgroundColor = chip == btn ? Color.FromArgb("#c8e6c9") : Color.FromArgb("#e0e0e0");

            ApplyFilter();
        }

        /* ─────────────────────────────  exports  ───────────────────────────── */

        async Task ExportPdfAsync()
        {
            string path = Path.Combine(FileSystem.CacheDirectory,
                                       $"{_company.Name}_Renewals.pdf");

            var pdf = new PdfDocument();
            var page = pdf.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var fontTitle = new XFont("Arial", 16, XFontStyle.Bold);
            var font = new XFont("Arial", 12);

            double margin = 40, y = margin;

            gfx.DrawString($"{_company.Name} – Renewal History", fontTitle,
                XBrushes.Black, new XRect(margin, y, page.Width, 30));

            y += 40;

            foreach (var r in _view)
            {
                string line = $"{r.RenewalYear}   {r.RenewalDate:dd MMM yyyy}   by {r.RenewedBy}";
                gfx.DrawString(line, font, XBrushes.Black, new XRect(margin, y, page.Width - margin, 20));
                y += 20;
                if (y > page.Height - margin)
                {
                    page = pdf.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }
            }

            using var fs = File.Create(path);
            pdf.Save(fs);

            await Toast.Make($"PDF saved:\n{path}", ToastDuration.Short).Show();
        }

        async Task ExportExcelAsync()
        {
            string path = Path.Combine(FileSystem.CacheDirectory,
                                       $"{_company.Name}_Renewals.xlsx");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Renewals");

            ws.Cell("A1").Value = "Year";
            ws.Cell("B1").Value = "Renewal date";
            ws.Cell("C1").Value = "Renewed by";

            int row = 2;
            foreach (var r in _view)
            {
                ws.Cell(row, 1).Value = r.RenewalYear;
                ws.Cell(row, 2).Value = r.RenewalDate.ToString("dd MMM yyyy");
                ws.Cell(row, 3).Value = r.RenewedBy;
                row++;
            }

            using var fs = File.Create(path);
            wb.SaveAs(fs);

            await Toast.Make($"Excel saved:\n{path}", ToastDuration.Short).Show();
        }

        /* ─────────────────────────────  util  ───────────────────────────── */

        static async Task GuardAsync(Func<Task> work)
        {
            try { await work(); }
            catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK"); }
        }
    }
}
