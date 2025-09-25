using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DFIComplianceApp.Services;
using Microsoft.Maui.Dispatching;
using System.Threading.Tasks;

namespace DFIComplianceApp.ViewModels;

public partial class ChangePasswordViewModel : ObservableObject
{
    // ───────── Bindable Properties ─────────
    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;

    private readonly FirebaseAuthService _authService;
    private INavigation? _navigation;

    // ───────── Constructor ─────────
    public ChangePasswordViewModel(FirebaseAuthService authService)
    {
        _authService = authService;
    }

    // ───────── Initialize Navigation ─────────
    public void InitNavigation(INavigation nav) => _navigation = nav;

    // ───────── Change Password Command ─────────
    [RelayCommand]
    private async Task SaveAsync()
    {
        // Validate passwords
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword != ConfirmPassword)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current.MainPage.DisplayAlert("Error",
                    "Passwords do not match.", "OK"));
            return;
        }

        // Ensure user is logged in and IdToken is available
        if (string.IsNullOrEmpty(_authService.IdToken))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current.MainPage.DisplayAlert("Error",
                    "No logged-in user session.", "OK"));
            return;
        }

        // Attempt to change password via Firebase REST API
        bool success = await _authService.ChangePasswordAsync(NewPassword);

        if (!success)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current.MainPage.DisplayAlert("Error",
                    "Failed to change password. Please try again.", "OK"));
            return;
        }

        // Notify user and navigate back
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Application.Current.MainPage.DisplayAlert("Success",
                "Password updated successfully.", "OK");

            if (_navigation is not null)
                await _navigation.PopAsync();
        });
    }
}
