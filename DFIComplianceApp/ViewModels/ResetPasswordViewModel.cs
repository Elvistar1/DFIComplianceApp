using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DFIComplianceApp.Services;
using Microsoft.Maui.Dispatching;
using DFIComplianceApp.ViewModels;

namespace DFIComplianceApp.ViewModels;

public partial class ResetPasswordViewModel : ObservableObject
{
    private readonly IPasswordResetService _reset;
    private INavigation? _navigation;
    public void InitNavigation(INavigation nav) => _navigation = nav;


    public ResetPasswordViewModel(IPasswordResetService reset)
    {
        _reset = reset;
    }

    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private bool isBusy;

    // Set by ResetPasswordPage via QueryProperty
    public string Token { get; set; } = string.Empty;

    public bool CanSave => !IsBusy;

   
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(NewPassword) ||
            NewPassword != ConfirmPassword)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current.MainPage.DisplayAlert(
                    "Error", "Passwords do not match.", "OK"));
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _reset.SetNewPasswordAsync(Token, NewPassword);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (result.Success) // ✅ FIXED here
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Success", "Password reset. Please log in.", "OK");
                    await Application.Current.MainPage.Navigation.PopToRootAsync();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error", result.Message ?? "Invalid or expired token.", "OK");
                }
            });

        }
        finally
        {
            IsBusy = false;
        }
    }

}
