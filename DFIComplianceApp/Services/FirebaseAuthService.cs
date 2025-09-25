using DFIComplianceApp.Models;
using Microsoft.Maui.ApplicationModel; // For MainThread
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DFIComplianceApp.Services
{
    public class FirebaseAuthService : IFirebaseAuthService
    {
        private readonly string _databaseUrl;
        private readonly string _apiKey = "AIzaSyCQua2YURmDm2-noUZrMu23ZZIF_c63vEQ"; // 🔑 replace in production
        private readonly HttpClient _httpClient = new();
        public string? IdToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? LocalId { get; set; }
        private readonly AppDatabase _appDatabase;
        private Task? _listenerTask;
        private bool _listening;
        public event EventHandler? AuditLogsChanged;
        public event EventHandler? CompaniesChanged;
        // Add this field at the top of your class
        private readonly string _projectId = "dficomplianceapp-default-rtdb"; // <-- Set your actual Firebase project ID here

        private FirebaseRealtimeListener? _companyListener;
        private FirebaseRealtimeListener? _auditLogListener;
        private FirebaseRealtimeListener? _pendingReportListener;
        private CancellationTokenSource? _auditLogsCts;
        private bool _listeningAuditLogs;

        // alias for backward compatibility
        public Task<List<Company>> GetCompaniesAsync() => GetCompaniesSafeAsync();

        public event EventHandler? UsersChanged;
        public event EventHandler<PendingAiReport>? PendingReportChanged;
        public event EventHandler? PendingReportsChanged;

        public FirebaseAuthService(string? databaseUrl = null)
        {
            _databaseUrl = databaseUrl ?? "https://dficomplianceapp-default-rtdb.firebaseio.com";
        }

        public string? AuthToken => IdToken;

        // ────────────── AUTH ──────────────
        public async Task<User?> LoginAsync(string email, string password)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";
            var payload = new { email, password, returnSecureToken = true };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            LocalId = doc.RootElement.GetProperty("localId").GetString();
            IdToken = doc.RootElement.GetProperty("idToken").GetString();
            RefreshToken = doc.RootElement.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;

            if (string.IsNullOrEmpty(LocalId) || string.IsNullOrEmpty(IdToken))
                return null;

            var existingUser = await GetUserByIdAsync(LocalId);
            if (existingUser != null)
            {
                existingUser.IdToken = IdToken;
                return existingUser;
            }

            var newUser = new User
            {
                DFIUserId = LocalId,
                Email = email,
                FullName = "Default",
                Username = email.Split('@')[0],
                Role = "Administrator",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                IdToken = IdToken
            };

            await PushUserAsync(newUser);
            return newUser;
        }
        public async Task<AppSettings?> GetAppSettingsAsync()
        {
            string url = $"{_databaseUrl}/settings.json?auth={AuthToken}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            // 🔹 If Firebase has OpenRouterKey, sync back into SecureStorage
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("OpenRouterKey", out var keyElem))
            {
                string key = keyElem.GetString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    await SecureStorage.SetAsync("OpenRouterKey", key);
                }
            }

            return settings;
        }

        // 1. Get all pending (unsent) outbox messages from Firebase
        public async Task<List<OutboxMessage>> GetPendingOutboxAsync()
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/outboxMessages.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url), userErrorMessage: "Unable to connect to server. Please check your internet connection.");

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return new List<OutboxMessage>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, OutboxMessage>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Only return unsent messages
            return dict?.Values.Where(m => m.IsSent == false).OrderBy(m => m.CreatedAt).ToList() ?? new List<OutboxMessage>();
        }

        // 2. Mark an outbox message as sent in Firebase
        public async Task<bool> MarkOutboxSentAsync(Guid firebaseId, DateTime sentAt)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/outboxMessages/{firebaseId}.json?auth={IdToken}";

            // Patch only the IsSent and SentAt fields
            var patch = new { IsSent = true, SentAt = sentAt };
            var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
            var response = await _httpClient.PatchAsync(url, content);

            return response.IsSuccessStatusCode;
        }

        // 3. Delete an outbox message from Firebase
        public async Task<bool> DeleteOutboxAsync(OutboxMessage message)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/outboxMessages/{message.FirebaseId}.json?auth={IdToken}";
            var response = await _httpClient.DeleteAsync(url);

            return response.IsSuccessStatusCode;
        }

        public async Task SaveAppSettingsAsync(AppSettings settings)
        {
            // 🔹 Save base settings (MaintenanceMode, etc.)
            string url = $"{_databaseUrl}/settings.json?auth={AuthToken}";
            var json = JsonSerializer.Serialize(settings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();

            // 🔹 Save OpenRouter API Key if present in SecureStorage
            var openRouterKey = await SecureStorage.GetAsync("OpenRouterKey");
            if (!string.IsNullOrWhiteSpace(openRouterKey))
            {
                string keyUrl = $"{_databaseUrl}/settings/OpenRouterKey.json?auth={AuthToken}";
                var keyContent = new StringContent(JsonSerializer.Serialize(openRouterKey), Encoding.UTF8, "application/json");

                var keyResponse = await _httpClient.PutAsync(keyUrl, keyContent);
                keyResponse.EnsureSuccessStatusCode();
            }
        }
        public async Task<Dictionary<string, List<CompanyRenewal>>> GetAllCompanyRenewalsAsync()
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/renewals.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(
    () => _httpClient.GetStringAsync(url),
    userErrorMessage: "Unable to connect to server. Please check your internet connection."
);

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return new Dictionary<string, List<CompanyRenewal>>();

            var rawDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, CompanyRenewal>>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var result = new Dictionary<string, List<CompanyRenewal>>();
            if (rawDict != null)
            {
                foreach (var kvp in rawDict)
                    result[kvp.Key] = kvp.Value?.Values.ToList() ?? new List<CompanyRenewal>();
            }
            return result;
        }
        public async Task<List<Inspection>> GetInspectionsByCompanyIdAsync(Guid companyId)
        {
            var all = await GetInspectionsAsync();
            return all.Where(i => i.CompanyId == companyId).ToList();
        }

        public async Task<List<CompanyRenewal>> GetCompanyRenewalsByCompanyIdAsync(Guid companyId)
        {
            return await GetCompanyRenewalsAsync(companyId.ToString());
        }

        public Task StartCompanyListener()
        {
            if (_companyListener != null)
                return Task.CompletedTask; // already listening

            string url = $"{_databaseUrl}/companies.json?auth={IdToken}";
            _companyListener = new FirebaseRealtimeListener(url);

            _companyListener.OnDataChanged += async json =>
            {
                CompaniesChanged?.Invoke(this, EventArgs.Empty);
            };

            return Task.Run(() => _companyListener.StartListeningAsync(CancellationToken.None));
        }


        public void StopCompanyListener()
        {
            if (_companyListener != null)
            {
                _companyListener.StopListening();
                _companyListener = null;
            }
        }
        public Task LogoutAsync() => Task.CompletedTask;

        public async Task UpdateUserAsync(User user)
        {
            if (string.IsNullOrEmpty(user.DFIUserId))
                throw new ArgumentException("User must have a valid Firebase ID");

            var url = $"{_databaseUrl}/users/{user.DFIUserId}.json?auth={AuthToken}";
            var json = JsonSerializer.Serialize(user);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task SetUserActiveStatusAsync(string userId, bool isActive)
        {
            var url = $"{_databaseUrl}/users/{userId}/IsActive.json?auth={AuthToken}";
            var json = JsonSerializer.Serialize(isActive);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<FirebaseRegisterResult?> RegisterUserAsync(string email, string password)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_apiKey}";
            var payload = new { email, password, returnSecureToken = true };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            string? localId = doc.RootElement.TryGetProperty("localId", out var l) ? l.GetString() : null;
            string? idToken = doc.RootElement.TryGetProperty("idToken", out var t) ? t.GetString() : null;

            if (string.IsNullOrEmpty(localId) || string.IsNullOrEmpty(idToken))
                return null;

            return new FirebaseRegisterResult { LocalId = localId, IdToken = idToken };
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_apiKey}";
            var payload = new { requestType = "PASSWORD_RESET", email };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }

        public async Task<Dictionary<string, Company>> GetCompaniesFromFirebaseAsync()
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.GetStringAsync("https://your-app.firebaseio.com/companies.json");

                if (string.IsNullOrWhiteSpace(response) || response == "null")
                    return new Dictionary<string, Company>();

                // ✅ Case-insensitive mapping (fixes PascalCase vs camelCase issue)
                var companiesDict = JsonSerializer.Deserialize<Dictionary<string, Company>>(response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return companiesDict ?? new Dictionary<string, Company>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching companies: {ex.Message}");
                return new Dictionary<string, Company>();
            }
        }
        // Fetch all risk prediction history from Firebase
        public async Task<List<RiskPredictionHistory>> GetAllRiskPredictionHistoryAsync()
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/RiskPredictionHistory.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url), userErrorMessage: "Unable to connect to server. Please check your internet connection.");

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return new List<RiskPredictionHistory>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, RiskPredictionHistory>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return dict?.Values.ToList() ?? new List<RiskPredictionHistory>();
        }

        // Insert a new prediction record
        public async Task InsertRiskPredictionHistoryAsync(RiskPredictionHistory record)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/RiskPredictionHistory.json?auth={IdToken}";
            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }
        public async Task UpdateCompanyAsync(Company company)
        {
            if (company == null || string.IsNullOrWhiteSpace(company.RecordId))
                throw new ArgumentException("Company must have a valid RecordId.");

            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/companies/{company.RecordId}.json?auth={IdToken}";
            var json = JsonSerializer.Serialize(company, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
        }


        public async Task<bool> PushCompanyRenewalAsync(string companyId, CompanyRenewal renewal)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            try
            {
                // Generate a unique key (like Firebase push)
                var key = Guid.NewGuid().ToString("N");

                var url = $"{_databaseUrl}/renewals/{companyId}/{key}.json?auth={IdToken}";
                var json = JsonSerializer.Serialize(renewal);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error pushing renewal for {companyId}: {ex.Message}");
                return false;
            }
        }

        // ────────────── COMPANY ──────────────
        public async Task PushCompanyAsync(Company company)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));
            if (string.IsNullOrWhiteSpace(company.RecordId))
                company.RecordId = Guid.NewGuid().ToString();

            var recordId = company.RecordId;
            var fileNo = company.FileNumber ?? "";
            var certNo = company.CertificateNumber ?? "";

            var payload = new Dictionary<string, object?>
            {
                [$"/companies/{recordId}"] = company
            };

            if (!string.IsNullOrWhiteSpace(fileNo))
                payload[$"/byFileNumber/{fileNo}"] = new { recordId };

            if (!string.IsNullOrWhiteSpace(certNo))
                payload[$"/byCertificateNumber/{certNo}"] = new { recordId };

            var rootUrl = $"{_databaseUrl}/.json?auth={AuthToken}";
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PatchAsync(rootUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to push company: {response.ReasonPhrase} - {error}");
                throw new HttpRequestException($"Failed to push company: {response.ReasonPhrase}");
            }

            company.IsSynced = true;
            await App.Database.SaveCompanyAsync(company);
        }
        public async Task<List<Company>> GetCompaniesSafeAsync()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                Debug.WriteLine("⚠️ No AuthToken available. User not logged in.");
                return new List<Company>();
            }

            int retries = 3;
            while (retries > 0)
            {
                try
                {
                    var url = $"{_databaseUrl}/companies.json?auth={AuthToken}";
                    var response = await ExecuteWithRetryAsync(
                        () => _httpClient.GetStringAsync(url),
                        userErrorMessage: "Unable to connect to server. Please check your internet connection."
);

                    Debug.WriteLine($"🔥 Firebase raw companies response: {response}");

                    if (string.IsNullOrWhiteSpace(response) || response == "null")
                        return new List<Company>();

                    var companiesDict = JsonSerializer.Deserialize<Dictionary<string, Company>>(response,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    var result = companiesDict?.Values
                        .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Name))
                        .ToList() ?? new List<Company>();

                    Debug.WriteLine($"✅ Parsed {result.Count} companies from Firebase.");
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Error fetching companies: {ex.Message}");
                    retries--;
                    await Task.Delay(1000);
                }
            }

            return new List<Company>();
        }

        // ────────────── USER CRUD ──────────────
        public async Task<User?> GetUserProfileAsync(string userId)
        {
            var response = await _httpClient.GetStringAsync($"{_databaseUrl}/users/{userId}.json");
            if (string.IsNullOrWhiteSpace(response) || response == "null") return null;

            return JsonSerializer.Deserialize<User>(response);
        }

        public async Task<List<AuditLog>> GetAuditLogsSafeAsync()
        {
            if (string.IsNullOrEmpty(IdToken))
            {
                Console.WriteLine("⚠️ No IdToken found, cannot fetch audit logs.");
                return new List<AuditLog>();
            }

            var url = $"{_databaseUrl}/auditLogs.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url), userErrorMessage: "Unable to connect to server. Please check your internet connection.");
            Console.WriteLine($"AuditLog raw response: {response}");

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return new List<AuditLog>();

            try
            {
                var logsDict = JsonSerializer.Deserialize<Dictionary<string, AuditLog>>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return logsDict?.Values
                    .Where(log => log != null && !string.IsNullOrWhiteSpace(log.Action))
                    .OrderByDescending(log => log.Timestamp)
                    .ToList() ?? new List<AuditLog>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error parsing logs: {ex.Message}");
                return new List<AuditLog>();
            }
        }

        public async Task<bool> ChangePasswordAsync(string newPassword)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in first.");

            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={_apiKey}";
            var payload = new { idToken = IdToken, password = newPassword, returnSecureToken = true };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("idToken", out var newIdToken))
                IdToken = newIdToken.GetString();
            if (doc.RootElement.TryGetProperty("refreshToken", out var newRefresh))
                RefreshToken = newRefresh.GetString();
            if (doc.RootElement.TryGetProperty("localId", out var newLocalId))
                LocalId = newLocalId.GetString();

            return true;
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            var response = await _httpClient.GetStringAsync($"{_databaseUrl}/users.json");
            var usersDict = JsonSerializer.Deserialize<Dictionary<string, User>>(response);
            return usersDict?.Values.ToList() ?? new List<User>();
        }

        public async Task<User?> GetUserByIdAsync(string userId) => await GetUserProfileAsync(userId);

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                var users = await GetAllUsersAsync();
                return users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching user by username: {ex.Message}");
                return null;
            }
        }

        public async Task SaveOrUpdateAiReportAsync(AIReport report)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            if (report == null || report.Id == Guid.Empty)
                throw new ArgumentException("AIReport must have a valid Id.");

            try
            {
                var url = $"{_databaseUrl}/aiReports/{report.Id}.json?auth={IdToken}";
                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PutAsync(url, content); // ✅ Send to Firebase
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving AI report: {ex.Message}");
            }
        }



        public async Task PushUserAsync(User user)
        {
            var content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json");
            await _httpClient.PutAsync($"{_databaseUrl}/users/{user.DFIUserId}.json", content);
        }

        public async Task UpdateUserRoleAsync(string userId, string newRole)
        {
            var patch = new { Role = newRole };
            var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
            await _httpClient.PatchAsync($"{_databaseUrl}/users/{userId}.json", content);
        }

        // ────────────── REALTIME ──────────────
        public void StartUserListener()
        {
            _listening = true;
            _listenerTask = ListenForUserChangesAsync();
        }

        public void StopUserListener() => _listening = false;

        private async Task ListenForUserChangesAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_databaseUrl}/users.json");
            request.Headers.Accept.Add(new("text/event-stream"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            while (_listening && !reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("data:"))
                    UsersChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public Task<List<User>> GetUsersAsync()
        {
            return GetAllUsersAsync(); // reuse your existing method
        }


        public async Task SyncPendingCompaniesAsync()
        {
            var pending = await App.Database.GetUnsyncedCompaniesAsync();
            foreach (var company in pending)
            {
                try
                {
                    await PushCompanyAsync(company);
                    company.IsSynced = true;
                    await App.Database.SaveCompanyAsync(company);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Sync failed for {company.Name}: {ex.Message}");
                }
            }
        }

        // ------------------ AUDIT LOG STREAMING WITH CANCELLATION -------------------------
        public void StartAuditLogListener()
        {
            if (_auditLogListener != null)
                return; // Already listening

            _auditLogListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/auditLogs.json");
            _auditLogListener.OnDataChanged += async json =>
            {
                // You can deserialize and process logs here if needed
                AuditLogsChanged?.Invoke(this, EventArgs.Empty);
            };
            Task.Run(() => _auditLogListener.StartListeningAsync(CancellationToken.None));
        }



        public void StopAuditLogListener()
        {
            if (_auditLogListener != null)
            {
                _auditLogListener.StopListening();
                _auditLogListener = null;
            }
        }

        // ------------------ Audit Log Push Helper -------------------------
        public async Task PushAuditLogAsync(AuditLog log)
        {
            // ✅ Use the global token stored at login
            var token = App.FirebaseIdToken;

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("User must be logged in to push audit logs.");

            string url = $"{_databaseUrl}/auditLogs.json?auth={token}";
            var json = JsonSerializer.Serialize(log, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Failed to push audit log: {response.StatusCode} - {error}");
            }
            else
            {
                AuditLogsChanged?.Invoke(this, EventArgs.Empty);
                Console.WriteLine("✅ Audit log pushed successfully.");
            }
        }

        public async Task SaveReportToFirebaseAsync(AiReportJson report)
        {
            string url = $"{_databaseUrl}/reports.json?auth={IdToken}";
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Failed to save report: {response.StatusCode} - {error}");
            }
            else
            {
                Console.WriteLine("✅ Report saved successfully to Firebase");
            }
        }

        public async Task<List<AiReportJson>> GetReportsAsync()
        {
            string url = $"{_databaseUrl}/reports.json?auth={IdToken}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Failed to fetch reports: {response.StatusCode} - {error}");
                return new List<AiReportJson>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var reportsDict = JsonSerializer.Deserialize<Dictionary<string, AiReportJson>>(json);

            return reportsDict?.Values.ToList() ?? new List<AiReportJson>();
        }

        public async Task<List<CompanyRenewal>> GetCompanyRenewalsAsync(string companyId)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            try
            {
                var url = $"{_databaseUrl}/renewals/{companyId}.json?auth={IdToken}";
                var response = await ExecuteWithRetryAsync(
    () => _httpClient.GetStringAsync(url),
    userErrorMessage: "Unable to connect to server. Please check your internet connection."
);

                if (string.IsNullOrWhiteSpace(response) || response == "null")
                    return new List<CompanyRenewal>();

                var renewalsDict = JsonSerializer.Deserialize<Dictionary<string, CompanyRenewal>>(response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return renewalsDict?.Values
                    .Where(r => r != null && r.RenewalYear > 0)
                    .OrderByDescending(r => r.RenewalYear)
                    .ToList() ?? new List<CompanyRenewal>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching renewals for {companyId}: {ex.Message}");
                return new List<CompanyRenewal>();
            }
        }
        public async Task<AIReport?> GetAIReportByInspectionIdAsync(Guid inspectionId)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            try
            {
                var url = $"{_databaseUrl}/aiReports.json?auth={IdToken}";
                var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url), userErrorMessage: "Unable to connect to server. Please check your internet connection.");

                if (string.IsNullOrWhiteSpace(response) || response == "null")
                    return null;

                var dict = JsonSerializer.Deserialize<Dictionary<string, AIReport>>(response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return dict?.Values.FirstOrDefault(r => r.InspectionId == inspectionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching AIReport: {ex.Message}");
                return null;
            }
        }
        public async Task<bool> SaveAIReportAsync(AIReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.Id == Guid.Empty)
                report.Id = Guid.NewGuid();

            try
            {
                if (string.IsNullOrEmpty(IdToken))
                    throw new InvalidOperationException("User must be logged in to save reports to Firebase.");

                // 1. Save to Firebase (central source of truth)
                var url = $"{_databaseUrl}/aiReports/{report.Id}.json?auth={IdToken}";
                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Failed to save AIReport to Firebase: {response.StatusCode} - {error}");
                    return false;
                }

                // ✅ No local cache — Firebase is enough
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving AIReport: {ex.Message}");
                return false;
            }
        }
        public async Task<List<AIReport>> GetAIReportsAsync()
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/aiReports.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url), userErrorMessage: "Unable to connect to server. Please check your internet connection.");

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return new List<AIReport>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, AIReport>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return dict?.Values.ToList() ?? new List<AIReport>();
        }

        public async Task<bool> SaveInspectionAnswerAsync(InspectionAnswer answer)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            if (answer == null) throw new ArgumentNullException(nameof(answer));
            if (answer.Id == Guid.Empty)
                answer.Id = Guid.NewGuid();

            try
            {
                var url = $"{_databaseUrl}/inspectionAnswers/{answer.Id}.json?auth={IdToken}";
                var json = JsonSerializer.Serialize(answer, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Failed to save InspectionAnswer: {response.StatusCode} - {error}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving InspectionAnswer: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> SaveInspectionAsync(Inspection inspection)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            if (inspection == null) throw new ArgumentNullException(nameof(inspection));
            if (inspection.Id == Guid.Empty)
                inspection.Id = Guid.NewGuid();

            try
            {
                var url = $"{_databaseUrl}/inspections/{inspection.Id}.json?auth={IdToken}";
                var json = JsonSerializer.Serialize(inspection, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Failed to save Inspection: {response.StatusCode} - {error}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving Inspection: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> SaveInspectionPhotoAsync(
      string inspectionAnswerId,
      string photoFileName,
      byte[]? photoBytes,
      string localPath,
      string? base64 = null
  )
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            if (string.IsNullOrWhiteSpace(inspectionAnswerId))
                throw new ArgumentException("Inspection ID is required.", nameof(inspectionAnswerId));

            try
            {
                var photo = new InspectionPhoto
                {
                    Id = Guid.NewGuid(),
                    InspectionAnswerId = Guid.Parse(inspectionAnswerId),
                    FileName = photoFileName,
                    LocalPath = localPath,
                    UploadedAt = DateTime.UtcNow,
                    PhotoBase64 = base64,
                    DownloadUrl = "", // Not used for Base64-only storage
                    LastModifiedUtc = DateTime.UtcNow,
                    IsDirty = false,
                    IsDeleted = false
                };

                // Save directly under inspectionPhotos/{answerId}/{photoId}
                var dbUrl = $"{_databaseUrl}/inspectionPhotos/{inspectionAnswerId}/{photo.Id}.json?auth={IdToken}";
                var json = JsonSerializer.Serialize(photo, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var dbContent = new StringContent(json, Encoding.UTF8, "application/json");
                var dbResponse = await _httpClient.PutAsync(dbUrl, dbContent);

                if (!dbResponse.IsSuccessStatusCode)
                {
                    var error = await dbResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Failed to save Base64 photo in DB: {dbResponse.StatusCode} - {error}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving Base64 inspection photo: {ex.Message}");
                return false;
            }
        }


        public async Task<List<Inspection>> GetInspectionsAsync()
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/inspections.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url), userErrorMessage: "Unable to connect to server. Please check your internet connection.");

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return new List<Inspection>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, Inspection>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return dict?.Values.ToList() ?? new List<Inspection>();
        }
        public async Task SaveScheduledInspectionAsync(ScheduledInspection inspection)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/scheduledInspections/{inspection.Id}.json?auth={IdToken}";
            var json = JsonSerializer.Serialize(inspection);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content);

            response.EnsureSuccessStatusCode();
        }
        // Fetch all answers for a specific inspection
        public async Task<List<InspectionAnswer>> GetAnswersForInspectionAsync(Guid inspectionId)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/inspectionAnswers.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url), userErrorMessage: "Unable to connect to server. Please check your internet connection.");

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return new List<InspectionAnswer>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, InspectionAnswer>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return dict?.Values.Where(a => a.InspectionId == inspectionId).ToList() ?? new List<InspectionAnswer>();
        }

        // Fetch all photos for a specific answer
        public async Task<List<InspectionPhoto>> GetPhotosForAnswerAsync(Guid answerId)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/inspectionPhotos/{answerId}.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(
     () => _httpClient.GetStringAsync(url),
     userErrorMessage: "Unable to connect to server. Please check your internet connection."
 );

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return new List<InspectionPhoto>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, InspectionPhoto>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return dict?.Values.ToList() ?? new List<InspectionPhoto>();
        }




        public async Task<bool> SavePendingReportAsync(PendingAiReport report)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            if (report == null)
                throw new ArgumentNullException(nameof(report));

            try
            {
                // Use existing Id if present, otherwise generate a new one
                var key = !string.IsNullOrWhiteSpace(report.Id) ? report.Id : Guid.NewGuid().ToString("N");
                report.Id = key; // Ensure the model's Id matches the Firebase key

                var url = $"{_databaseUrl}/pendingAiReports/{key}.json?auth={IdToken}";

                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving pending report: {ex.Message}");
                return false;
            }
        }
        // In FirebaseAuthService
        public async Task<List<ScheduledInspection>> GetAllScheduledInspectionsAsync()
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            try
            {
                var url = $"{_databaseUrl}/scheduledInspections.json?auth={IdToken}";
                var response = await ExecuteWithRetryAsync(
    () => _httpClient.GetStringAsync(url),
    userErrorMessage: "Unable to connect to server. Please check your internet connection."
);

                if (string.IsNullOrWhiteSpace(response) || response == "null")
                    return new List<ScheduledInspection>();

                var dict = JsonSerializer.Deserialize<Dictionary<string, ScheduledInspection>>(response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return dict?.Values.ToList() ?? new List<ScheduledInspection>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching scheduled inspections: {ex.Message}");
                return new List<ScheduledInspection>();
            }
        }
        // In FirebaseAuthService.cs
        public async Task<Inspection?> GetInspectionByIdAsync(Guid inspectionId)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/inspections/{inspectionId}.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(
    () => _httpClient.GetStringAsync(url),
    userErrorMessage: "Unable to connect to server. Please check your internet connection."
);

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return null;

            return JsonSerializer.Deserialize<Inspection>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        public async Task<List<PendingAiReport>> GetPendingReportsAsync()
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            try
            {
                var url = $"{_databaseUrl}/pendingAiReports.json?auth={IdToken}";
                var response = await ExecuteWithRetryAsync(
    () => _httpClient.GetStringAsync(url),
    userErrorMessage: "Unable to connect to server. Please check your internet connection."
);

                if (string.IsNullOrWhiteSpace(response) || response == "null")
                    return new List<PendingAiReport>();

                var dict = JsonSerializer.Deserialize<Dictionary<string, PendingAiReport>>(response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return dict?.Values.ToList() ?? new List<PendingAiReport>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching pending reports: {ex.Message}");
                return new List<PendingAiReport>();
            }
        }
        public async Task<bool> SaveOutboxAsync(OutboxMessage message)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            // Use FirebaseId for online storage
            if (message.FirebaseId == Guid.Empty)
                message.FirebaseId = Guid.NewGuid();

            try
            {
                var url = $"{_databaseUrl}/outboxMessages/{message.FirebaseId}.json?auth={IdToken}";
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving outbox message: {ex.Message}");
                return false;
            }
        }



        public async Task<bool> SaveOutboxAsync(PendingAiReport report)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            if (report == null)
                throw new ArgumentNullException(nameof(report));

            try
            {
                // Generate unique key for Firebase
                var key = Guid.NewGuid().ToString("N");
                var url = $"{_databaseUrl}/outboxReports/{key}.json?auth={IdToken}";

                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving outbox report: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> UpdatePendingReportAsync(PendingAiReport report)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            if (report == null)
                throw new ArgumentNullException(nameof(report));

            if (string.IsNullOrEmpty(report.Id))
                throw new ArgumentException("Report must have a valid Id.");

            if (report.InspectionId == Guid.Empty)
                throw new InvalidOperationException("Cannot update PendingAiReport: InspectionId is empty.");

            try
            {
                var url = $"{_databaseUrl}/pendingAiReports/{report.Id}.json?auth={IdToken}";
                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating pending report: {ex.Message}");
                return false;
            }
        }

        // ────────────── LOGIN + TOKEN HELPER ──────────────
        public async Task<(User? user, string? idToken)> LoginAndGetTokenAsync(string email, string password)
        {
            var user = await LoginAsync(email, password);
            if (user == null || string.IsNullOrEmpty(IdToken))
                return (null, null);

            return (user, IdToken);
        }



        // ------------------ Log Audit Helper -------------------------
        public async Task LogAuditAsync(string action, string username, string role, string details = "")
        {
            var log = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                Username = username,
                Role = role,
                Action = action,
                Details = details,
                PerformedBy = username
            };
            await PushAuditLogAsync(log);
        }
        // Add these methods to your FirebaseAuthService class

        public async Task<bool> SaveAppointmentAsync(Appointment appointment)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            if (appointment == null)
                throw new ArgumentNullException(nameof(appointment));

            // Use Guid for unique key if not set
            if (appointment.Id == Guid.Empty)
                appointment.Id = Guid.NewGuid();

            try
            {
                var url = $"{_databaseUrl}/appointments/{appointment.Id}.json?auth={IdToken}";
                var json = JsonSerializer.Serialize(appointment, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving appointment: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Appointment>> GetAppointmentsAsync()
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/appointments.json?auth={IdToken}";
            var response = await ExecuteWithRetryAsync(
    () => _httpClient.GetStringAsync(url),
    userErrorMessage: "Unable to connect to server. Please check your internet connection."
);

            if (string.IsNullOrWhiteSpace(response) || response == "null")
                return new List<Appointment>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, Appointment>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return dict?.Values.ToList() ?? new List<Appointment>();
        }
        public void StartPendingReportListener()
        {
            if (_pendingReportListener != null) return; // Already listening

            string url = $"{_databaseUrl}/pendingAiReports.json?auth={IdToken}";
            _pendingReportListener = new FirebaseRealtimeListener(url);

            _pendingReportListener.OnDataChanged += async json =>
            {
                PendingReportsChanged?.Invoke(this, EventArgs.Empty);
            };

            Task.Run(() => _pendingReportListener.StartListeningAsync(CancellationToken.None));
        }

        public void StopPendingReportListener()
        {
            if (_pendingReportListener != null)
            {
                _pendingReportListener.StopListening();
                _pendingReportListener = null;
            }
        }

        public async Task<PendingAiReport?> GetPendingReportByIdAsync(Guid reportId)
        {
            if (string.IsNullOrEmpty(IdToken))
                throw new InvalidOperationException("User must be logged in.");

            var url = $"{_databaseUrl}/pendingAiReports/{reportId}.json?auth={IdToken}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            return JsonSerializer.Deserialize<PendingAiReport>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        // Add this helper method to your FirebaseAuthService class
        private async Task<T?> ExecuteWithRetryAsync<T>(
            Func<Task<T>> networkCall,
            int maxAttempts = 3,
            int delayMs = 2000,
            string? userErrorMessage = null)
        {
            int attempt = 0;
            Exception? lastException = null;
            while (attempt < maxAttempts)
            {
                try
                {
                    return await networkCall();
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    attempt++;
                    Debug.WriteLine($"Network error: {ex.Message}");
                    if (attempt < maxAttempts)
                        await Task.Delay(delayMs * attempt); // Exponential backoff
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempt++;
                    Debug.WriteLine($"Unexpected error: {ex.Message}");
                    if (attempt < maxAttempts)
                        await Task.Delay(delayMs * attempt);
                }
            }

            // Optionally notify the user (on UI thread)
            if (!string.IsNullOrWhiteSpace(userErrorMessage))
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Application.Current.MainPage.DisplayAlert(
                        "Network Error",
                        userErrorMessage,
                        "OK"
                    ));
            }

            return default;
        }
    }
}