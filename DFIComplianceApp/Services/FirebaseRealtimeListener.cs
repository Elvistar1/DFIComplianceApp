using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
namespace DFIComplianceApp.Services
{
    public class FirebaseRealtimeListener
    {
        private readonly string _url;
        private readonly HttpClient _http;
        private CancellationTokenSource? _cts;

        public event Action<JsonElement>? OnDataChanged;

        public FirebaseRealtimeListener(string url)
        {
            _url = url;
            _http = new HttpClient();
        }

        public async Task StartListeningAsync(CancellationToken token)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            try
            {
                using var response = await _http.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream && !_cts.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:")) continue;

                    var json = line.Substring(5).Trim();
                    if (json == "null") continue;

                    var doc = JsonDocument.Parse(json);
                    OnDataChanged?.Invoke(doc.RootElement);
                }
            }
            catch (OperationCanceledException)
            {
                // Listening was canceled
            }
            catch (Exception ex)
            {
                // Handle other exceptions as needed
                Console.WriteLine($"Error in StartListeningAsync: {ex}");
            }
        }

        public void StopListening()
        {
            _cts?.Cancel();
        }
    }
}