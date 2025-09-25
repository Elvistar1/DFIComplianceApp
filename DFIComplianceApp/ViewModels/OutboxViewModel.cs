using CommunityToolkit.Maui.Alerts;                   // ← Toasts!
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using Microsoft.Maui.Dispatching;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DFIComplianceApp.ViewModels;

public partial class OutboxViewModel : ObservableObject
{
    private readonly IAppDatabase _db;
    private readonly IEmailService _email;

    public ObservableCollection<OutboxMessage> Pending { get; } = new();

    public OutboxViewModel(IAppDatabase db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    // Called from page OnAppearing
    public async Task LoadAsync()
    {
        Pending.Clear();
        foreach (var m in await _db.GetPendingOutboxAsync())
            Pending.Add(m);
    }

    /* ───── Resend ───── */
    [RelayCommand]
    private async Task ResendAsync(OutboxMessage m)
    {
        try
        {
            await _email.SendAsync(m.To, m.Subject, m.Body, m.IsHtml);
            await _db.MarkOutboxSentAsync(m.Id, DateTime.UtcNow);
            Pending.Remove(m);

            // 🎉 Toast on success
            await Toast.Make("E‑mail resent!").Show();
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Resend failed", ex.Message, "OK");
        }
    }

    /* ───── Delete ───── */
    [RelayCommand]
    private async Task DeleteAsync(OutboxMessage m)
    {
        await _db.DeleteOutboxAsync(m);
        Pending.Remove(m);
        await Toast.Make("E‑mail deleted.").Show();
    }
}
