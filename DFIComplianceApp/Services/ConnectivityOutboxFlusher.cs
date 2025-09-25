using Microsoft.Maui.Networking;
using System.Threading.Tasks;

namespace DFIComplianceApp.Services;

public sealed class ConnectivityOutboxFlusher
{
    private readonly OutboxSyncService _flusher;

    public ConnectivityOutboxFlusher(OutboxSyncService flusher)
    {
        _flusher = flusher;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? s, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
            await _flusher.FlushAsync();
    }
}
