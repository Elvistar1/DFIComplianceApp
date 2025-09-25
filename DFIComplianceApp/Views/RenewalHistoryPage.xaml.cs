using DFIComplianceApp.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DFIComplianceApp.Views
{
    public partial class RenewalHistoryPage : ContentPage
    {
        readonly ObservableCollection<CompanyWithRenewalInfo> _master = new();
        readonly ObservableCollection<CompanyWithRenewalInfo> _view = new();

        public RenewalHistoryPage()
        {
            InitializeComponent();
            BindingContext = _view;
            _ = LoadAsync();
        }

        async Task LoadAsync()
        {
            var companies = await App.Firebase.GetCompaniesSafeAsync();
            var allRenewals = await App.Firebase.GetAllCompanyRenewalsAsync();
            var grouped = new List<CompanyWithRenewalInfo>();

            foreach (var company in companies)
            {
                var renewals = allRenewals.GetValueOrDefault(company.Id.ToString());
                if (renewals == null || renewals.Count == 0)
                    continue;

                var latestRenewal = renewals.OrderByDescending(x => x.RenewalDate).First();
                grouped.Add(new CompanyWithRenewalInfo
                {
                    Id = company.Id,
                    Name = company.Name,
                    Location = company.Location,
                    LatestRenewalDate = latestRenewal.RenewalDate,
                    RenewedBy = latestRenewal.RenewedBy
                });
            }

            _master.Clear();
            foreach (var c in grouped)
                _master.Add(c);

            PopulateYearPicker();
            ApplyFilter();
        }

        void PopulateYearPicker()
        {
            YearPicker.Items.Clear();
            YearPicker.Items.Add("All Years");

            var years = _master
                .Select(c => c.LatestRenewalDate.Year)
                .Distinct()
                .OrderByDescending(y => y);

            foreach (var year in years)
                YearPicker.Items.Add(year.ToString());

            YearPicker.SelectedIndex = 0; // Default
        }

        void ApplyFilter()
        {
            string q = SearchBar.Text?.Trim().ToLower() ?? "";
            string selectedYear = YearPicker.SelectedIndex > 0 ? YearPicker.Items[YearPicker.SelectedIndex] : null;

            var filtered = _master.Where(c =>
                (c.Name.ToLower().Contains(q) ||
                 (c.RenewedBy?.ToLower().Contains(q) ?? false)) &&
                (selectedYear == null || c.LatestRenewalDate.Year.ToString() == selectedYear)
            );

            var sorted = SortList(filtered.ToList());

            _view.Clear();
            foreach (var c in sorted)
                _view.Add(c);
        }

        void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        void OnSortChanged(object sender, EventArgs e) => ApplyFilter();
        void OnYearChanged(object sender, EventArgs e) => ApplyFilter();

        private System.Collections.Generic.List<CompanyWithRenewalInfo> SortList(System.Collections.Generic.List<CompanyWithRenewalInfo> list)
        {
            return SortPicker.SelectedIndex switch
            {
                0 => list.OrderBy(c => c.Name).ToList(),
                1 => list.OrderByDescending(c => c.Name).ToList(),
                2 => list.OrderByDescending(c => c.LatestRenewalDate).ToList(),
                3 => list.OrderBy(c => c.LatestRenewalDate).ToList(),
                _ => list.OrderBy(c => c.Name).ToList(),
            };
        }

        // ✅ Export to Excel
        private async void OnExportExcelClicked(object sender, EventArgs e)
        {
            var filePath = Path.Combine(FileSystem.CacheDirectory, $"RenewalHistory_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Renewal History");

                ws.Cell(1, 1).Value = "Company Name";
                ws.Cell(1, 2).Value = "Location";
                ws.Cell(1, 3).Value = "Latest Renewal Date";
                ws.Cell(1, 4).Value = "Renewed By";

                int row = 2;
                foreach (var c in _view)
                {
                    ws.Cell(row, 1).Value = c.Name;
                    ws.Cell(row, 2).Value = c.Location;
                    ws.Cell(row, 3).Value = c.LatestRenewalDate.ToString("dd MMM yyyy");
                    ws.Cell(row, 4).Value = c.RenewedBy;
                    row++;
                }

                ws.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share Excel File",
                File = new ShareFile(filePath)
            });
        }

        // ✅ Export to Word
        private async void OnExportWordClicked(object sender, EventArgs e)
        {
            var filePath = Path.Combine(FileSystem.CacheDirectory, $"RenewalHistory_{DateTime.Now:yyyyMMdd_HHmm}.docx");

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = new Body();

                // Title
                Paragraph titlePara = new Paragraph(
                    new Run(
                        new Text("Renewal History")
                    )
                );
                titlePara.ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center }
                );
                body.Append(titlePara);

                // Table
                Table table = new Table();

                // Table properties
                TableProperties tblProps = new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 },
                        new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 },
                        new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 },
                        new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 },
                        new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 },
                        new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 }
                    )
                );
                table.AppendChild(tblProps);

                // Header row
                TableRow headerRow = new TableRow();
                headerRow.Append(
                    CreateCell("Company Name", true),
                    CreateCell("Location", true),
                    CreateCell("Latest Renewal Date", true),
                    CreateCell("Renewed By", true)
                );
                table.Append(headerRow);

                // Data rows
                foreach (var c in _view)
                {
                    TableRow row = new TableRow();
                    row.Append(
                        CreateCell(c.Name),
                        CreateCell(c.Location),
                        CreateCell(c.LatestRenewalDate.ToString("dd MMM yyyy")),
                        CreateCell(c.RenewedBy)
                    );
                    table.Append(row);
                }

                body.Append(table);
                mainPart.Document.Append(body);
                mainPart.Document.Save();
            }

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share Word File",
                File = new ShareFile(filePath)
            });
        }

        private TableCell CreateCell(string text, bool bold = false)
        {
            Run run = new Run(new Text(text));
            if (bold) run.RunProperties = new RunProperties(new Bold());

            Paragraph para = new Paragraph(run);
            return new TableCell(para);
        }

        public class CompanyWithRenewalInfo
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public string Location { get; set; } = "";
            public DateTime LatestRenewalDate { get; set; }
            public string RenewedBy { get; set; } = "";
        }
    }
}
