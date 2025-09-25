using Microsoft.Maui.Networking;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace DFIComplianceApp.Services;

public sealed class OutboxBackgroundFlusher : IAsyncDisposable
{
    private readonly OutboxSyncService _flusher;
    private readonly PeriodicTimer _timer;

    public OutboxBackgroundFlusher(OutboxSyncService flusher)
    {
        _flusher = flusher;

        // ❶ auto‑flush every 3 minutes
        _timer = new PeriodicTimer(TimeSpan.FromMinutes(3));
        _ = LoopAsync();

        // ❷ flush when network comes back
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? s, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
            await _flusher.FlushAsync();
    }

    private async Task LoopAsync()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            await _flusher.FlushAsync();
        }
    }

    public ValueTask DisposeAsync()
    {
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _timer.Dispose();
        return ValueTask.CompletedTask;
    }
}
