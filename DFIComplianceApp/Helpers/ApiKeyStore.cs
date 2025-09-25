// Helpers/ApiKeyStore.cs
using Microsoft.Maui.Storage;
using System.Threading.Tasks;

namespace DFIComplianceApp.Helpers;

public static class ApiKeyStore
{
    private const string KeyName = "OpenAIKey";

    public static async Task<string?> GetAsync()
        => await SecureStorage.GetAsync(KeyName);

    public static async Task SaveAsync(string key)
        => await SecureStorage.SetAsync(KeyName, key);

    public static async Task ClearAsync()
        => SecureStorage.Remove(KeyName);
}
