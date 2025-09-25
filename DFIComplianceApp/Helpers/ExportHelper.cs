// File: Helpers/ExportHelper.cs
using ClosedXML.Excel;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DFIComplianceApp.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Text;

namespace DFIComplianceApp.Helpers
{
    /// <summary>
    /// Centralised PDF / Excel export helpers for Users, Inspections, and Risk Predictions.
    /// </summary>
    internal static class ExportHelper
    {
        /* ──────────────── USERS ──────────────── */

        public static async Task ExportUsersToPdfAsync(IEnumerable<User> users, string title = "Users")
        {
            var pdf = new PdfDocument();
            var fontHead = new XFont("Courier New", 14, XFontStyle.Bold);
            var fontBody = new XFont("Courier New", 12);
            double margin = 40, lineH = 20, usableY = 0, y;

            XGraphics StartPage()
            {
                var p = pdf.AddPage();
                usableY = p.Height - margin;
                y = margin;
                return XGraphics.FromPdfPage(p);
            }

            XGraphics gfx = StartPage();
            gfx.DrawString($"User List — {DateTime.Now:dd MMM yyyy}", fontHead, XBrushes.Black, new XPoint(margin, y));
            y += lineH + 10;

            const string fmt = "{0,-12}  {1,-18}  {2}";
            string header = string.Format(fmt, "Role", "Username", "Full Name");

            void DrawHeader()
            {
                gfx.DrawString(header, fontHead, XBrushes.Black, new XPoint(margin, y));
                y += lineH;
                gfx.DrawLine(XPens.Black, margin, y, gfx.PageSize.Width - margin, y);
                y += 10;
            }
            DrawHeader();

            foreach (var u in users)
            {
                if (y + lineH > usableY)
                {
                    gfx.Dispose();
                    gfx = StartPage();
                    DrawHeader();
                }

                string line = string.Format(fmt, u.Role, u.Username, u.FullName);
                gfx.DrawString(line, fontBody, XBrushes.Black, new XPoint(margin, y));
                y += lineH;
            }

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                pdf.Save(ms);
                bytes = ms.ToArray();
            }
            pdf.Dispose();

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string fileName = $"{title}_{stamp}.pdf";
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);

