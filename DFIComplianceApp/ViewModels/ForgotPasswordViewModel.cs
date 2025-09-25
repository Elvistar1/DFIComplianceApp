using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DFIComplianceApp.Services;
using Microsoft.Maui.Dispatching;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace DFIComplianceApp.ViewModels;

public partial class ForgotPasswordViewModel : ObservableObject
{
    private readonly FirebaseAuthService _firebaseAuth;
    public ICommand DismissKeyboardCommand { get; }

    public ForgotPasswordViewModel(FirebaseAuthService firebaseAuth)
    {
        _firebaseAuth = firebaseAuth;
        DismissKeyboardCommand = new RelayCommand(DismissKeyboard);
    }

    private void DismissKeyboard()
    {
        var currentPage = Application.Current?.MainPage;
        currentPage?.Focus(); // Unfocus to dismiss keyboard
    }

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand(CanExecute = nameof(CanReset))]
    private async Task ResetAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ResetCommand.NotifyCanExecuteChanged();

        try
        {
            string trimmedEmail = Email?.Trim().ToLowerInvariant() ?? "";

            if (string.IsNullOrWhiteSpace(trimmedEmail))
            {
                await ShowAlertAsync("Missing Email", "Please enter your email address.");
                return;
            }

            if (!IsValidEmail(trimmedEmail))
            {
                await ShowAlertAsync("Invalid Format", "Please enter a valid email address.");
                return;
            }

            // Firebase reset call
            await _firebaseAuth.SendPasswordResetEmailAsync(trimmedEmail);

            await ShowToastAsync("✅ Reset link sent to your email.");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current.MainPage.Navigation.PopAsync();
            });
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"Could not send reset: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            ResetCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanReset() => !IsBusy;

    private bool IsValidEmail(string email)
    {
        string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
    }

    private async Task ShowAlertAsync(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Application.Current.MainPage.DisplayAlert(title, message, "OK");
        });
    }

    private async Task ShowToastAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Toast.Make(message, ToastDuration.Short).Show();
        });
    }
}
