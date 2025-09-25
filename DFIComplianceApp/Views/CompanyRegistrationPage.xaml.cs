using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json;

namespace DFIComplianceApp.Views
{
    public partial class CompanyRegistrationPage : ContentPage
    {
        private readonly string _username;
        private readonly ISmsSender _sms;
        private readonly FirebaseAuthService _firebaseService;
        private readonly List<Entry> _applicantEntries = new();
        private bool _isManualFileNumber = false;
        private readonly List<Company> _masterCompanies = new(); // local cached copy
        private readonly List<Company> _allCompanies = new();

        public CompanyRegistrationPage(string username, ISmsSender sms, FirebaseAuthService firebaseService)
        {
            InitializeComponent();

            _username = username ?? throw new ArgumentNullException(nameof(username));
            _sms = sms ?? throw new ArgumentNullException(nameof(sms));
            _firebaseService = firebaseService ?? throw new ArgumentNullException(nameof(firebaseService));

            RegisteredByLabel.Text = $"Registered by: {_username}";
            RegistrationDateLabel.Text = $"Date: {DateTime.Now:dd MMM yyyy}";

            MalePicker.ItemsSource = Enumerable.Range(0, 201).ToList();
            FemalePicker.ItemsSource = Enumerable.Range(0, 201).ToList();
            ApplicantCountPicker.ItemsSource = Enumerable.Range(0, 11).Select(i => i.ToString()).ToList();
            ApplicantCountPicker.SelectedIndex = 0;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Subscribe to new company registrations
            MessagingCenter.Subscribe<CompanyRegistrationPage, Company>(this, "CompanyRegistered", (sender, company) =>
            {
                if (!_allCompanies.Any(c => c.Id == company.Id))
                {
                    _allCompanies.Add(company);
                   
                }
            });

            _ = LoadCompaniesAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessagingCenter.Unsubscribe<CompanyRegistrationPage, Company>(this, "CompanyRegistered");
        }


