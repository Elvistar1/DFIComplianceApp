using DFIComplianceApp.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Maui.Storage;
using ClosedXML.Excel;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace DFIComplianceApp.Views
{
    public partial class InspectionHistoryPage : ContentPage
    {
        private readonly List<Inspection> _all = new();

        public InspectionHistoryPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            InspectorPickerLayout.IsVisible = App.CurrentUser?.Role != "Inspector";
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                RefreshView.IsRefreshing = true;

                // ✅ Fetch all inspections from Firebase
                var fetched = await App.Firebase.GetInspectionsAsync(); // <-- Make sure this exists

                _all.Clear();
                _all.AddRange(fetched.GroupBy(x => x.Id).Select(g => g.First()));

                BuildFilterPickers();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load inspections: {ex.Message}", "OK");
            }
            finally
            {
                RefreshView.IsRefreshing = false;
            }
        }


        private void BuildFilterPickers()
        {
            var years = _all.Select(i => i.PlannedDate.Year).Distinct().OrderByDescending(y => y).ToList();
            YearPicker.ItemsSource = new List<string> { "All" }.Concat(years.Select(y => y.ToString())).ToList();
            YearPicker.SelectedIndex = 0;

            var companies = _all.Select(i => i.CompanyName).Distinct().OrderBy(c => c).ToList();
            CompanyPicker.ItemsSource = new List<string> { "All" }.Concat(companies).ToList();
            CompanyPicker.SelectedIndex = 0;

            var inspectors = _all.Select(i => i.InspectorName).Distinct().OrderBy(i => i).ToList();
            InspectorPicker.ItemsSource = new List<string> { "All" }.Concat(inspectors).ToList();
            InspectorPicker.SelectedIndex = 0;
        }

        private void OnFilterChanged(object sender, EventArgs e) => ApplyFilters();
        private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

        private void ApplyFilters()
        {
            string search = (SearchBar.Text ?? "").Trim().ToLower();
            string year = YearPicker.SelectedItem as string ?? "All";
            string company = CompanyPicker.SelectedItem as string ?? "All";
            string inspector = InspectorPicker.SelectedItem as string ?? "All";

            IEnumerable<Inspection> filtered = _all;

            if (App.CurrentUser?.Role == "Inspector")
            {
                filtered = filtered.Where(i => i.InspectorName == App.CurrentUser.Username);
            }
            else
            {
                if (inspector != "All")
                    filtered = filtered.Where(i => i.InspectorName == inspector);
            }

            if (!string.IsNullOrWhiteSpace(search))
                filtered = filtered.Where(i => (i.CompanyName ?? "").ToLower().Contains(search));

            if (year != "All" && int.TryParse(year, out int y))
                filtered = filtered.Where(i => i.PlannedDate.Year == y);

            if (company != "All")
                filtered = filtered.Where(i => i.CompanyName == company);

            InspectionCollection.ItemsSource = filtered.OrderByDescending(i => i.PlannedDate).ToList();
        }


        private async void OnRowTapped(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is Inspection inspection)
            {
                InspectionCollection.SelectedItem = null;
                await Navigation.PushAsync(new InspectionDetailPage(inspection.Id));

            }
        }





        private async void OnExportPdf(object sender, EventArgs e)
        {
            var rows = (InspectionCollection.ItemsSource as IEnumerable<Inspection>)?.ToList() ?? new();
            if (!rows.Any())
            {
                await DisplayAlert("Export", "Nothing to export.", "OK");
                return;
            }

            string path = Path.Combine(FileSystem.CacheDirectory, $"Inspections_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            var pdf = new PdfDocument();
            var page = pdf.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Arial", 10);

            const double margin = 30, lineHeight = 16;
            double y = margin;

            foreach (var row in rows)
            {
                string line = $"{row.PlannedDate:yyyy-MM-dd}  {row.CompanyName,-25} {row.InspectorName}";
                gfx.DrawString(line, font, XBrushes.Black, new XPoint(margin, y));
                y += lineHeight;

                if (y > page.Height - margin)
                {
                    page = pdf.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }
            }

            using var fs = File.Create(path);
            pdf.Save(fs);

            await DisplayAlert("Export", $"PDF saved to\n{path}", "OK");
        }

        private async void OnExportExcel(object sender, EventArgs e)
        {
            var rows = (InspectionCollection.ItemsSource as IEnumerable<Inspection>)?.ToList() ?? new();
            if (!rows.Any())
            {
                await DisplayAlert("Export", "Nothing to export.", "OK");
                return;
            }

            string path = Path.Combine(FileSystem.CacheDirectory, $"Inspections_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Inspections");

            ws.Cell("A1").Value = "Planned";
            ws.Cell("B1").Value = "Completed";
            ws.Cell("C1").Value = "Company";
            ws.Cell("D1").Value = "Inspector";

            int row = 2;
            foreach (var i in rows)
            {
                ws.Cell(row, 1).Value = i.PlannedDate;
                ws.Cell(row, 2).Value = i.CompletedDate?.ToString("yyyy-MM-dd") ?? "-";
                ws.Cell(row, 3).Value = i.CompanyName;
                ws.Cell(row, 4).Value = i.InspectorName;
                row++;
            }

            using var fs = File.Create(path);
            wb.SaveAs(fs);

            await DisplayAlert("Export", $"Excel saved to\n{path}", "OK");
        }

        private void OnRefresh(object sender, EventArgs e) => _ = LoadAsync();
    }
}
