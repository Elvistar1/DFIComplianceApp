using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;

namespace DFIComplianceApp.Views
{
    public partial class SystemSettingsPage : ContentPage
    {
        private AppSettings _settings = new();
        private readonly string _currentUsername;
        private readonly string _currentRole;

        private readonly IFirebaseAuthService _authService;

        public SystemSettingsPage(string currentUsername = "Director", string currentRole = "Director")
        {
            InitializeComponent();
            _currentUsername = currentUsername;
            _currentRole = currentRole;

            _authService = App.Services.GetRequiredService<IFirebaseAuthService>();

            _ = LoadSettingsAsync();
        }

        /* ───────── load current settings ───────── */
        private async Task LoadSettingsAsync()
        {
            try
            {
                ShowLoading(true, "Loading settings...");

                _settings = await _authService.GetAppSettingsAsync() ?? new AppSettings();

                MaintenanceSwitch.IsToggled = _settings.MaintenanceMode;

                if (!string.IsNullOrWhiteSpace(_settings.OpenRouterKey))
                {
                    await SecureStorage.SetAsync("OpenRouterKey", _settings.OpenRouterKey);
                    OpenAIKeyEntry.Text = "••••••••"; // mask
                }
                else if (!string.IsNullOrWhiteSpace(await SecureStorage.GetAsync("OpenRouterKey")))
                {
                    OpenAIKeyEntry.Text = "••••••••";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Unable to read settings.\n{ex.Message}", "OK");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!string.Equals(App.CurrentUser?.Role, "Administrator", StringComparison.OrdinalIgnoreCase))
            {
                if (await App.CheckMaintenanceModeAsync(App.CurrentUser?.Role))
                {
                    await Navigation.PopAsync();
                }
            }
        }

        /* ───────── save button ───────── */
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            string key = OpenAIKeyEntry.Text?.Trim() ?? "";
            bool keyLooksMasked = key.StartsWith("•");

            if (!keyLooksMasked && key.Length > 0 && key.Length < 20)
            {
                await DisplayAlert("Validation", "That doesn’t look like a valid OpenRouter API key.", "OK");
                return;
            }

            try
            {
                ShowLoading(true, "Saving settings...");

                _settings.MaintenanceMode = MaintenanceSwitch.IsToggled;

                if (!keyLooksMasked && key.StartsWith("sk-"))
                {
                    _settings.OpenRouterKey = key;
                    await SecureStorage.SetAsync("OpenRouterKey", key);

                    if (App.Services.GetService<IAdviceService>() is OpenRouterAdviceService routerSvc)
                        await routerSvc.RefreshKeyAsync();
                }

                await _authService.SaveAppSettingsAsync(_settings);

                await _authService.PushAuditLogAsync(new AuditLog
                {
                    Username = _currentUsername,
                    Role = _currentRole,
                    Action = "Updated system settings (incl. OpenRouter key)",
                    Timestamp = DateTime.UtcNow
                });

                await Toast.Make("✅ Settings saved", ToastDuration.Short).Show();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Save failed.\n{ex.Message}", "OK");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        /* ───────── helper to show/hide inline loading ───────── */
        private void ShowLoading(bool isLoading, string message = "")
        {
            LoadingOverlay.IsVisible = isLoading;
            MainGrid.IsEnabled = !isLoading;
            LoadingLabel.Text = message;
        }
    }
}