        private async Task LoadCompaniesAsync()
        {
            try
            {
                LoadingIndicator.IsRunning = true;
                LoadingIndicator.IsVisible = true;

                // 1️⃣ Load local companies first
                var localCompanies = await App.Database.GetCompaniesAsync();
                _allCompanies.Clear();
                _allCompanies.AddRange(localCompanies);

                // Keep a local dictionary of unsynced companies
                var unsyncedCompanies = localCompanies.Where(c => !c.IsSynced).ToDictionary(c => c.Id, c => c);

                // 2️⃣ If online, fetch Firebase companies
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var firebaseCompanies = await App.Firebase.GetCompaniesAsync();

                    foreach (var firebaseCompany in firebaseCompanies)
                    {
                        // If we have the company locally and unsynced, skip overwriting
                        if (unsyncedCompanies.ContainsKey(firebaseCompany.Id))
                            continue;

                        // Save/update locally
                        await App.Database.SaveCompanyAsync(firebaseCompany);

                        // Add to in-memory list if not already present
                        if (!_allCompanies.Any(c => c.Id == firebaseCompany.Id))
                            _allCompanies.Add(firebaseCompany);
                    }
                }

                
                
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load companies: {ex.Message}", "OK");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }


        private void MergeCompanies(IEnumerable<Company> companies)
        {
            foreach (var c in companies)
            {
                var existing = _masterCompanies.FirstOrDefault(x => x.Id == c.Id);
                if (existing != null)
                {
                    // Update existing
                    existing.Name = c.Name;
                    existing.FileNumber = c.FileNumber;
                    existing.CertificateNumber = c.CertificateNumber;
                    existing.Location = c.Location;
                    existing.PostalAddress = c.PostalAddress;
                    existing.NatureOfWork = c.NatureOfWork;         
                    existing.Occupier = c.Occupier;
                    existing.Contact = c.Contact;
                    existing.Email = c.Email;
                    existing.EmployeesMale = c.EmployeesMale;
                    existing.EmployeesFemale = c.EmployeesFemale;
                    existing.ApplicantNames = c.ApplicantNames;
                    existing.RegistrationDate = c.RegistrationDate;
                    existing.RegisteredBy = c.RegisteredBy;
                }
                else
                {
                    _masterCompanies.Add(c);
                }
            }
        }

        private void OnApplicantCountChanged(object? sender, EventArgs e)
        {
            _applicantEntries.Clear();
            ApplicantsStack.Children.Clear();

            if (ApplicantCountPicker.SelectedItem is not string str || !int.TryParse(str, out int count) || count == 0)
                return;

            for (int i = 1; i <= count; i++)
            {
                var entry = new Entry
                {
                    Placeholder = $"Full name of applicant #{i}",
                    Style = (Style)Resources["Input"]
                };
                AttachLettersOnlyFilter(entry);
                _applicantEntries.Add(entry);
                ApplicantsStack.Children.Add(entry);
            }
        }

        private static void AttachLettersOnlyFilter(Entry entry)
        {
            entry.TextChanged += (_, args) =>
            {
                string filtered = new string(args.NewTextValue.Where(ch => char.IsLetter(ch) || ch is ' ' or '-').ToArray());
                if (filtered != args.NewTextValue)
                    entry.Text = filtered;
            };
        }

        private async void OnAutoGenerateFileNumberClicked(object sender, EventArgs e)
        {
            _isManualFileNumber = false;
            FileNumberEntry.IsEnabled = false;
            FileNumberEntry.Text = await GenerateFileNumberAsync();
        }

        private void OnAddManualFileNumberClicked(object sender, EventArgs e)
        {
            _isManualFileNumber = true;
            FileNumberEntry.IsEnabled = true;
            FileNumberEntry.Text = string.Empty;
        }

        private async Task<string> GenerateFileNumberAsync()
        {
            var existingNumbers = _masterCompanies.Select(c => c.FileNumber).ToHashSet();
            var random = new Random();
            string candidate;
            do
            {
                candidate = random.Next(10, 999999).ToString();
            } while (existingNumbers.Contains(candidate));

            return candidate;
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            RegisterButton.IsEnabled = false;
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;

            try
            {
                string name = Trim(NameEntry.Text);
                string location = Trim(LocationEntry.Text);
                string postal = Trim(PostalEntry.Text);
                string nature = NatureOfWorkPicker.SelectedItem?.ToString()?.Trim() ?? string.Empty;
                string fileNo = Trim(FileNumberEntry.Text);
                string certNo = Trim(CertificateNumberEntry.Text);
                string occupier = Trim(OccupierEntry.Text);
                string contact = Trim(ContactEntry.Text);
                string email = Trim(EmailEntry.Text);

                if (new[] { name, location, postal, nature, occupier, contact, email }.Any(string.IsNullOrEmpty))
                {
                    await DisplayAlert("Validation", "Fill in all required fields (*).", "OK");
                    return;
                }

                if (!Regex.IsMatch(fileNo, @"^\d{2,}$") || _masterCompanies.Any(c => c.FileNumber == fileNo))
                {
                    await DisplayAlert("File Number", "Invalid or duplicate file number.", "OK");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(certNo) && _masterCompanies.Any(c => c.CertificateNumber == certNo))
                {
                    await DisplayAlert("Certificate Number", "Duplicate certificate number.", "OK");
                    return;
                }

                if (!Regex.IsMatch(contact, @"^\d{10}$"))
                {
                    await DisplayAlert("Contact", "Contact number must be exactly 10 digits.", "OK");
                    return;
                }

                if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    await DisplayAlert("Email", "Invalid e‑mail address.", "OK");
                    return;
                }

                foreach (var ent in _applicantEntries)
                {
                    string val = Trim(ent.Text);
                    if (val.Length == 0 || !Regex.IsMatch(val, @"^[A-Za-z\s\-]+$"))
                    {
                        await DisplayAlert("Applicants", "Applicant names may contain letters, spaces or hyphens only.", "OK");
                        return;
                    }
                }

                int males = MalePicker.SelectedItem is int m ? m : 0;
                int females = FemalePicker.SelectedItem is int f ? f : 0;

                var company = new Company
                {
                    Id = Guid.NewGuid(),
                    Name = ToTitle(name),
                    Location = ToTitle(location),
                    PostalAddress = postal,
                    NatureOfWork = nature,
                    FileNumber = fileNo,
                    CertificateNumber = certNo,
                    Occupier = ToTitle(occupier),
                    Contact = contact,
                    Email = email,
                    RegisteredBy = _username,
                    RegistrationDate = DateTime.Now,
                    EmployeesMale = males,
                    EmployeesFemale = females,
                    ApplicantNames = string.Join("; ", _applicantEntries.Select(a => ToTitle(a.Text ?? "")))
                };

                company.Name ??= "";
                company.Location ??= "";
                company.PostalAddress ??= "";
                company.NatureOfWork ??= "";
                company.Occupier ??= "";
                company.Contact ??= "";
                company.Email ??= "";
                company.RegisteredBy ??= "";
                company.ApplicantNames ??= "";

                if (string.IsNullOrWhiteSpace(company.RecordId))
                    company.RecordId = Guid.NewGuid().ToString();
                if (company.Id == Guid.Empty)
                    company.Id = Guid.NewGuid();

                var json = JsonSerializer.Serialize(company, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                Console.WriteLine(json); // Inspect output

                await RegisterCompanyAsync(company);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                LoadingIndicator.IsRunning = false; 
                LoadingIndicator.IsVisible = false;
                RegisterButton.IsEnabled = true;
            }
        }
        // Place this inside the CompanyRegistrationPage class

        private void CertificateNumberEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            // Allow only digits
            string newText = new string(entry.Text?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());

            if (entry.Text != newText)
            {
                int cursorPos = entry.CursorPosition - (entry.Text.Length - newText.Length);
                if (cursorPos < 0) cursorPos = 0;

                entry.Text = newText;
                entry.CursorPosition = cursorPos;
            }
        }
        private void OccupierEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            // Allow only alphabets and spaces (modify as needed)
            string newText = new string(entry.Text?.Where(c => char.IsLetter(c) || char.IsWhiteSpace(c)).ToArray() ?? Array.Empty<char>());

