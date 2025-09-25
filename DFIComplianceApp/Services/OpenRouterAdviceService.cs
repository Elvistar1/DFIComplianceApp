using Microsoft.Maui.Storage;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DFIComplianceApp.Services;

public sealed class OpenRouterAdviceService : IAdviceService
{
    private readonly HttpClient _http;
    private readonly IExpertSystemService _expert;
    private string _apiKey = "";

    public OpenRouterAdviceService(HttpClient http, IExpertSystemService expert)
    {
        _http = http;
        _expert = expert;

        // 🔹 Preload key at startup so first call doesn’t fail
        Task.Run(RefreshKeyAsync);
    }

    public async Task RefreshKeyAsync()
    {
        _apiKey = await SecureStorage.GetAsync("OpenRouterKey") ?? "";
    }

    public async Task<string> GetAdviceAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            await RefreshKeyAsync();

        if (string.IsNullOrWhiteSpace(_apiKey))
            return await _expert.GetDetailedAdviceAsync(input, ct);

        try
        {
            var result = await CallOpenRouterAsync(input, ct);
            if (string.IsNullOrWhiteSpace(result) || result.StartsWith("[Error]"))
                return await _expert.GetDetailedAdviceAsync(input, ct);

            return result;
        }
        catch
        {
            return await _expert.GetDetailedAdviceAsync(input, ct);
        }
    }

    private async Task<string> CallOpenRouterAsync(string input, CancellationToken ct)
    {
        var payload = new
        {
            model = "anthropic/claude-3-haiku",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content =
@"You are a senior compliance officer for Ghana's Factories, Offices and Shops Act (Act 328), 1970.
When given inspection checklist data in JSON format, write a formal advisory letter to the company director.

The letter must include:
- Reference number placeholder (e.g., Ref No: .............)
- Date placeholder (e.g., Date: .............)
- Formal salutation: 'Dear Sir/Madam,'
- Introductory paragraph referring to the inspection conducted
- Detailed findings and legal advice in full sentences and paragraph format (no bullet points)
- Closing paragraph with 'Yours faithfully' and Inspector signature placeholder.

Use formal, polite, professional English suitable for government correspondence in Ghana. Do not copy back the raw checklist JSON."
                },
                new { role = "user", content = input }
            }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", $"Bearer {_apiKey}");

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString()
                   ?? "[Error] Empty response";
        }
        catch
        {
            return "[Error] Could not parse OpenRouter response";
        }
    }
}
