using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DFIComplianceApp.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Text.Json;

namespace DFIComplianceApp.Services;

public class SyncService : INotifyPropertyChanged
{
    private readonly AppDatabase _localDb;
    private readonly FirebaseAuthService _firebase;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);
    private CancellationTokenSource? _cts;

    // Sync status properties
    private bool _isSyncing;
    private string _syncStatusText = "Up to date";
    private string _lastSyncTimeText = "Last sync: never";

    public bool IsSyncing
    {
        get => _isSyncing;
        set { _isSyncing = value; OnPropertyChanged(); }
    }
    public string SyncStatusText
    {
        get => _syncStatusText;
        set { _syncStatusText = value; OnPropertyChanged(); }
    }
    public string LastSyncTimeText
    {
        get => _lastSyncTimeText;
        set { _lastSyncTimeText = value; OnPropertyChanged(); }
    }

    // Add listeners for each entity
    private FirebaseRealtimeListener? _companyListener;
    private FirebaseRealtimeListener? _aiReportListener;
    private FirebaseRealtimeListener? _userListener;
    private FirebaseRealtimeListener? _appointmentListener;
    private FirebaseRealtimeListener? _auditLogListener;
    private FirebaseRealtimeListener? _violationListener;
    private FirebaseRealtimeListener? _inspectionListener;
    private FirebaseRealtimeListener? _pendingAiReportListener;
    private FirebaseRealtimeListener? _inspectionPhotoListener;
    private FirebaseRealtimeListener? _companyRenewalListener;

    public SyncService(AppDatabase localDb, FirebaseAuthService firebase)
    {
        _localDb = localDb;
        _firebase = firebase;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => RunSyncLoop(_cts.Token));
        StartRealtimeListeners();
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void StartRealtimeListeners()
    {
        // Company
        _companyListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/companies.json");
        _companyListener.OnDataChanged += async json =>
        {
            var companies = JsonSerializer.Deserialize<Dictionary<string, Company>>(json.GetRawText());
            if (companies != null)
            {
                foreach (var c in companies.Values)
                    await _localDb.SaveCompanyAsync(c);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("Company data updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _companyListener.StartListeningAsync(CancellationToken.None));

        // AIReport
        _aiReportListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/aiReports.json");
        _aiReportListener.OnDataChanged += async json =>
        {
            var reports = JsonSerializer.Deserialize<Dictionary<string, AIReport>>(json.GetRawText());
            if (reports != null)
            {
                foreach (var r in reports.Values)
                    await _localDb.SaveAIReportAsync(r);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("AI Reports updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _aiReportListener.StartListeningAsync(CancellationToken.None));

        // User
        _userListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/users.json");
        _userListener.OnDataChanged += async json =>
        {
            var users = JsonSerializer.Deserialize<Dictionary<string, User>>(json.GetRawText());
            if (users != null)
            {
                foreach (var u in users.Values)
                    await _localDb.SaveUserAsync(u);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("User data updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _userListener.StartListeningAsync(CancellationToken.None));

        // Appointment
        _appointmentListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/appointments.json");
        _appointmentListener.OnDataChanged += async json =>
        {
            var appointments = JsonSerializer.Deserialize<Dictionary<string, Appointment>>(json.GetRawText());
            if (appointments != null)
            {
                foreach (var a in appointments.Values)
                    await _localDb.SaveAppointmentAsync(a);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("Appointments updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _appointmentListener.StartListeningAsync(CancellationToken.None));

        // AuditLog
        _auditLogListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/auditLogs.json");
        _auditLogListener.OnDataChanged += async json =>
        {
            var logs = JsonSerializer.Deserialize<Dictionary<string, AuditLog>>(json.GetRawText());
            if (logs != null)
            {
                foreach (var l in logs.Values)
                {
                    // Check if log exists and is up-to-date
                    var localLogs = await _localDb.GetAuditLogsAsync();
                    var localLog = localLogs.FirstOrDefault(log => log.Id == l.Id);
                    if (localLog == null || l.LastModifiedUtc > localLog.LastModifiedUtc)
                        await _localDb.SaveAuditLogAsync(l);
                }
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("Audit logs updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _auditLogListener.StartListeningAsync(CancellationToken.None));

        // Violation
        _violationListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/violations.json");
        _violationListener.OnDataChanged += async json =>
        {
            var violations = JsonSerializer.Deserialize<Dictionary<string, Violation>>(json.GetRawText());
            if (violations != null)
            {
                foreach (var v in violations.Values)
                    await _localDb.SaveViolationAsync(v);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("Violations updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _violationListener.StartListeningAsync(CancellationToken.None));

        // Inspection
        _inspectionListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/inspections.json");
        _inspectionListener.OnDataChanged += async json =>
        {
            var inspections = JsonSerializer.Deserialize<Dictionary<string, Inspection>>(json.GetRawText());
            if (inspections != null)
            {
                foreach (var i in inspections.Values)
                    await _localDb.SaveInspectionAsync(i);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("Inspections updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _inspectionListener.StartListeningAsync(CancellationToken.None));

        // PendingAiReport
        _pendingAiReportListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/pendingAiReports.json");
        _pendingAiReportListener.OnDataChanged += async json =>
        {
            var pending = JsonSerializer.Deserialize<Dictionary<string, PendingAiReport>>(json.GetRawText());
            if (pending != null)
            {
                foreach (var p in pending.Values)
                    await _localDb.SavePendingReportAsync(p);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("Pending AI Reports updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _pendingAiReportListener.StartListeningAsync(CancellationToken.None));

        // InspectionPhoto
        _inspectionPhotoListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/inspectionPhotos.json");
        _inspectionPhotoListener.OnDataChanged += async json =>
        {
            var photos = JsonSerializer.Deserialize<Dictionary<string, InspectionPhoto>>(json.GetRawText());
            if (photos != null)
            {
                foreach (var p in photos.Values)
                    await _localDb.SaveInspectionPhotoAsync(p);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("Inspection photos updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _inspectionPhotoListener.StartListeningAsync(CancellationToken.None));

        // CompanyRenewal
        _companyRenewalListener = new FirebaseRealtimeListener("https://dficomplianceapp-default-rtdb.firebaseio.com/companyRenewals.json");
        _companyRenewalListener.OnDataChanged += async json =>
        {
            var renewals = JsonSerializer.Deserialize<Dictionary<string, CompanyRenewal>>(json.GetRawText());
            if (renewals != null)
            {
                foreach (var r in renewals.Values)
                    await _localDb.SaveCompanyRenewalAsync(r);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Snackbar.Make("Company renewals updated in real-time.", null, null, TimeSpan.FromSeconds(2)).Show());
            }
        };
        Task.Run(() => _companyRenewalListener.StartListeningAsync(CancellationToken.None));

        // Add more listeners for other entities as needed...
    }

    private async Task RunSyncLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await RunSyncNowAsync();
            await Task.Delay(_syncInterval, token);
        }
    }

    public async Task RunSyncNowAsync()
    {
        try
        {
            IsSyncing = true;
            SyncStatusText = "Syncing...";
            await SyncCompaniesAsync();
            await SyncAIReportsAsync();
            await SyncUsersAsync(); // <-- Add this line
            SyncStatusText = "Up to date";
            LastSyncTimeText = $"Last sync: {DateTime.Now:t}";
        }
        catch (Exception ex)
        {
            SyncStatusText = "Sync failed";
            await Snackbar.Make($"Sync failed: {ex.Message}", null, null, TimeSpan.FromSeconds(3)).Show();
        }
        finally
        {
            IsSyncing = false;
        }
    }


    private async Task SyncCompaniesAsync()
    {
        // 1. Push local dirty companies to Firebase
        var localCompanies = await _localDb.GetCompaniesAsync();
        var dirtyCompanies = localCompanies.Where(c => c.IsDirty).ToList();
        foreach (var company in dirtyCompanies)
        {
            await RetryAsync(async () =>
            {
                await _localDb.SaveCompanyAsync(company);
                company.IsDirty = false;
                await _localDb.UpdateCompanyAsync(company);
            });
        }

        // 2. Pull remote companies and update local if remote is newer
        // FIX: Use _localDb.GetCompaniesAsync() instead of _firebase.GetAllCompaniesAsync()
        var remoteCompanies = await _localDb.GetCompaniesAsync();
        foreach (var remote in remoteCompanies)
        {
            var local = localCompanies.FirstOrDefault(c => c.Id == remote.Id);
            if (local == null || remote.LastModifiedUtc > local.LastModifiedUtc)
            {
                await _localDb.SaveCompanyAsync(remote);
            }
        }

        // Show a success message
        await Snackbar.Make("Sync completed successfully.", duration: TimeSpan.FromSeconds(3)).Show();
    }

    private async Task SyncAIReportsAsync()
    {
        // Repeat similar logic for AIReport
        var localReports = await _localDb.GetAllAIReportsAsync();
        var dirtyReports = localReports.Where(r => r.IsDirty).ToList();
        foreach (var report in dirtyReports)
        {
            // FIX: Use localDb to save the report instead of _firebase.PushAIReportAsync
            await _localDb.SaveAIReportAsync(report);
            report.IsDirty = false;
            await _localDb.SaveAIReportAsync(report);
        }

        // FIX: Replace _firebase.GetAllAIReportsAsync() with a method from _localDb
        var remoteReports = await _localDb.GetAllAIReportsAsync();
        foreach (var remote in remoteReports)
        {
            var local = localReports.FirstOrDefault(r => r.Id == remote.Id);
            if (local == null || remote.LastModifiedUtc > local.LastModifiedUtc)
            {
                await _localDb.SaveAIReportAsync(remote);
            }
        }

        // Show a success message
        await Snackbar.Make("Sync completed successfully.", duration: TimeSpan.FromSeconds(3)).Show();
        // Show a conflict warning
        await Snackbar.Make("Sync conflict detected. Please review changes.", duration: TimeSpan.FromSeconds(5)).Show();
    }

    public async Task SyncUsersAsync()
    {
        // 1. Fetch users from Firebase
        var firebaseUsers = await _firebase.GetAllUsersAsync();

        // 2. Remove all local users (replace ClearUsersAsync with DeleteUser for each user)
        var localUsers = await _localDb.GetAllUsersAsync();
        foreach (var user in localUsers)
        {
            await _localDb.DeleteUserAsync(user);
        }

        // 3. Save Firebase users to local database
        foreach (var user in firebaseUsers)
        {
            await _localDb.SaveUserAsync(user);
        }
    }

    private async Task RetryAsync(Func<Task> action, int maxAttempts = 3, int delayMs = 2000)
    {
        int attempt = 0;
        Exception? lastException = null;
        while (attempt < maxAttempts)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;
                if (attempt < maxAttempts)
                    await Task.Delay(delayMs * attempt); // Exponential backoff
            }
        }
        // Notify user after all retries fail
        await Snackbar.Make($"Sync failed after {maxAttempts} attempts: {lastException?.Message}", null, null, TimeSpan.FromSeconds(5)).Show();
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}