            if (entry.Text != newText)
            {
                int cursorPos = entry.CursorPosition - (entry.Text.Length - newText.Length);
                if (cursorPos < 0) cursorPos = 0;

                entry.Text = newText;
                entry.CursorPosition = cursorPos;
            }
        }
        void OnFileNumberChanged(object? s, TextChangedEventArgs e)
        {
            if (!_isManualFileNumber) return;
            string digits = new(e.NewTextValue.Where(char.IsDigit).ToArray());
            if (digits != e.NewTextValue)
                ((Entry)s!).Text = digits;
        }
        void OnOccupierChanged(object? s, TextChangedEventArgs e)
        {
            string filtered = new(e.NewTextValue.Where(ch => char.IsLetter(ch) || ch is ' ' or '-').ToArray());
            if (filtered != e.NewTextValue)
                ((Entry)s!).Text = filtered;
        }
        void OnCertChanged(object? s, TextChangedEventArgs e)
        {
            string digits = new(e.NewTextValue.Where(char.IsDigit).ToArray());
            if (digits != e.NewTextValue)
                ((Entry)s!).Text = digits;
        }
        void OnContactChanged(object? s, TextChangedEventArgs e)
        {
            string digits = new(e.NewTextValue.Where(char.IsDigit).Take(10).ToArray());
            if (digits != e.NewTextValue)
                ((Entry)s!).Text = digits;
        }

        private void ContactEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            // Allow only digits, max length handled by MaxLength property
            string newText = new string(entry.Text?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());

            if (entry.Text != newText)
            {
                int cursorPos = entry.CursorPosition - (entry.Text.Length - newText.Length);
                if (cursorPos < 0) cursorPos = 0;

                entry.Text = newText;
                entry.CursorPosition = cursorPos;
            }
        }
        private async Task RegisterCompanyAsync(Company company)
        {
            company.IsSynced = false;

            // 1. Save company locally first
            await App.Database.SaveCompanyAsync(company);
            _masterCompanies.Add(company);
            Debug.WriteLine($"Company saved locally: {company.Name} ({company.Id})");

            // 2. Create renewal record
            var renewal = new CompanyRenewal
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                RenewalYear = DateTime.Now.Year,
                RenewalDate = DateTime.Now,
                RenewedBy = _username
            };

            await App.Database.SaveCompanyRenewalAsync(renewal);
            Debug.WriteLine($"Renewal saved locally: {renewal.CompanyId} ({renewal.RenewalYear})");

            // 3. Save audit log
            await App.Database.SaveAuditLogAsync(new AuditLog
            {
                Username = _username,
                Role = "Director",
                Action = $"Registered company “{company.Name}” (+ renewal {DateTime.Now.Year})",
                Timestamp = DateTime.Now
            });

            // 4. Push company and renewal to Firebase
            try
            {
                await _firebaseService.PushCompanyAsync(company);
                await _firebaseService.PushCompanyRenewalAsync(company.Id.ToString(), renewal);

                company.IsSynced = true;
                await App.Database.UpdateCompanySyncStatusAsync(company.Id, true);
                _allCompanies.Add(company);
                Debug.WriteLine($"Company and renewal pushed to Firebase: {company.Name} ({company.Id})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to push to Firebase: {ex.Message}");
            }

            // 5. Outbox for email/SMS (optional)
            string smsBody = $"Hi {company.Occupier}, your company “{company.Name}” was successfully registered on {company.RegistrationDate:dd MMM yyyy}.";
            _ = Task.Run(async () =>
            {
                try
                {
                    string e164 = company.Contact.StartsWith("+") ? company.Contact : $"+233{company.Contact}";
                    await _sms.SendSmsAsync(e164, smsBody);
                }
                catch { }

                await App.Database.SaveOutboxAsync(new OutboxMessage
                {
                    Type = "Email",
                    To = company.Email,
                    Subject = "Company Registration Successful",
                    Body = smsBody.Replace("Hi ", "Dear ").Replace(" was", " has been"),
                    IsHtml = false,
                    IsSent = false,
                    CreatedAt = DateTime.UtcNow
                });
            });

            await ToastAsync("✅ Company registered and synced.");

            // 6. Navigate to renewal history page and remove registration page from stack
            await Navigation.PushAsync(new CompanyRenewalHistoryPage(company));

            // Remove current (registration) page so user doesn't see old input on back
            Navigation.RemovePage(this);
        }




        private static string Trim(string? s) => s?.Trim() ?? string.Empty;
        private static string ToTitle(string s) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());

        private static async Task ToastAsync(string message)
        {
            try
            {
                await Toast.Make(message, ToastDuration.Short).Show();
            }
            catch
            {
                await Application.Current.MainPage.DisplayAlert("Info", message, "OK");
            }
        }
    }
}
