using CommunityToolkit.Mvvm.Messaging;
using DFIComplianceApp.Helpers;
using DFIComplianceApp.Messages;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DFIComplianceApp.Views
{
    public partial class ManageUsersPage : ContentPage, IRecipient<UserAddedMessage>
    {
        readonly ObservableCollection<User> _master = new();
        readonly ObservableCollection<User> _view = new();

        readonly string _currentUsername;
        readonly string _userRole;
        readonly IFirebaseAuthService _firebaseAuthService;

        bool _isListenerAttached;

        public ManageUsersPage(string currentUsername = "Director", string userRole = "Director", IFirebaseAuthService firebaseAuthService = null)
        {
            InitializeComponent();
            _currentUsername = currentUsername;
            _userRole = userRole;

            // Resolve service safely
            _firebaseAuthService = firebaseAuthService ?? App.Services.GetService<IFirebaseAuthService>();

            UserCollectionView.ItemsSource = _view;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            WeakReferenceMessenger.Default.Register(this);

            // Load users without blocking UI
            await LoadUsersAsync();

            if (_firebaseAuthService != null && !_isListenerAttached)
            {
                // Run listener async so it doesn't block UI
                await Task.Run(() => _firebaseAuthService.StartUserListener());
                _firebaseAuthService.UsersChanged += OnFirebaseUsersChanged;
                _isListenerAttached = true;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            WeakReferenceMessenger.Default.Unregister<UserAddedMessage>(this);

            if (_firebaseAuthService != null && _isListenerAttached)
            {
                _firebaseAuthService.UsersChanged -= OnFirebaseUsersChanged;
                _isListenerAttached = false;
            }
        }

        public async void Receive(UserAddedMessage msg) => await LoadUsersAsync();

        // Replace your LoadUsersAsync with this version to always load from Firebase and update local cache
        async Task LoadUsersAsync()
        {
            try
            {
                // Always fetch from Firebase for centralization
                var allUsers = await _firebaseAuthService.GetUsersAsync();

                // Update local cache in the background
                _ = Task.Run(async () =>
                {
                    foreach (var u in allUsers)
                        await App.Database.SaveUserAsync(u);
                });

                // Prepare users off the UI thread
                var processed = allUsers.Select(u =>
                {
                    UpdateUiProps(u);
                    u.CanEdit = CanEditUser(u);
                    return u;
                }).ToList();

                // Update UI on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _master.Clear();
                    foreach (var u in processed)
                        _master.Add(u);

                    ApplyFilter();
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    DisplayAlert("Error", $"Failed to load users: {ex.Message}", "OK"));
            }
        }

        void UpdateUiProps(User u)
        {
            u.StatusText = u.IsActive ? "Status: Active" : "Status: Inactive";
            u.ButtonText = u.IsActive ? "Deactivate" : "Activate";
            u.StatusColorHex = u.IsActive ? "#4CAF50" : "#F44336";
        }

        // ✅ Updated Logic: Allow Admin and Director to edit all users except themselves
        bool CanEditUser(User target)
        {
            if (_currentUsername == target.Username)
                return false; // Cannot edit self

            if (_userRole == "Administrator")
                return true; // Admins can edit anyone (except self above)

            if (_userRole == "Director")
                return target.Role is "Inspector" or "Secretary";

            return false; // All other roles can't edit users
        }

        void OnSearchTextChanged(object s, TextChangedEventArgs e) => ApplyFilter(e.NewTextValue);
        void OnSortOptionChanged(object s, EventArgs e) => ApplyFilter();

        void ApplyFilter(string query = "")
        {
            query = query?.ToLower() ?? "";

            var result = _master.Where(u =>
                u.FullName?.ToLower().Contains(query) == true ||
                u.Username?.ToLower().Contains(query) == true);

            result = SortPicker.SelectedIndex switch
            {
                0 => result.OrderBy(u => u.Role),
                1 => result.OrderByDescending(u => u.IsActive),
                _ => result
            };

            _view.Clear();
            foreach (var u in result)
            {
                u.CanEdit = CanEditUser(u); // Re-evaluate after filtering
                _view.Add(u);
            }
        }

        async void OnToggleStatusClicked(object s, EventArgs e)
        {
            if ((s as Button)?.CommandParameter is not User u) return;

            string action = u.IsActive ? "deactivate" : "activate";
            bool ok = await DisplayAlert("Confirm",
                         $"Are you sure you want to {action} “{u.Username}”?",
                         "Yes", "No");
            if (!ok) return;

            try
            {
                // Check internet connection first
                if (!Connectivity.Current.NetworkAccess.HasFlag(NetworkAccess.Internet))
                {
                    await DisplayAlert("No Internet",
                        "You need an active internet connection to change user status.",
                        "OK");
                    return;
                }

                // Flip status
                u.IsActive = !u.IsActive;
                UpdateUiProps(u);

                // Save local
                await App.Database.SaveUserAsync(u);

                // Save remote (Firebase)
                await _firebaseAuthService.UpdateUserAsync(u);

                // Audit log (local + remote)
                var log = new AuditLog
                {
                    Username = _currentUsername,
                    Role = _userRole,
                    Action = $"{(u.IsActive ? "Activated" : "Deactivated")} user {u.Username}",
                    Timestamp = DateTime.Now
                };


                await App.Database.SaveAuditLogAsync(log);
                await App.Firebase.LogAuditAsync(log.Action, log.Username, log.Role, "User toggle status");

                ApplyFilter();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    "Failed to update user status. Please check your internet and try again.",
                    "OK");
            }
        }

        public async Task LoadUsersFromFirebaseAsync()
        {
            try
            {
                _master.Clear();

                // Use your injected FirebaseAuthService
                var allUsers = await _firebaseAuthService.GetUsersAsync();

                foreach (var u in allUsers)
                {
                    UpdateUiProps(u);
                    u.CanEdit = CanEditUser(u);
                    _master.Add(u);
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load users: {ex.Message}", "OK");
            }
        }



        async void OnEditUserClicked(object s, EventArgs e)
        {
            if ((s as Button)?.CommandParameter is not User u) return;

            if (!CanEditUser(u))
            {
                await DisplayAlert("Permission Denied", "You are not allowed to edit this user.", "OK");
                return;
            }

            string fn = await DisplayPromptAsync("Full Name", "New name", initialValue: u.FullName);
            string em = await DisplayPromptAsync("Email", "New email", initialValue: u.Email);
            string[] roles = { "Inspector", "Secretary", "Director", "Administrator" };
            string rl = await DisplayActionSheet("Role", "Cancel", null, roles);

            if (string.IsNullOrWhiteSpace(fn) || string.IsNullOrWhiteSpace(em) || !roles.Contains(rl)) return;

            // 🔹 Check for internet before proceeding
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await DisplayAlert("No Internet", "You need an active internet connection to edit a user.", "OK");
                return;
            }

            // Update user locally
            u.FullName = fn;
            u.Email = em;
            u.Role = rl;
            await App.Database.SaveUserAsync(u);

            try
            {
                // Save to Firebase
                await _firebaseAuthService.UpdateUserAsync(u);

                // Save audit log
                await App.Database.SaveAuditLogAsync(new AuditLog
                {
                    Username = _currentUsername,
                    Role = _userRole,
                    Action = $"Edited user {u.Username} (New role: {rl})",
                    Timestamp = DateTime.Now
                });

                ApplyFilter();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to update user: {ex.Message}", "OK");
            }
        }



        async void OnExportPdfClicked(object s, EventArgs e)
        {
            var toExport = _view.Any() ? _view : _master;
            await ExportHelper.ExportUsersToPdfAsync(toExport.Cast<User>(), "Users");
        }

        async void OnExportExcelClicked(object s, EventArgs e)
        {
            var toExport = _view.Any() ? _view : _master;
            await ExportHelper.ExportUsersToExcelAsync(toExport.Cast<User>(), "Users");
        }

        // Firebase listener update
        private async void OnFirebaseUsersChanged(object sender, EventArgs e)
        {
            await LoadUsersAsync();
        }
    }
}