            await File.WriteAllBytesAsync(path, bytes);
            await Application.Current.MainPage.DisplayAlert("Exported", $"PDF saved:\n{path}", "OK");
        }

        public static async Task ExportUsersToExcelAsync(IEnumerable<User> users, string title = "Users")
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string fileName = $"{title}_{stamp}.xlsx";
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Users");

            ws.Cell("A1").Value = "Role";
            ws.Cell("B1").Value = "Username";
            ws.Cell("C1").Value = "Full Name";
            ws.Cell("D1").Value = "Email";
            ws.Cell("E1").Value = "Active";
            ws.Row(1).Style.Font.Bold = true;

            int row = 2;
            foreach (var u in users)
            {
                ws.Cell(row, 1).Value = u.Role;
                ws.Cell(row, 2).Value = u.Username;
                ws.Cell(row, 3).Value = u.FullName;
                ws.Cell(row, 4).Value = u.Email;
                ws.Cell(row, 5).Value = u.IsActive ? "Yes" : "No";
                row++;
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            wb.SaveAs(fs);

            await Application.Current.MainPage.DisplayAlert("Exported", $"Excel saved:\n{path}", "OK");
        }

        /* ──────────────── SCHEDULED INSPECTIONS ──────────────── */

        public static async Task ExportScheduledInspectionsAsync(List<ScheduledInspectionDisplay> rows, string fileNamePrefix)
        {
            var table = new List<string[]> { new[] { "Company Name", "Inspector(s)", "Scheduled Date" } };

            foreach (var row in rows)
            {
                table.Add(new[]
                {
                    row.CompanyName,
                    row.InspectorUsernames,
                    row.ScheduledDate.ToString("dd MMM yyyy")
                });
            }

            await SaveAsExcelFileAsync(table, $"{fileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        /* ──────────────── INSPECTIONS ──────────────── */

        public static async Task ExportInspectionsAsync(IEnumerable<Inspection> rows)
        {
            var displays = rows.Select(i => new InspectionDisplay
            {
                CompanyName = i.CompanyName,
                InspectorName = i.InspectorName,
                PlannedDate = i.PlannedDate,
                CompletedDate = i.CompletedDate,
            });

            await ExportInspectionsAsync(displays);
        }

        public static async Task ExportInspectionsAsync(IEnumerable<InspectionDisplay> inspections, string title = "Inspections")
        {
            if (inspections == null || !inspections.Any())
            {
                await Application.Current.MainPage.DisplayAlert("Export", "No inspections to export.", "OK");
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string fileName = $"{title}_{stamp}.xlsx";
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Inspections");

            ws.Cell("A1").Value = "Company";
            ws.Cell("B1").Value = "Inspector";
            ws.Cell("C1").Value = "Planned Date";
            ws.Cell("D1").Value = "Completed Date";
            ws.Cell("E1").Value = "Remarks";
            ws.Row(1).Style.Font.Bold = true;

            int row = 2;
            foreach (var i in inspections)
            {
                ws.Cell(row, 1).Value = i.CompanyName;
                ws.Cell(row, 2).Value = i.InspectorName;
                ws.Cell(row, 3).Value = i.PlannedDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 4).Value = i.CompletedDate?.ToString("yyyy-MM-dd") ?? "—";
                ws.Cell(row, 5).Value = i.Remarks ?? "";
                row++;
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            wb.SaveAs(fs);

            await Application.Current.MainPage.DisplayAlert("Exported", $"Excel saved:\n{path}", "OK");
        }

        public static async Task ExportRiskPredictionHistoryToPdfAsync(List<RiskPredictionHistory> history)
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Verdana", 12, XFontStyle.Regular);

            double yPoint = 40;

            gfx.DrawString("Company Risk Prediction History", new XFont("Verdana", 14, XFontStyle.Bold), XBrushes.Black,
                new XRect(0, yPoint, page.Width, page.Height),
                XStringFormats.TopCenter);

            yPoint += 40;

            foreach (var record in history)
            {
                var text = $"Company: {record.CompanyName} | Risk: {record.RiskLevel} | " +
                           $"Violations: {record.ViolationCount} | LastInspection: {record.DaysSinceLastInspection} days | " +
                           $"Renewal: {record.RenewalStatus} | Type: {record.CompanyType} | Date: {record.DatePredicted:yyyy-MM-dd}";

                gfx.DrawString(text, font, XBrushes.Black,
                    new XRect(20, yPoint, page.Width - 40, page.Height),
                    XStringFormats.TopLeft);

                yPoint += 30;

                // Add new page if needed
                if (yPoint > page.Height - 60)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    yPoint = 40;
                }
            }

            string fileName = $"RiskPredictionHistory_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            using (var stream = File.Create(filePath))
            {
                document.Save(stream);
            }

            await Application.Current.MainPage.DisplayAlert("Success", $"PDF exported to: {filePath}", "OK");
        }

        public static async Task ShareRiskPredictionHistoryAsync(List<RiskPredictionHistory> history)
        {
            string fileName = $"RiskPredictionHistory_{DateTime.Now:yyyyMMddHHmmss}.txt";
            string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("Company Risk Prediction History\n");

            foreach (var record in history)
            {
                sb.AppendLine($"Company: {record.CompanyName}");
                sb.AppendLine($"Risk Level: {record.RiskLevel}");
                sb.AppendLine($"Violation Count: {record.ViolationCount}");
                sb.AppendLine($"Days Since Last Inspection: {record.DaysSinceLastInspection}");
                sb.AppendLine($"Renewal Status: {record.RenewalStatus}");
                sb.AppendLine($"Company Type: {record.CompanyType}");
                sb.AppendLine($"Date Predicted: {record.DatePredicted:yyyy-MM-dd}");
                sb.AppendLine("---------------------------------------------");
            }

            File.WriteAllText(filePath, sb.ToString());

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share Risk Prediction History",
                File = new ShareFile(filePath)
            });
        }



        public static async Task ExportRiskPredictionHistoryToExcelAsync(List<RiskPredictionHistory> history, string title = "RiskPredictionHistory")
        {
            if (history == null || history.Count == 0)
            {
                await Application.Current.MainPage.DisplayAlert("Export", "No prediction history to export.", "OK");
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string fileName = $"{title}_{stamp}.xlsx";
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Prediction History");

            // Header
            ws.Cell("A1").Value = "Company Name";
            ws.Cell("B1").Value = "Risk Level";
            ws.Cell("C1").Value = "Violation Count";
            ws.Cell("D1").Value = "Days Since Last Inspection";
            ws.Cell("E1").Value = "Renewal Status";
            ws.Cell("F1").Value = "Company Type";
            ws.Cell("G1").Value = "Date Predicted";
            ws.Row(1).Style.Font.Bold = true;

            // Data
            int row = 2;
            foreach (var record in history)
            {
                ws.Cell(row, 1).Value = record.CompanyName;
                ws.Cell(row, 2).Value = record.RiskLevel;
                ws.Cell(row, 3).Value = record.ViolationCount;
                ws.Cell(row, 4).Value = record.DaysSinceLastInspection;
                ws.Cell(row, 5).Value = record.RenewalStatus;
                ws.Cell(row, 6).Value = record.CompanyType;
                ws.Cell(row, 7).Value = DateTime.Parse(record.DatePredicted).ToString("yyyy-MM-dd HH:mm");
                row++;
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            wb.SaveAs(fs);

            await Application.Current.MainPage.DisplayAlert("Exported", $"Excel saved:\n{path}", "OK");
        }







        /* ──────────────── GENERIC EXCEL SAVER ──────────────── */

        private static async Task SaveAsExcelFileAsync(List<string[]> table, string fileName)
        {
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sheet1");

            int row = 1;
            foreach (var line in table)
            {
                for (int col = 0; col < line.Length; col++)
                {
                    ws.Cell(row, col + 1).Value = line[col];
                }
                row++;
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            wb.SaveAs(fs);

            await Application.Current.MainPage.DisplayAlert("Exported", $"Excel saved:\n{path}", "OK");
        }

        public static async Task ExportCompaniesToPdfAsync(IEnumerable<Company> companies, string fileName)
        {
            // TODO: Implement PDF export logic here.
            await Task.CompletedTask;
        }

        public static async Task ExportCompaniesToExcelAsync(IEnumerable<Company> companies, string fileName)
        {
            // TODO: Implement Excel export logic here.
            await Task.CompletedTask;
        }
    }
}
