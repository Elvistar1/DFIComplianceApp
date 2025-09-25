using DFIComplianceApp.Helpers;
using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Views;

public partial class ApiKeySettingsPage : ContentPage
{
    public ApiKeySettingsPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        KeyEntry.Text = await ApiKeyStore.GetAsync() ?? string.Empty;
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        string key = KeyEntry.Text?.Trim() ?? "";
        if (!key.StartsWith("sk-"))
        {
            await DisplayAlert("Key", "That doesn't look like an OpenAI key.", "OK");
            return;
        }

        await ApiKeyStore.SaveAsync(key);
        StatusLabel.Text = "? Key saved securely.";
        StatusLabel.IsVisible = true;
    }

    private async void OnClearClicked(object s, EventArgs e)
    {
        await ApiKeyStore.ClearAsync();
        KeyEntry.Text = "";
        StatusLabel.Text = "?? Key cleared.";
        StatusLabel.IsVisible = true;
    }
}
