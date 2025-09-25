using ClosedXML.Excel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DFIComplianceApp.Views
{
    public partial class CompanyListPage : ContentPage
    {
        private readonly string _userRole;
        private readonly string _currentUsername;
        private readonly IEmailService _email;
        private readonly ISmsSender _sms;

        private readonly List<Company> _allCompanies = new();

        public CompanyListPage(string userRole, string currentUsername)
            : this(userRole, currentUsername, false) { }

        public CompanyListPage(string userRole, string currentUsername, bool autoExport)
        {
            InitializeComponent();
            _userRole = userRole;
            _currentUsername = currentUsername;

            _email = App.Services.GetRequiredService<IEmailService>();
            _sms = App.Services.GetRequiredService<ISmsSender>();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // Check local DB first
                var localCompanies = await App.Database.GetCompaniesAsync();

                if (localCompanies == null || !localCompanies.Any())
                {
                    // 🔑 Local DB is empty → fetch from Firebase first
                    var firebaseCompanies = await App.Firebase.GetCompaniesSafeAsync();

                    if (firebaseCompanies.Any())
                    {
                        foreach (var f in firebaseCompanies)
                        {
                            try
                            {
                                // Save Firebase companies into local DB
                                await App.Database.SaveCompanyAsync(f);
                            }
                            catch { /* ignore individual failures */ }
                        }
                    }
                }

                // Now load everything (this will merge Firebase + DB and refresh UI)
                await LoadCompaniesAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load companies: {ex.Message}", "OK");
            }
        }


        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessagingCenter.Unsubscribe<object, Company>(this, "CompanyRegistered");
        }
        private void ShowErrorState(string message)
        {
            ErrorMessageLabel.Text = message;
            ErrorStateView.IsVisible = true;
            CompaniesCollectionView.IsVisible = false;
        }

        private void HideErrorState()
        {
            ErrorStateView.IsVisible = false;
            CompaniesCollectionView.IsVisible = true;
        }

        private async void OnRetryClicked(object sender, EventArgs e)
        {
            await LoadCompaniesAsync();
        }

        private async Task LoadCompaniesAsync()
        {
            try
            {
                RefreshView.IsRefreshing = true;

                // Always try Firebase first
                var firebaseCompanies = await App.Firebase.GetCompaniesSafeAsync();

                if (firebaseCompanies == null || !firebaseCompanies.Any())
                {
                    ShowErrorState("⚠️ Could not load companies. Check your internet connection.");
                    return;
                }

                // Load renewal dates safely
                var tasks = firebaseCompanies.Select(async c =>
                {
                    try
                    {
                        c.EffectiveLastRenewalDate = await App.Database.GetEffectiveLastRenewalDateAsync(c.Id);
                    }
                    catch
                    {
                        c.EffectiveLastRenewalDate = c.RegistrationDate;
                    }
                    c.LastRenewalDate = c.EffectiveLastRenewalDate;
                    return c;
                }).ToList();

                var updatedCompanies = await Task.WhenAll(tasks);

                // Update in-memory list
                _allCompanies.Clear();
                _allCompanies.AddRange(updatedCompanies);

                // Save Firebase copy into local DB for history/audit
                foreach (var f in firebaseCompanies)
                {
                    try { await App.Database.SaveCompanyAsync(f); } catch { }
                }

                // Refresh filters + UI
                BuildFilterPickers();
                ApplyCurrentFilters();
                HideErrorState(); // clear retry UI if showing
            }
            catch (Exception ex)
            {
                ShowErrorState("⚠️ Unable to load companies. Please check your internet and retry.");
            }
            finally
            {
                RefreshView.IsRefreshing = false;
            }
        }




        private void OnRefresh(object sender, EventArgs e) => _ = LoadCompaniesAsync();

        private void BuildFilterPickers()
        {
            var years = _allCompanies
                        .Select(c => c.RegistrationDate.Year)
                        .Union(_allCompanies.Select(c => (c.EffectiveLastRenewalDate ?? c.RegistrationDate).Year))
                        .Distinct()
                        .OrderByDescending(y => y)
                        .ToList();

            YearFilterPicker.ItemsSource = new List<string> { "All" }.Concat(years.Select(y => y.ToString())).ToList();
            YearFilterPicker.SelectedIndex = 0;

            var locations = _allCompanies
                            .Select(c => c.Location ?? string.Empty)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Distinct()
                            .OrderBy(s => s)
                            .ToList();

            LocationFilterPicker.ItemsSource = new List<string> { "All" }.Concat(locations).ToList();
            LocationFilterPicker.SelectedIndex = 0;
        }

        private void OnSearchTextChanged(object s, TextChangedEventArgs e) => ApplyCurrentFilters();
        private void OnFilterChanged(object s, EventArgs e) => ApplyCurrentFilters();

        private void ApplyCurrentFilters()
        {
            string q = (CompanySearchBar.Text ?? string.Empty).Trim().ToLower();
            string yr = (YearFilterPicker.SelectedItem as string) ?? "All";
            string loc = (LocationFilterPicker.SelectedItem as string) ?? "All";
            string status = (StatusFilterPicker.SelectedItem as string) ?? "Active";

            IEnumerable<Company> result = _allCompanies;

            if (status == "Active")
                result = result.Where(c => !c.IsDormant);
            else if (status == "Dormant")
                result = result.Where(c => c.IsDormant);

            if (!string.IsNullOrWhiteSpace(q))
                result = result.Where(c => c.Name.ToLower().Contains(q));

            if (yr != "All" && int.TryParse(yr, out int y))
                result = result.Where(c => (c.EffectiveLastRenewalDate ?? c.RegistrationDate).Year == y);

            if (loc != "All")
                result = result.Where(c => string.Equals(c.Location, loc, StringComparison.OrdinalIgnoreCase));

            CompaniesCollectionView.ItemsSource = result.ToList();
        }



        private void OnEditClicked(object s, EventArgs e)
        {
            if ((s as Button)?.BindingContext is Company c)
            {

                c.IsEditing = true;
                RefreshList();
            }
        }

        private async void OnSaveChangesClicked(object s, EventArgs e)
        {
            if ((s as Button)?.BindingContext is not Company c) return;
            if (_userRole != "Director") return;

            // ✅ Validation including new fields
            if (string.IsNullOrWhiteSpace(c.Name) ||
                string.IsNullOrWhiteSpace(c.Location) ||
                string.IsNullOrWhiteSpace(c.NatureOfWork) ||
                string.IsNullOrWhiteSpace(c.FileNumber) ||
                string.IsNullOrWhiteSpace(c.Occupier) ||
                string.IsNullOrWhiteSpace(c.ApplicantNames))
            {
                await DisplayAlert("Validation", "Fill in all required fields including Applicant Names.", "OK");
                return;
            }

            if (c.EmployeesMale < 0 || c.EmployeesFemale < 0)
            {
                await DisplayAlert("Validation", "Employee counts cannot be negative.", "OK");
                return;
            }

            if (!Regex.IsMatch(c.FileNumber, @"^\d{2,}$"))
            {
                await DisplayAlert("File Number", "File number must be all numbers, at least 2 digits.", "OK");
                return;
            }

            var allCompanies = await App.Database.GetCompaniesAsync();
            if (allCompanies.Any(x => x.Id != c.Id && x.FileNumber == c.FileNumber))
            {
                await DisplayAlert("File Number", "This file number already exists. Please enter a unique number.", "OK");
                return;
            }

            // ✅ Require internet
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await DisplayAlert("No Internet", "An internet connection is required to save changes.", "OK");
                return;
            }

            var originalCompany = allCompanies.FirstOrDefault(x => x.Id == c.Id);

            c.IsEditing = false;

            // ✅ Push to Firebase first (central storage)
            try
            {
                await App.Firebase.PushCompanyAsync(c);
                c.IsSynced = true;

                // Keep a local snapshot for offline browsing
                await App.Database.SaveCompanyAsync(c);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Firebase sync failed: {ex.Message}");
                await DisplayAlert("Save Failed", "Could not save to Firebase. Please try again.", "OK");
                return;
            }

            // ✅ Audit log for edits (Firebase first, then local)
            string editAction = $"Edited company: {c.Name}";

            var audit = new AuditLog
            {
                Username = _currentUsername,
                Role = _userRole,
                Action = editAction,
                Timestamp = DateTime.Now
            };

            try
            {
                await App.Firebase.LogAuditAsync(editAction, _currentUsername, _userRole, $"Company '{c.Name}' was updated.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Firebase audit log failed: {ex.Message}");
            }

            await App.Database.SaveAuditLogAsync(audit);

            // ✅ Handle dormant/reactivation separately
            if (originalCompany != null && originalCompany.IsDormant != c.IsDormant)
            {
                string dormantAction = c.IsDormant ? "marked as Dormant" : "reactivated";

                var dormantLog = new AuditLog
                {
                    Username = _currentUsername,
                    Role = _userRole,
                    Action = $"Company '{c.Name}' {dormantAction}",
                    Timestamp = DateTime.Now
                };

                try
                {
                    await App.Firebase.LogAuditAsync(dormantLog.Action, _currentUsername, _userRole, "Dormant status changed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Firebase dormant audit log failed: {ex.Message}");
                }

                await App.Database.SaveAuditLogAsync(dormantLog);
            }

            RefreshList();

            // ✅ Show toast / message
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var toast = Toast.Make("Company saved and synced successfully!", ToastDuration.Short);
                await toast.Show();
            });
        }




        private async void OnRenewCompanyClicked(object s, EventArgs e)
        {
            if ((s as Button)?.BindingContext is not Company company) return;

            // Prevent renewal if company is dormant
            if (company.IsDormant)
            {
                await DisplayAlert("Cannot Renew", $"The company '{company.Name}' is marked as Dormant and cannot be renewed.", "OK");
                return;
            }

            var renewals = await App.Database.GetCompanyRenewalsAsync(company.Id);
            var renewedYrs = renewals.Select(r => r.RenewalYear).ToHashSet();
            renewedYrs.Add(company.RegistrationDate.Year);

            int currentYear = DateTime.Now.Year;
            var choices = Enumerable.Range(company.RegistrationDate.Year, currentYear - company.RegistrationDate.Year + 1)
                                    .Where(y => !renewedYrs.Contains(y))
                                    .OrderByDescending(y => y)
                                    .Select(y => y.ToString())
                                    .ToArray();

            if (choices.Length == 0)
            {
                await DisplayAlert("Info", "All possible years already renewed.", "OK");
                return;
            }

            string pick = await DisplayActionSheet("Select renewal year", "Cancel", null, choices);
            if (pick is null || pick == "Cancel") return;
            if (!int.TryParse(pick, out int year)) return;

            var renewal = new CompanyRenewal
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                RenewalYear = year,
                RenewalDate = DateTime.Now,
                RenewedBy = _currentUsername
            };
            await App.Database.SaveCompanyRenewalAsync(renewal);

            company.EffectiveLastRenewalDate = await App.Database.GetEffectiveLastRenewalDateAsync(company.Id);
            company.LastRenewalDate = company.EffectiveLastRenewalDate;
            company.RenewedBy = _currentUsername;
            company.IsSynced = false;
            await App.Database.SaveCompanyAsync(company);
            await App.Firebase.SyncPendingCompaniesAsync();


            await App.Database.SaveAuditLogAsync(new AuditLog
            {
                Username = _currentUsername,
                Role = _userRole,
                Action = $"Renewed {company.Name} for {year}",
                Timestamp = DateTime.Now
            });

            // ✅ Send confirmation e‑mail and SMS
            string subject = $"Renewal Confirmed – {company.Name}";
            string body = $"Dear {company.Occupier},\n\nYour company “{company.Name}” has been renewed for {year}.\n\nThank you for staying compliant with DFI.\n";

            await App.Database.SaveOutboxAsync(new OutboxMessage
            {
                Type = "Email",
                To = company.Email,
                Subject = subject,
                Body = body,
                IsHtml = false,
                IsSent = false,
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                if (!string.IsNullOrWhiteSpace(company.Contact))
                {
                    string e164 = company.Contact.StartsWith("+") ? company.Contact : $"+233{company.Contact}";
                    await _sms.SendSmsAsync(e164, $"Your company “{company.Name}” has been renewed for {year}. Thank you.");
                }
            }
            catch { /* silent */ }

            RefreshList();
            await DisplayAlert("Success", $"Renewed “{company.Name}” for {year}.", "OK");
        }

        private async void OnExportToPdfClicked(object s, EventArgs e)
        {
            var rows = (CompaniesCollectionView.ItemsSource as IEnumerable<Company>)?.ToList() ?? new();
            if (rows.Count == 0)
            {
                await DisplayAlert("Export", "Nothing to export.", "OK");
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(FileSystem.CacheDirectory, $"Companies_{stamp}.pdf");

            var pdf = new PdfDocument();
            var page = pdf.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Arial", 10);

            const double margin = 30, lineH = 16;
            double y = margin;

            // Header line with new columns added
            string header = $"{"Name",-25} {"Location",-18} {"File #",-8} {"Applicants",-20} {"Male",4} {"Female",6} {"Registered",12}";
            gfx.DrawString(header, font, XBrushes.DarkBlue, new XPoint(margin, y));
            y += lineH;

            foreach (var c in rows)
            {
                string line = $"{c.Name,-25} {c.Location,-18} {c.FileNumber,-8} {Truncate(c.ApplicantNames, 20),-20} {c.EmployeesMale,4} {c.EmployeesFemale,6} {c.RegistrationDate:yyyy-MM-dd,12}";
                gfx.DrawString(line, font, XBrushes.Black, new XPoint(margin, y));
                y += lineH;

                if (y > page.Height - margin)
                {
                    page = pdf.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                    gfx.DrawString(header, font, XBrushes.DarkBlue, new XPoint(margin, y));
                    y += lineH;
                }
            }

            using var fs = File.Create(path);
            pdf.Save(fs);

            await DisplayAlert("Export", $"PDF saved to\n{path}", "OK");
        }

        // Helper method to truncate long strings (optional)
        private string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }
        private async void OnViewRenewalHistoryClicked(object s, EventArgs e)
        {
            if ((s as Button)?.BindingContext is Company c)
                await Navigation.PushAsync(new CompanyRenewalHistoryPage(c));
        }

        private async Task ExportToExcelAsync(IEnumerable<Company>? rows = null)
        {
            rows ??= (CompaniesCollectionView.ItemsSource as IEnumerable<Company>)?.ToList() ?? new();
            if (!rows.Any())
            {
                await DisplayAlert("Export", "Nothing to export.", "OK");
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(FileSystem.CacheDirectory, $"Companies_{stamp}.xlsx");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Companies");

            ws.Cell("A1").Value = "Name";
            ws.Cell("B1").Value = "Location";
            ws.Cell("C1").Value = "File #";
            ws.Cell("D1").Value = "Applicants";
            ws.Cell("E1").Value = "Employees Male";
            ws.Cell("F1").Value = "Employees Female";
            ws.Cell("G1").Value = "Registered";
            ws.Cell("H1").Value = "Last Renewed";

            int r = 2;
            foreach (var c in rows)
            {
                ws.Cell(r, 1).Value = c.Name;
                ws.Cell(r, 2).Value = c.Location;
                ws.Cell(r, 3).Value = c.FileNumber;
                ws.Cell(r, 4).Value = c.ApplicantNames;
                ws.Cell(r, 5).Value = c.EmployeesMale;
                ws.Cell(r, 6).Value = c.EmployeesFemale;
                ws.Cell(r, 7).Value = c.RegistrationDate.ToString("yyyy-MM-dd");
                ws.Cell(r, 8).Value = (c.EffectiveLastRenewalDate ?? c.RegistrationDate).ToString("yyyy-MM-dd");
                r++;
            }

            using var fs = File.Create(path);
            wb.SaveAs(fs);

            await DisplayAlert("Export", $"Excel saved to\n{path}", "OK");
        }



        private async void OnExportToExcelClicked(object sender, EventArgs e)
        {
            await ExportToExcelAsync();
        }

        private void OnToggleExpandClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is Company company)
            {
                company.IsExpanded = !company.IsExpanded;
            }
        }



        private void RefreshList()
        {
            ApplyCurrentFilters();
        }

        public bool IsEditable => _userRole == "Director";
    }
}
