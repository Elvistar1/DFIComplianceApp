using CommunityToolkit.Mvvm.Messaging;
using DFIComplianceApp.Messages;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Text.RegularExpressions;

namespace DFIComplianceApp.Views
{
    public partial class CreateUserPage : ContentPage
    {
        private readonly string _currentUsername;
        private readonly FirebaseAuthService _firebaseAuth;

        public CreateUserPage(string currentUsername, FirebaseAuthService firebaseAuth)
        {
            InitializeComponent();
            _currentUsername = currentUsername;
            _firebaseAuth = firebaseAuth;

            loadingSpinner.IsVisible = false;
            loadingSpinner.IsRunning = false;
        }

        void OnFullNameTextChanged(object? s, TextChangedEventArgs e)
        {
            var cleaned = Regex.Replace(e.NewTextValue ?? "", @"[^A-Za-z\- ]", "");
            if (cleaned != e.NewTextValue)
                ((Entry)s!).Text = cleaned;
        }

        void OnPhoneTextChanged(object? s, TextChangedEventArgs e)
        {
            var digits = Regex.Replace(e.NewTextValue ?? "", @"\D", "");
            if (digits != e.NewTextValue)
                ((Entry)s!).Text = digits;
        }

        async void OnCreateUserClicked(object sender, EventArgs e)
        {
            loadingSpinner.IsVisible = true;
            loadingSpinner.IsRunning = true;

            try
            {
                // ───────── Internet Check ─────────
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    await DisplayAlert("No Internet", "You must be online to create users.", "OK");
                    return;
                }

                // ───────── Gather Input ─────────
                string fullName = fullNameEntry.Text?.Trim() ?? "";
                string username = usernameEntry.Text?.Trim().ToLowerInvariant() ?? "";
                string email = emailEntry.Text?.Trim().ToLowerInvariant() ?? "";
                string phone = phoneEntry.Text?.Trim() ?? "";
                string address = addressEditor.Text?.Trim() ?? "";
                string role = rolePicker.SelectedItem as string ?? "";

                // ───────── Validation ─────────
                if (string.IsNullOrWhiteSpace(fullName) ||
                    string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(phone) ||
                    string.IsNullOrWhiteSpace(address) ||
                    string.IsNullOrWhiteSpace(role))
                {
                    await DisplayAlert("Validation", "Please fill in all fields.", "OK");
                    return;
                }

                if (!Regex.IsMatch(fullName, @"^[A-Za-z -]+$"))
                {
                    await DisplayAlert("Full Name", "Use letters and hyphens only.", "OK");
                    return;
                }


                if (!Regex.IsMatch(phone, @"^\d{10}$"))
                {
                    await DisplayAlert("Phone", "Phone number must be exactly 10 digits.", "OK");
                    return;
                }


                if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    await DisplayAlert("Email", "Invalid email format.", "OK");
                    return;
                }


                if (await App.Database.GetUserByUsernameAsync(username) != null)
                {
                    await DisplayAlert("Duplicate", "Username already exists.", "OK");
                    return;
                }

                if (await App.Database.GetUserByEmailAsync(email) != null)
                {
                    await DisplayAlert("Duplicate", "Email already exists.", "OK");
                    return;
                }

                // ───────── Create Firebase Auth User ─────────
                string tempPassword = Guid.NewGuid().ToString("N")[..12]; // random temp pwd
                var createdUser = await _firebaseAuth.RegisterUserAsync(email, tempPassword);

                if (createdUser == null)
                {
                    await DisplayAlert("Error", "Failed to create Firebase user.", "OK");
                    return;
                }

                // ───────── Save Profile in Realtime DB ─────────
                var user = new User
                {
                    FullName = fullName,
                    Username = username,
                    Email = email,
                    PhoneNumber = phone,
                    ContactAddress = address,
                    Role = role,
                    IsActive = true,
                    ForcePasswordChange = true,
                    DFIUserId = createdUser.LocalId,
                    CreatedAt = DateTime.UtcNow
                };

                await _firebaseAuth.PushUserAsync(user);

                // ───────── Send Password Reset ─────────
                bool resetSent = false;
                try
                {
                    resetSent = await _firebaseAuth.SendPasswordResetEmailAsync(email);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Warning",
                        $"User created but reset email could not be sent.\n\nDetails: {ex.Message}",
                        "OK");
                }

                // ───────── Audit Log ─────────
                var currentUser = App.CurrentUser;
                string auditAction = $"Created user {username} ({role})";

                try
                {
                    // ✅ Firebase central log
                    await App.Firebase.LogAuditAsync(auditAction, currentUser?.Username ?? "Unknown", currentUser?.Role ?? "Unknown", $"User {username} created");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Firebase audit log failed: {ex.Message}");
                }

                // Always store locally too
                await App.Database.SaveAuditLogAsync(new AuditLog
                {
                    Username = currentUser?.Username ?? "Unknown",
                    Role = currentUser?.Role ?? "Unknown",
                    Action = auditAction,
                    Timestamp = DateTime.Now
                });

                // ───────── Notify Other Views ─────────
                WeakReferenceMessenger.Default.Send(new UserAddedMessage(user));

                // ───────── Confirmation ─────────
                if (resetSent)
                {
                    await DisplayAlert("Created",
                        $"{role} “{username}” added.\nA password setup link has been e-mailed to {email}.\nGenerated ID: {user.DFIUserId}",
                        "OK");
                }
                else
                {
                    await DisplayAlert("Created",
                        $"{role} “{username}” added.\nHowever, the password setup email could not be sent to {email}.\nGenerated ID: {user.DFIUserId}",
                        "OK");
                }

                await Navigation.PopAsync();
            }
            finally
            {
                loadingSpinner.IsRunning = false;
                loadingSpinner.IsVisible = false;
            }
        }


    }

}
