using System;
using System.Threading;
using System.Threading.Tasks;

namespace DFIComplianceApp.Services
{
    public class EmailBackgroundService : IAsyncDisposable
    {
        private readonly EmailReceiverService _emailReceiver;
        private readonly PeriodicTimer _timer;
        private CancellationTokenSource _cts = new();

        public EmailBackgroundService(EmailReceiverService emailReceiver, TimeSpan? interval = null)
        {
            _emailReceiver = emailReceiver;
            _timer = new PeriodicTimer(interval ?? TimeSpan.FromMinutes(5)); // default every 5 min
            _ = RunAsync(_cts.Token);
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (await _timer.WaitForNextTickAsync(token))
                {
                    await _emailReceiver.CheckNewEmailsAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Timer cancelled - do nothing
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _timer.Dispose();
            _cts.Dispose();
            await Task.CompletedTask;
        }
    }
}
