using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using DFIComplianceApp.Views;
using Microsoft.Maui.Dispatching;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;

namespace DFIComplianceApp.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly INavigation _navigation;
    private readonly FirebaseAuthService _firebaseAuth;
    private readonly IAppDatabase _db; // keep only for audit logs & settings

    public bool IsNotBusy => !IsBusy;

    // ───────── Bindable Properties ─────────
    [ObservableProperty] private string? username;
    [ObservableProperty] private string? password;
    [ObservableProperty] private string? selectedRole;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool rememberMe;
    [ObservableProperty] private bool isPasswordHidden = true;

    public ObservableCollection<string> Roles { get; } =
        new(new[] { "Administrator", "Director", "Secretary", "Inspector" });

    public LoginViewModel(INavigation navigation, FirebaseAuthService firebaseAuth, IAppDatabase db)
    {
        _navigation = navigation;
        _firebaseAuth = firebaseAuth;
        _db = db;

        MainThread.BeginInvokeOnMainThread(async () => await LoadSavedLoginAsync());
    }

    public ICommand TogglePasswordVisibilityCommand => new Command(() =>
    {
        IsPasswordHidden = !IsPasswordHidden;
        OnPropertyChanged(nameof(PasswordToggleIcon));
    });

    public string PasswordToggleIcon => IsPasswordHidden ? "eye.png" : "eye_off.png";

    // ───────── Commands ─────────
    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            if (string.IsNullOrWhiteSpace(SelectedRole) ||
                string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert("Missing Info", "Please fill in all login fields.", "OK");

                var audit = new AuditLog
                {
                    Username = Username ?? "Unknown",
                    Role = SelectedRole ?? "Unknown",
                    Action = "Login Failed - Missing fields",
                    Timestamp = DateTime.UtcNow
                };
                await _db.SaveAuditLogAsync(audit);
                await SafeFirebaseAudit(audit);
                return;
            }

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await Application.Current.MainPage.DisplayAlert("No Internet", "Internet connection is required.", "OK");

                var audit = new AuditLog
                {
                    Username = Username,
                    Role = SelectedRole,
                    Action = "Login Failed - No Internet",
                    Timestamp = DateTime.UtcNow
                };
                await _db.SaveAuditLogAsync(audit);
                await SafeFirebaseAudit(audit);
                return;
            }

            // 🔑 Step 1: Firebase Login (get user + ID token)
            var (user, idToken) = await _firebaseAuth.LoginAndGetTokenAsync(Username.Trim(), Password.Trim());
            if (user == null || string.IsNullOrEmpty(idToken))
            {
                await Application.Current.MainPage.DisplayAlert("Login Failed", "Invalid email or password.", "OK");

                var audit = new AuditLog
                {
                    Username = Username,
                    Role = SelectedRole,
                    Action = "Login Failed - Invalid credentials",
                    Timestamp = DateTime.UtcNow
                };
                await _db.SaveAuditLogAsync(audit);
                await SafeFirebaseAudit(audit);
                return;
            }

            // 🔒 Save ID token globally so all Firebase calls use it
            App.FirebaseIdToken = idToken;

            // Save user snapshot locally
            await _db.SaveUserAsync(user);

            // ───────── Business Logic Checks ─────────
            if (!user.IsActive)
            {
                await Application.Current.MainPage.DisplayAlert("Inactive", "Account is inactive. Contact an administrator.", "OK");

                var audit = new AuditLog
                {
                    Username = user.Username,
                    Role = user.Role,
                    Action = "Login Failed - Inactive account",
                    Timestamp = DateTime.UtcNow
                };
                await _db.SaveAuditLogAsync(audit);
                await SafeFirebaseAudit(audit);
                return;
            }

            if (!string.Equals(user.Role, SelectedRole, StringComparison.OrdinalIgnoreCase))
            {
                await Application.Current.MainPage.DisplayAlert("Wrong Role", $"Select '{user.Role}' in the Role picker.", "OK");

                var audit = new AuditLog
                {
                    Username = user.Username,
                    Role = SelectedRole,
                    Action = $"Login Failed - Role mismatch (Selected: {SelectedRole}, Actual: {user.Role})",
                    Timestamp = DateTime.UtcNow
                };
                await _db.SaveAuditLogAsync(audit);
                await SafeFirebaseAudit(audit);
                return;
            }

            var settings = await _db.GetAppSettingsAsync();
            if (settings?.MaintenanceMode == true &&
                !string.Equals(user.Role, "Administrator", StringComparison.OrdinalIgnoreCase))
            {
                await Application.Current.MainPage.DisplayAlert("Maintenance Mode", "🚧 The system is under maintenance.", "OK");

                var audit = new AuditLog
                {
                    Username = user.Username,
                    Role = user.Role,
                    Action = "Login Blocked - Maintenance Mode",
                    Timestamp = DateTime.UtcNow
                };
                await _db.SaveAuditLogAsync(audit);
                await SafeFirebaseAudit(audit);
                return;
            }

            // ───────── Log success ─────────
            
            var successAudit = new AuditLog
            {
                Username = user.Username,
                Role = user.Role,
                Action = "Login Success",
                Timestamp = DateTime.UtcNow
            };
            await _db.SaveAuditLogAsync(successAudit);
            await SafeFirebaseAudit(successAudit);

            // ✅ CanEdit flag
            user.CanEdit = string.Equals(user.Role, "Administrator", StringComparison.OrdinalIgnoreCase);

            // RememberMe
            if (RememberMe)
            {
                await SecureStorage.SetAsync("last_username", Username);
                await SecureStorage.SetAsync("last_role", SelectedRole);
            }
            else
            {
                SecureStorage.Remove("last_username");
                SecureStorage.Remove("last_role");
            }

            App.CurrentUser = user;

            await Application.Current.MainPage.DisplayAlert("Login Successful", $"Welcome {user.FullName}!", "OK");

            // ───────── Navigate to Role Dashboard ─────────
            Page dashboardPage = user.Role switch
            {
                "Administrator" => new AdministratorDashboardPage(user.Username),
                "Director" => new DirectorDashboardPage(user.Username),
                "Inspector" => new InspectorDashboardPage(user.Username),
                "Secretary" => new SecretaryDashboardPage(user.Username),
                _ => null!
            };

            if (dashboardPage != null)
                await MainThread.InvokeOnMainThreadAsync(() => _navigation.PushAsync(dashboardPage));
            else
                await Application.Current.MainPage.DisplayAlert("Error", "No dashboard found for this role.", "OK");
        }
        catch (HttpRequestException ex)
        {
            await Application.Current.MainPage.DisplayAlert("Network Error", "Unable to reach authentication server. Please check your internet connection.", "OK");

            var audit = new AuditLog
            {
                Username = Username,
                Role = SelectedRole,
                Action = $"Login Failed - Network error ({ex.Message})",
                Timestamp = DateTime.UtcNow
            };
            await _db.SaveAuditLogAsync(audit);
            await SafeFirebaseAudit(audit);
        }
        finally
        {
            Password = null;
            IsBusy = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }


    /// <summary>
    /// Helper to log to Firebase safely without crashing
    /// </summary>
    private async Task SafeFirebaseAudit(AuditLog log)
    {
        try
        {
            await App.Firebase.LogAuditAsync(log.Action, log.Username, log.Role, $"Login audit: {log.Action}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Firebase audit log failed: {ex.Message}");
        }
    }



    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        try
        {
            var page = App.Services.GetService<ForgotPasswordPage>();
            if (page == null)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "ForgotPasswordPage not found.", "OK");
                return;
            }
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current.MainPage.Navigation.PushAsync(page);
            });
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK");
        }
    }

    private async Task LoadSavedLoginAsync()
    {
        try
        {
            var savedUsername = await SecureStorage.GetAsync("last_username");
            var savedRole = await SecureStorage.GetAsync("last_role");

            if (!string.IsNullOrWhiteSpace(savedUsername))
                Username = savedUsername;

            if (!string.IsNullOrWhiteSpace(savedRole))
                SelectedRole = savedRole;
        }
        catch
        {
            // ignore errors
        }
    }
}
