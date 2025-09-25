// =============================================================
//  Services/AppDatabase.cs – updated with ScheduledInspectionHistory
// =============================================================

using DFIComplianceApp.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using MyEmailMessage = DFIComplianceApp.Models.EmailMessage;

namespace DFIComplianceApp.Services;

public sealed class AppDatabase : IAppDatabase
{
    private readonly SQLiteAsyncConnection _db;
    private readonly Task _initTask;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public AppDatabase(string dbPath)
    {
        _db = new SQLiteAsyncConnection(
            dbPath,
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.FullMutex);

        _initTask = InitialiseAsync();
    }

    // ─── Scheduled Inspections ─────────────────────────────
    public async Task<ScheduledInspection?> GetScheduledInspectionByIdAsync(Guid id)
    {
        return await _db.Table<ScheduledInspection>()
                        .Where(s => s.Id == id)
                        .FirstOrDefaultAsync();
    }

    public async Task<int> DeleteScheduledInspectionAsync(ScheduledInspection inspection)
    {
        inspection.IsDeleted = true;
        inspection.IsDirty = true;
        return await _db.UpdateAsync(inspection);
    }

    public async Task<List<ScheduledInspection>> GetDirtyScheduledInspectionsAsync()
    {
        return await _db.Table<ScheduledInspection>()
                        .Where(s => s.IsDirty)
                        .ToListAsync();
    }

    public Task EnsureInitialisedAsync() => _initTask;
    public Task EnsureInitializedAsync() => _initTask;

    private async Task InitialiseAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            var result = await _db.ExecuteScalarAsync<string>("PRAGMA journal_mode=WAL;");
            if (!string.Equals(result?.Trim(), "wal", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Failed to enable WAL mode. Got response: {result}");

            await _db.CreateTableAsync<User>();
            await _db.CreateTableAsync<Appointment>();
            await _db.CreateTableAsync<AuditLog>();
            await _db.CreateTableAsync<Company>();
            await _db.CreateTableAsync<Inspection>();
            await _db.CreateTableAsync<AppSettings>();
            await _db.CreateTableAsync<AIReport>();
            await _db.CreateTableAsync<CompanyRenewal>();
            await _db.CreateTableAsync<InspectionAnswer>();
            await _db.CreateTableAsync<PendingAiReport>();
            await _db.CreateTableAsync<InspectionPhoto>();
            await _db.CreateTableAsync<OutboxMessage>();
            await _db.CreateTableAsync<ScheduledInspection>();
            await _db.CreateTableAsync<ScheduledInspectionHistory>();
            await _db.CreateTableAsync<RiskPredictionHistory>();
            await _db.CreateTableAsync<MyEmailMessage>();

            await _db.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS ux_user_username ON User(Username COLLATE NOCASE);");
            await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_renew_company ON CompanyRenewal(CompanyId);");
            await _db.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_company_fileno ON Company(FileNumber COLLATE NOCASE);");
            await _db.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_company_certno ON Company(CertificateNumber COLLATE NOCASE);");
            await _db.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_inspection_scheduledid ON Inspection(ScheduledInspectionId) WHERE ScheduledInspectionId IS NOT NULL;");

            await _db.CreateTableAsync<Violation>();
            await AddColumnIfMissingAsync("Violation", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Violation", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Violation", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");
            await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_violation_isdirty ON Violation(IsDirty);");

            // 🔹 User migrations
            await AddColumnIfMissingAsync("User", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("User", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("User", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            // 🔹 Appointment migrations
            await AddColumnIfMissingAsync("Appointment", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Appointment", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Appointment", "IsSynced", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Appointment", "CreatedAt", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");
            await AddColumnIfMissingAsync("Appointment", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            // 🔹 AuditLog migrations
            await AddColumnIfMissingAsync("AuditLog", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("AuditLog", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("AuditLog", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            // 🔹 Company migrations
            await AddColumnIfMissingAsync("Company", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Company", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Company", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            // 🔹 Inspection migrations
            await AddColumnIfMissingAsync("Inspection", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Inspection", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Inspection", "IsSynced", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Inspection", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            // Add sync columns to all tables that need to be syncable

            // User
            await AddColumnIfMissingAsync("User", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("User", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("User", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            // Appointment
            await AddColumnIfMissingAsync("Appointment", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Appointment", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Appointment", "IsSynced", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Appointment", "CreatedAt", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");
            await AddColumnIfMissingAsync("Appointment", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            // AuditLog
            await AddColumnIfMissingAsync("AuditLog", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("AuditLog", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("AuditLog", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            // Company
            await AddColumnIfMissingAsync("Company", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Company", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Company", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            // Inspection
            await AddColumnIfMissingAsync("Inspection", "IsDirty", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Inspection", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Inspection", "IsSynced", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync("Inspection", "LastModifiedUtc", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");

            await RemoveDuplicateUsersAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task SaveEmailAsync(MyEmailMessage email)
    {
        var existing = await _db.Table<MyEmailMessage>()
            .Where(e => e.GmailMessageId == email.GmailMessageId)
            .FirstOrDefaultAsync();

        if (existing == null)
            await _db.InsertAsync(email);
        else
        {
            email.Id = existing.Id;  // keep the same local PK
            await _db.UpdateAsync(email);
        }
    }

    // ─── InspectionPhotos Helpers ───────────────────────────────
    public async Task<InspectionPhoto?> GetInspectionPhotoByIdAsync(Guid id)
    {
        return await _db.Table<InspectionPhoto>()
                        .Where(p => p.Id == id)
                        .FirstOrDefaultAsync();
    }

    public Task<int> SaveAuditLogAsync(AuditLog log)
    {
        if (log.Id == 0) // AutoIncrement, so new entry
            log.Timestamp = DateTime.UtcNow;

        log.IsDirty = true;
        log.LastModifiedUtc = DateTime.UtcNow;

        return _db.InsertOrReplaceAsync(log);
    }

    public Task<int> DeleteAuditLogAsync(AuditLog log)
    {
        log.IsDeleted = true;
        log.IsDirty = true;
        log.LastModifiedUtc = DateTime.UtcNow;

        return _db.InsertOrReplaceAsync(log);
    }

    // 🔹 Get all non-deleted inspections
    public Task<List<Inspection>> GetInspectionsAsync()
    {
        return _db.Table<Inspection>()
                  .Where(i => !i.IsDeleted)
                  .OrderByDescending(i => i.PlannedDate)
                  .ToListAsync();
    }

    // 🔹 Get inspection by Id
    public async Task<Inspection?> GetInspectionByIdAsync(Guid id)
    {
        return await _db.Table<Inspection>()
                        .Where(i => i.Id == id && !i.IsDeleted)
                        .FirstOrDefaultAsync();
    }

    // 🔹 Fetch settings (ensure one row exists)
    public async Task<AppSettings> GetSettingsAsync()
    {
        var settings = await _db.FindAsync<AppSettings>(1);
        if (settings == null)
        {
            settings = new AppSettings(); // defaults
            await _db.InsertAsync(settings);
        }
        return settings;
    }

    // 🔹 Save settings (always enforce singleton Id = 1)
    public async Task<int> SaveSettingsAsync(AppSettings settings)
    {
        settings.Id = 1;               // enforce singleton
        settings.IsDirty = true;       // mark for sync
        settings.IsDeleted = false;    // settings row should not be deleted
        return await _db.InsertOrReplaceAsync(settings);
    }

    // 🔹 Delete inspection (soft by default)
    public async Task<int> DeleteInspectionAsync(Guid id, bool softDelete = true)
    {
        var inspection = await _db.FindAsync<Inspection>(id);
        if (inspection == null)
            return 0;

        if (softDelete)
        {
            inspection.IsDeleted = true;
            inspection.IsDirty = true;
            inspection.LastModifiedUtc = DateTime.UtcNow;
            return await _db.UpdateAsync(inspection);
        }
        else
        {
            return await _db.DeleteAsync(inspection);
        }
    }

    public Task<List<AuditLog>> GetAuditLogsAsync()
    {
        return _db.Table<AuditLog>()
                  .Where(l => !l.IsDeleted)
                  .OrderByDescending(l => l.Timestamp)
                  .ToListAsync();
    }

    public async Task<List<InspectionPhoto>> GetDirtyInspectionPhotosAsync()
    {
        return await _db.Table<InspectionPhoto>()
                        .Where(p => p.IsDirty)
                        .ToListAsync();
    }

    public Task<int> SaveViolationAsync(Violation v)
    {
        if (v.Id == Guid.Empty) v.Id = Guid.NewGuid();
        v.IsDirty = true;
        v.LastModifiedUtc = DateTime.UtcNow;
        return _db.InsertOrReplaceAsync(v);
    }

    private async Task AddColumnIfMissingAsync(string tableName, string columnName, string columnDefinition)
    {
        // Check if column already exists
        var pragma = await _db.QueryScalarsAsync<string>($"PRAGMA table_info({tableName});");

        bool columnExists = false;
        foreach (var row in await _db.QueryAsync<TableInfo>($"PRAGMA table_info({tableName});"))
        {
            if (row.name?.Equals(columnName, StringComparison.OrdinalIgnoreCase) == true)
            {
                columnExists = true;
                break;
            }
        }

        if (!columnExists)
        {
            await _db.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
        }
    }

    public Task<int> DeleteCompanyAsync(Company company)
    {
        company.IsDeleted = true;
        company.IsDirty = true;
        company.LastModifiedUtc = DateTime.UtcNow;

        return _db.InsertOrReplaceAsync(company);
    }

    public Task<List<Company>> GetCompaniesAsync(bool includeDeleted = false)
    {
        var query = _db.Table<Company>();
        if (!includeDeleted)
            query = query.Where(c => !c.IsDeleted);

        return query.OrderBy(c => c.Name).ToListAsync();
    }

    private class TableInfo
    {
        public int cid { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
        public int notnull { get; set; }
        public string? dflt_value { get; set; }
        public int pk { get; set; }
    }

    public Task<int> SaveAppointmentAsync(Appointment appointment)
    {
        if (appointment.Id == Guid.Empty)
            appointment.Id = Guid.NewGuid();

        appointment.IsDirty = true;
        appointment.LastModifiedUtc = DateTime.UtcNow; // make sure Appointment has this column

        return _db.InsertOrReplaceAsync(appointment);
    }

    public Task<List<Appointment>> GetUnsyncedAppointmentsAsync()
    {
        return _db.Table<Appointment>()
                  .Where(a => a.IsDirty || !a.IsSynced || a.IsDeleted)
                  .ToListAsync();
    }

    public Task<int> DeleteAppointmentAsync(Appointment appointment)
    {
        appointment.IsDeleted = true;
        appointment.IsDirty = true;
        return _db.InsertOrReplaceAsync(appointment);
    }

    public async Task<int> SoftDeleteViolationAsync(Violation v)
    {
        v.IsDeleted = true;
        v.IsDirty = true;
        v.LastModifiedUtc = DateTime.UtcNow;
        return await _db.UpdateAsync(v);
    }

    public Task<List<Violation>> GetDirtyViolationsAsync()
    {
        return _db.Table<Violation>().Where(x => x.IsDirty).ToListAsync();
    }

    public async Task<List<ScheduledInspection>> GetAllScheduledInspectionsAsync()
    {
        await EnsureInitializedAsync();
        return await _db.Table<ScheduledInspection>().ToListAsync();
    }

    public async Task<IEnumerable<MyEmailMessage>> GetAllEmailMessagesAsync()
    {
        return await _db.Table<MyEmailMessage>()
            .OrderByDescending(e => e.ReceivedDate)
            .ToListAsync();
    }

    public async Task<bool> EmailExistsAsync(string gmailMessageId)
    {
        var count = await _db.Table<MyEmailMessage>()
                             .Where(e => e.GmailMessageId == gmailMessageId)
                             .CountAsync();
        return count > 0;
    }

    public Task<List<CompanyRenewalExtended>> GetAllCompanyRenewalsAsync()
    {
        return _db.QueryAsync<CompanyRenewalExtended>(
            @"SELECT r.Id,
                 r.CompanyId,
                 r.RenewalYear,
                 r.RenewalDate,
                 r.RenewedBy,
                 c.Name AS CompanyName,
                 c.Location AS CompanyLocation
          FROM CompanyRenewal r
          INNER JOIN Company c ON r.CompanyId = c.Id
          ORDER BY r.RenewalDate DESC");
    }

    public async Task InsertEmailMessageAsync(MyEmailMessage email)
    {
        await _db.InsertAsync(email);
    }

    public Task<List<Appointment>> GetAppointmentsAsync()
    {
        return _db.Table<Appointment>().ToListAsync();
    }

    public async Task<List<AIReport>> GetAllAIReportsAsync()
    {
        return await _db.Table<AIReport>().ToListAsync();
    }

    public Task<Appointment?> GetAppointmentByIdAsync(Guid id)
    {
        return _db.Table<Appointment>()
                  .Where(a => a.Id == id)
                  .FirstOrDefaultAsync();
    }

    public Task<int> SaveRiskPredictionHistoryAsync(RiskPredictionHistory history)
    {
        return _db.InsertAsync(history);
    }

    public async Task MoveApprovedOrExpiredInspectionsToHistoryAsync()
    {
        var today = DateTime.Today;
        var scheduled = await _db.Table<ScheduledInspection>().ToListAsync();
        var reports = await _db.Table<AIReport>().ToListAsync();

        foreach (var s in scheduled)
        {
            bool isExpired = s.ScheduledDate < today;
            bool isCompleted = reports.Any(r => r.InspectionId == s.Id && r.Status == "Approved");

            if (isExpired || isCompleted)
            {
                string inspectorUsernames = "(unknown)";
                try
                {
                    var ids = string.IsNullOrWhiteSpace(s.InspectorIdsJson)
                              ? new List<string>()
                              : JsonSerializer.Deserialize<List<string>>(s.InspectorIdsJson) ?? new List<string>();

                    var allUsers = await _db.Table<User>().ToListAsync();
                    var names = allUsers.Where(u => ids.Contains(u.Id.ToString()))
                                        .Select(u => u.Username)
                                        .ToList();

                    inspectorUsernames = names.Count > 0 ? string.Join(", ", names) : "(unknown)";
                }
                catch
                {
                    inspectorUsernames = "(unknown)";
                }

                var history = new ScheduledInspectionHistory
                {
                    CompanyId = s.CompanyId,
                    CompanyName = s.CompanyName,
                    InspectorUsername = inspectorUsernames,
                    ScheduledDate = s.ScheduledDate,
                    Notes = s.Notes,
                    Status = isExpired ? "Expired" : "Completed",
                    ArchivedAt = DateTime.UtcNow
                };

                await _db.InsertAsync(history);
                await _db.DeleteAsync(s);
            }
        }
    }

    public async Task<Dictionary<string, int>> GetRiskLevelDistributionAsync(string companyType, string location, int? year)
    {
        await EnsureInitializedAsync();

        var query = _db.Table<RiskPredictionHistory>();

        if (!string.IsNullOrWhiteSpace(companyType))
            query = query.Where(r => r.CompanyType == companyType);

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(r => r.Location == location);

        var allRecords = await query.ToListAsync();

        var latestPerCompany = allRecords
            .Where(r =>
            {
                if (!DateTime.TryParse(r.DatePredicted, out var date)) return false;
                if (year.HasValue && date.Year != year.Value) return false;
                return true;
            })
            .GroupBy(r => r.CompanyId)
            .Select(g =>
                g.OrderByDescending(r =>
                    DateTime.TryParse(r.DatePredicted, out var date) ? date : DateTime.MinValue
                ).First()
            )
            .ToList();

        var distribution = latestPerCompany
            .GroupBy(r => r.RiskLevel)
            .ToDictionary(g => g.Key, g => g.Count());

        return distribution;
    }

    public async Task DeleteAIReportAsync(Guid id)
    {
        await _db.DeleteAsync<AIReport>(id);
    }

    public async Task<List<int>> GetYearsForCompaniesAsync(List<Guid> companyIds)
    {
        var query = @"
        SELECT DISTINCTstrftime('%Y', PlannedDate) as Year
        FROM ScheduledInspection
        WHERE CompanyId IN (" + string.Join(",", companyIds.Select(id => $"'{id}'")) + ")";

        var result = await _db.QueryAsync<SqlYearResult>(query);
        return result.Select(r => int.Parse(r.Year!)).ToList();
    }

    private class SqlYearResult
    {
        public string? Year { get; set; }
    }

    // This one returns List<Company>
    public async Task<List<Company>> GetRiskHistoryCompanyObjectsByTypeAndLocationAsync(string companyType, string location)
    {
        var riskEntries = await _db.Table<RiskPredictionHistory>()
            .Where(r => r.CompanyType == companyType && r.Location == location)
            .ToListAsync();

        var companyNames = riskEntries
            .Select(r => r.CompanyName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();

        var companies = await _db.Table<Company>()
            .Where(c => c.Name != null && companyNames.Contains(c.Name))
            .ToListAsync();

        return companies;
    }

    public async Task<List<Company>> GetCompaniesByTypeAsync(string companyType)
    {
        return await _db.Table<Company>()
            .Where(c => c.NatureOfWork == companyType)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrendDataPoint>> GetRiskPredictionTrendAsync(Guid companyId)
    {
        var history = await _db.Table<RiskPredictionHistory>()
            .Where(h => h.CompanyId == companyId)
            .ToListAsync();

        var validHistory = history
            .Select(h =>
            {
                if (DateTime.TryParse(h.DatePredicted, out var dt))
                    return new { Record = h, Date = dt.Date };
                return null;
            })
            .Where(x => x != null)
            .GroupBy(x => x!.Date)
            .Select(g => new TrendDataPoint
            {
                Date = g.Key,
                RiskLevelCounts = g
                    .GroupBy(x => x!.Record.RiskLevel)
                    .ToDictionary(rl => rl.Key, rl => rl.Count())
            })
            .OrderBy(p => p.Date);

        return validHistory;
    }

    public async Task<List<Inspection>> GetInspectionsByCompanyIdAsync(Guid companyId)
    {
        return await _db.Table<Inspection>()
            .Where(i => i.CompanyId == companyId)
            .ToListAsync();
    }

    public Task<List<CompanyRenewal>> GetRenewalsByCompanyIdAsync(Guid companyId)
    {
        return _db.Table<CompanyRenewal>()
                  .Where(r => r.CompanyId == companyId && !r.IsDeleted)
                  .OrderByDescending(r => r.RenewalYear)
                  .ToListAsync();
    }

    public async Task<List<CompanyRenewal>> GetCompanyRenewalsByCompanyIdAsync(Guid companyId)
    {
        return await _db.Table<CompanyRenewal>()
            .Where(r => r.CompanyId == companyId)
            .ToListAsync();
    }

    public Task<CompanyRenewal?> GetCompanyRenewalByIdAsync(Guid id)
    {
        return _db.Table<CompanyRenewal>()
                  .Where(r => r.Id == id && !r.IsDeleted)
                  .FirstOrDefaultAsync();
    }

    public Task<List<RiskPredictionHistory>> GetAllRiskPredictionHistoryAsync()
    {
        return _db.Table<RiskPredictionHistory>().OrderByDescending(h => h.DatePredicted).ToListAsync();
    }

    public Task<int> InsertRiskPredictionHistoryAsync(RiskPredictionHistory history)
    {
        return _db.InsertAsync(history);
    }

    public Task<List<ScheduledInspectionHistory>> GetScheduledInspectionHistoryAsync()
    {
        return _db.Table<ScheduledInspectionHistory>()
                  .OrderByDescending(h => h.ArchivedAt)
                  .ToListAsync();
    }

    private async Task RemoveDuplicateUsersAsync()
    {
        var dupRows = await _db.QueryAsync<User>(
            @"SELECT * FROM User
              WHERE Id NOT IN (
                  SELECT MIN(Id)
                  FROM User
                  GROUP BY LOWER(Username)
              )");

        foreach (var u in dupRows)
            await _db.DeleteAsync(u);
    }

    public Task<List<User>> GetUsersAsync() => _db.Table<User>().ToListAsync();

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        var norm = username.Trim();
        return await _db.Table<User>()
                        .Where(u => u.Username.ToLower() == norm.ToLower())
                        .FirstOrDefaultAsync();
    }

    public async Task MarkReminderSentAsync(Guid companyId, int year)
    {
        var renewal = await _db.Table<CompanyRenewal>()
            .Where(r => r.CompanyId == companyId && r.RenewalYear == year)
            .FirstOrDefaultAsync();

        if (renewal != null)
        {
            renewal.ReminderSent = true;
            await _db.UpdateAsync(renewal);
        }
    }

    public async Task<int> SaveAIReportAsync(AIReport report)
    {
        if (report.Id == Guid.Empty)
            report.Id = Guid.NewGuid();

        report.LastModifiedUtc = DateTime.UtcNow;
        report.IsDirty = true;

        // Check if already exists
        var existing = await _db.FindAsync<AIReport>(report.Id);
        if (existing != null)
        {
            return await _db.UpdateAsync(report);
        }

        return await _db.InsertAsync(report);
    }

    public async Task<CompanyRenewal?> GetCompanyRenewalAsync(Guid companyId, int year)
    {
        return await _db.Table<CompanyRenewal>()
            .Where(r => r.CompanyId == companyId && r.RenewalYear == year)
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        var norm = email.Trim();
        return await _db.Table<User>()
                        .Where(u => u.Email.ToLower() == norm.ToLower())
                        .FirstOrDefaultAsync();
    }

    public Task<int> GetUsersCountAsync() => _db.Table<User>().CountAsync();

    public async Task<int> SaveUserAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Username))
            throw new ArgumentException("User.Username cannot be null or empty.", nameof(user));

        user.Username = user.Username.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(user.Email))
            user.Email = user.Email.Trim().ToLowerInvariant();

        var existing = await _db.Table<User>()
                            .FirstOrDefaultAsync(u => u.Username == user.Username);

    if (existing != null)
    {
        user.Id = existing.Id;
        user.DFIUserId = existing.DFIUserId;
        return await _db.UpdateAsync(user);
    }

    int result = await _db.InsertAsync(user);

    if (string.IsNullOrWhiteSpace(user.DFIUserId))
    {
        user.DFIUserId = "DFI-" + user.Id.ToString().Substring(0, 8).ToUpper();
        await _db.UpdateAsync(user);
    }

    return result;
    }

    public Task<int> DeleteUserAsync(User user) => _db.DeleteAsync(user);

    public Task<int> GetUnsentOutboxCountAsync() =>
        _db.Table<OutboxMessage>()
           .Where(m => !m.IsSent)
           .CountAsync();

    public Task<List<OutboxMessage>> GetPendingOutboxAsync() =>
        _db.Table<OutboxMessage>()
           .Where(m => !m.IsSent)
           .OrderBy(m => m.CreatedAt)
           .ToListAsync();

    public async Task<DateTime?> GetLastRenewalDateAsync(Guid companyId)
    {
        var latest = await _db.Table<CompanyRenewal>()
            .Where(r => r.CompanyId == companyId)
            .OrderByDescending(r => r.RenewalDate)
            .FirstOrDefaultAsync();

        return latest?.RenewalDate;
    }

    public Task<int> MarkOutboxSentAsync(int id, DateTime sentAt) =>
        _db.ExecuteAsync(
            "UPDATE OutboxMessage " +
            "SET IsSent = 1, SentAt = ? " +
            "WHERE Id = ?", sentAt, id);

    public Task<int> SaveOutboxAsync(OutboxMessage msg) => _db.InsertAsync(msg);
    public Task<int> UpdateOutboxAsync(OutboxMessage msg) => _db.UpdateAsync(msg);
    public Task<int> DeleteOutboxAsync(OutboxMessage msg) => _db.DeleteAsync(msg);

    public Task<int> SaveMessageOutboxAsync(OutboxMessage msg) => SaveOutboxAsync(msg);

    public Task<List<Company>> GetCompaniesAsync() => _db.Table<Company>().ToListAsync();

    public async Task<int> SaveCompanyAsync(Company c)
    {
        if (c.Id == Guid.Empty)
            c.Id = Guid.NewGuid();

        c.LastModifiedUtc = DateTime.UtcNow;
        c.IsDirty = true;

        string fileNo = (c.FileNumber ?? string.Empty).Trim().ToLower();
        string certNo = (c.CertificateNumber ?? string.Empty).Trim().ToLower();
        string name = (c.Name ?? string.Empty).Trim().ToLower();

        Company? existing = null;

        if (fileNo.Length > 0)
        {
            existing = (await _db.QueryAsync<Company>(
                            "SELECT * FROM Company WHERE FileNumber IS NOT NULL AND LOWER(FileNumber) = ? LIMIT 1",
                            fileNo)).FirstOrDefault();
        }
        if (existing == null && certNo.Length > 0)
        {
            existing = (await _db.QueryAsync<Company>(
                            "SELECT * FROM Company WHERE CertificateNumber IS NOT NULL AND LOWER(CertificateNumber) = ? LIMIT 1",
                            certNo)).FirstOrDefault();
        }
        if (existing == null && name.Length > 0)
        {
            existing = (await _db.QueryAsync<Company>(
                            "SELECT * FROM Company WHERE Name IS NOT NULL AND LOWER(Name) = ? LIMIT 1",
                            name)).FirstOrDefault();
        }

        if (existing != null)
        {
            c.Id = existing.Id;
            return await _db.UpdateAsync(c);
        }

        return await _db.InsertAsync(c);
    }

    public async Task<int> SaveInspectionAsync(Inspection i)
    {
        if (i.Id == Guid.Empty)
            i.Id = Guid.NewGuid();

        i.LastModifiedUtc = DateTime.UtcNow;
        i.IsDirty = true;
        i.IsSynced = false; // Always mark unsynced when saving

        // Ensure ScheduledInspectionId is set
        if (i.ScheduledInspectionId == Guid.Empty)
            i.ScheduledInspectionId = Guid.NewGuid();

        // Try find existing record by ScheduledInspectionId
        var existing = await _db.Table<Inspection>()
                                 .FirstOrDefaultAsync(x => x.ScheduledInspectionId == i.ScheduledInspectionId);

        if (existing != null)
        {
            i.Id = existing.Id; // keep original Id
            return await _db.UpdateAsync(i);
        }

        return await _db.InsertAsync(i);
    }

    public Task<int> SaveInspectionAnswerAsync(InspectionAnswer a) => _db.InsertAsync(a);
    public Task<int> SaveInspectionPhotoAsync(InspectionPhoto p) => _db.InsertAsync(p);

    public Task<List<InspectionAnswer>> GetInspectionAnswersAsync(Guid inspectionId) =>
        _db.Table<InspectionAnswer>()
           .Where(a => a.InspectionId == inspectionId)
           .ToListAsync();

    public Task<List<InspectionPhoto>> GetPhotosForAnswerAsync(Guid answerId) =>
        _db.Table<InspectionPhoto>()
           .Where(p => p.InspectionAnswerId == answerId)
           .ToListAsync();

    public Task<List<PendingAiReport>> GetPendingReportsAsync() =>
        _db.Table<PendingAiReport>()
           .OrderByDescending(r => r.CreatedAt)
           .ToListAsync();

    public Task<List<InspectionAnswer>> GetAnswersForInspectionAsync(Guid inspectionId) =>
        GetInspectionAnswersAsync(inspectionId);

    public async Task<int> SavePendingReportAsync(PendingAiReport report)
    {
        await EnsureInitializedAsync();
        return await _db.InsertAsync(report);
    }

    public Task<AIReport?> GetAIReportByIdAsync(Guid id)
    {
        return _db.Table<AIReport>()
                  .Where(r => r.Id == id && !r.IsDeleted)
                  .FirstOrDefaultAsync();
    }

    public async Task<List<ScheduledInspection>> GetUpcomingInspectionsForInspectorAsync(Guid inspectorId)
    {
        var all = await _db.Table<ScheduledInspection>()
                           .Where(x => x.ScheduledDate <= DateTime.Today)
                           .ToListAsync();

        return all.Where(x =>
        {
            try
            {
                var list = string.IsNullOrWhiteSpace(x.InspectorIdsJson)
                            ? new List<string>()
                            : JsonSerializer.Deserialize<List<string>>(x.InspectorIdsJson) ?? new List<string>();

                return list.Contains(inspectorId.ToString());
            }
            catch
            {
                return false;
            }
        }).ToList();
    }

    public Task<int> UpdatePendingReportAsync(PendingAiReport report) => _db.UpdateAsync(report);

    public Task<List<AIReport>> GetAIReportsAsync()
    {
        return _db.Table<AIReport>()
                  .Where(r => !r.IsDeleted)
                  .OrderByDescending(r => r.CreatedAt)
                  .ToListAsync();
    }

    public async Task<PendingAiReport?> GetPendingReportByInspectionIdAsync(Guid inspectionId)
    {
        return await _db.Table<PendingAiReport>()
                        .Where(r => r.InspectionId == inspectionId)
                        .FirstOrDefaultAsync();
    }

    public async Task<AIReport?> GetAIReportByInspectionIdAsync(Guid inspectionId)
    {
        return await _db.Table<AIReport>()
                        .Where(r => r.InspectionId == inspectionId)
                        .FirstOrDefaultAsync();
    }

    public Task<List<AIReport>> GetAIReportsByInspectorAsync(string inspectorUsername) =>
        _db.Table<AIReport>()
           .Where(r => r.InspectorUsername == inspectorUsername)
           .OrderByDescending(r => r.CreatedAt)
           .ToListAsync();

    public Task<List<CompanyRenewal>> GetCompanyRenewalsAsync(Guid id) =>
        _db.Table<CompanyRenewal>()
           .Where(r => r.CompanyId == id)
           .OrderByDescending(r => r.RenewalDate)
           .ToListAsync();

    public Task<List<CompanyRenewal>> GetCompanyRenewalsAsync()
    {
        return _db.Table<CompanyRenewal>()
                  .Where(r => !r.IsDeleted)
                  .OrderByDescending(r => r.RenewalDate)
                  .ToListAsync();
    }

    public async Task<int> SaveCompanyRenewalAsync(CompanyRenewal r)
    {
        var dup = await _db.Table<CompanyRenewal>()
                           .FirstOrDefaultAsync(x => x.CompanyId == r.CompanyId && x.RenewalYear == r.RenewalYear);
        return dup != null ? 0 : await _db.InsertAsync(r);
    }

    public async Task<DateTime> GetEffectiveLastRenewalDateAsync(Guid companyId)
    {
        var latest = await _db.Table<CompanyRenewal>()
                              .Where(r => r.CompanyId == companyId)
                              .OrderByDescending(r => r.RenewalDate)
                              .FirstOrDefaultAsync();

        if (latest != null && latest.RenewalDate > DateTime.MinValue)
            return latest.RenewalDate;

        var cmp = await _db.Table<Company>()
                           .FirstOrDefaultAsync(c => c.Id == companyId);
        return cmp?.RegistrationDate ?? DateTime.Now;
    }

    public async Task<List<Inspection>> GetUnsyncedInspectionsAsync()
    {
        return await _db.Table<Inspection>().Where(x => !x.IsSynced).ToListAsync();
    }

    public async Task<List<Appointment>> GetAllAppointmentsAsync()
    {
        await EnsureInitializedAsync();
        return await _db.Table<Appointment>().OrderByDescending(a => a.MeetingDate).ToListAsync();
    }

    public async Task<List<Company>> GetAllCompaniesAsync()
    {
        await EnsureInitializedAsync();
        return await _db.Table<Company>().OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<List<int>> GetAvailableInspectionYearsByCompanyTypeAsync(string companyType)
    {
        var query = @")
    SELECT DISTINCT strftime('%Y', PlannedDate) AS Year
    FROM Inspection
    WHERE NatureOfWork = ?
    ORDER BY Year DESC";

        var results = await _db.QueryAsync<YearResult>(query, companyType);
        return results.Select(r => int.Parse(r.Year!)).ToList();
    }

    public async Task<List<AuditLog>> GetAllAuditLogsAsync()
    {
        return await _db.Table<AuditLog>().ToListAsync();
    }

    public async Task<List<Violation>> GetAllViolationsAsync()
    {
        return await _db.Table<Violation>().ToListAsync();
    }

    public async Task DeleteViolationAsync(Guid violationId)
    {
        await _db.DeleteAsync<Violation>(violationId);
    }

    public async Task<List<int>> GetAvailableInspectionYearsByTypeAndLocationAsync(string companyType, string location)
    {
        var query = @"
    SELECT DISTINCT strftime('%Y', PlannedDate) AS Year
    FROM Inspection
    WHERE NatureOfWork = ? AND Location = ?
    ORDER BY Year DESC";

        var results = await _db.QueryAsync<YearResult>(query, companyType, location);
        return results.Select(r => int.Parse(r.Year!)).ToList();
    }

    // Helper class for year query results
    public class YearResult
    {
        public string? Year { get; set; }
    }

    public Task<int> UpdateCompanyAsync(Company company)
    {
        return _db.UpdateAsync(company);
    }

    public async Task<int> CalculateDaysSinceLastInspectionAsync(Guid companyId)
    {
        var lastInspection = await _db.Table<AIReport>()
            .Where(r => r.CompanyId == companyId && r.Status == "Approved")
            .OrderByDescending(r => r.DateCompleted)
            .FirstOrDefaultAsync();

        if (lastInspection != null && lastInspection.DateCompleted.HasValue)
        {
            return (DateTime.Now - lastInspection.DateCompleted.Value).Days;
        }

        // If no inspection found, return -1
        return -1;
    }

    public async Task<List<Guid>> GetCompanyIdsByTypeAndLocationAsync(string companyType, string location)
    {
        var query = @"
        SELECT DISTINCT CompanyId 
        FROM RiskPredictionHistory 
        WHERE CompanyType = ? AND Location = ?";
        var results = await _db.QueryAsync<RiskPredictionHistory>(query, companyType, location);
        return results.Select(r => r.CompanyId).Distinct().ToList();
    }

    public async Task<List<int>> GetAvailablePredictionYearsByCompanyTypeAsync(string companyType)
    {
        var history = await _db.Table<RiskPredictionHistory>()
            .Where(h => h.CompanyType == companyType)
            .ToListAsync();

        var years = history
            .Select(h =>
            {
                if (DateTime.TryParseExact(h.DatePredicted,
                                            "yyyy-MM-dd HH:mm:ss",
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.None,
                                            out var dt))
                    return (int?)dt.Year;

                return null;
            })
            .Where(y => y.HasValue)
            .Select(y => y!.Value)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        return years;
    }

    public async Task<List<int>> GetAvailablePredictionYearsByTypeAndLocationAsync(string companyType, string location)
    {
        try
        {
            var results = await _db.QueryAsync<RiskPredictionHistory>(
                @"SELECT * FROM RiskPredictionHistory 
              WHERE CompanyType = ? AND Location = ?", companyType, location);

            var years = results
                .Where(r => DateTime.TryParse(r.DatePredicted, out _))
                .Select(r => DateTime.Parse(r.DatePredicted).Year)
                .Distinct()
                .ToList();

            return years;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GetAvailablePredictionYearsByTypeAndLocationAsync] Error: {ex.Message}");
            return new List<int>();
        }
    }

    public async Task<List<Company>> GetRiskHistoryCompaniesByTypeAndLocationAsync(string companyType, string location)
    {
        var riskEntries = await _db.Table<RiskPredictionHistory>()
            .Where(r => r.CompanyType == companyType && r.Location == location)
            .ToListAsync();

        var companyNames = riskEntries
            .Select(r => r.CompanyName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();

        var companies = await _db.Table<Company>()
            .Where(c => c.Name != null && companyNames.Contains(c.Name))
            .ToListAsync();

        return companies;
    }

    public async Task<List<int>> GetAvailablePredictionYearsByTypeAsync(string companyType)
    {
        var records = await _db.Table<RiskPredictionHistory>()
            .Where(r => r.CompanyType == companyType)
            .ToListAsync();

        return records
            .Select(r =>
            {
                if (DateTime.TryParse(r.DatePredicted, out var dt))
                    return (int?)dt.Year;
                return null;
            })
            .Where(year => year.HasValue)
            .Select(year => year!.Value)
            .Distinct()
            .OrderBy(y => y)
            .ToList();
    }

    public async Task MarkInspectionsAsSyncedAsync(List<Guid> inspectionIds)
    {
        foreach (var id in inspectionIds)
        {
            var inspection = await _db.Table<Inspection>().Where(x => x.Id == id).FirstOrDefaultAsync();
            if (inspection != null)
            {
                inspection.IsSynced = true;
                await _db.UpdateAsync(inspection);
            }
        }
    }

    public async Task<Inspection?> GetInspectionByScheduledIdAsync(Guid scheduledInspectionId)
    {
        return await _db.Table<Inspection>()
            .Where(i => i.ScheduledInspectionId == scheduledInspectionId)
            .FirstOrDefaultAsync();
    }

    public Task<List<User>> GetAllUsersAsync()
    {
        return _db.Table<User>().ToListAsync();
    }

    public async Task<int> SaveScheduledInspectionAsync(ScheduledInspection inspection)
    {
        await EnsureInitializedAsync();

        if (inspection.Id == Guid.Empty)
            inspection.Id = Guid.NewGuid();

        return await _db.InsertAsync(inspection);
    }

    public async Task<AppSettings> GetAppSettingsAsync()
    {
        var s = await _db.Table<AppSettings>().FirstOrDefaultAsync();
        if (s == null)
        {
            s = new AppSettings();
            await SaveAppSettingsAsync(s);
        }
        return s;
    }

    public async Task<List<int>> GetAvailableInspectionYearsAsync()
    {
        await EnsureInitializedAsync();

        var allRecords = await _db.Table<RiskPredictionHistory>().ToListAsync();

        var years = allRecords
            .Where(x => DateTime.TryParse(x.DatePredicted, out _))
            .Select(x => DateTime.Parse(x.DatePredicted).Year)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        return years;
    }

    public async Task<List<string>> GetLocationsForCompanyAsync(Guid companyId)
    {
        var company = await _db.Table<Company>()
                               .Where(c => c.Id == companyId)
                               .FirstOrDefaultAsync();

        if (company != null && !string.IsNullOrWhiteSpace(company.Location))
            return new List<string> { company.Location };

        return new List<string>();
    }

    public async Task<List<string>> GetCompanyLocationsAsync()
    {
        await EnsureInitializedAsync();

        var companies = await _db.Table<Company>()
            .Where(c => !string.IsNullOrEmpty(c.Location))
            .ToListAsync();

        return companies
            .Select(c => c.Location?.Trim() ?? string.Empty)
            .Where(loc => !string.IsNullOrWhiteSpace(loc))
            .Distinct()
            .OrderBy(loc => loc)
            .ToList();
    }

    public async Task<Company> GetCompanyByIdAsync(Guid id)
    {
        return await _db.Table<Company>().Where(c => c.Id == id).FirstOrDefaultAsync();
    }

    public async Task<List<Guid>> GetCompanyIdsByTypeAsync(string companyType)
    {
        var query = @"
        SELECT DISTINCT CompanyId 
        FROM RiskPredictionHistory 
        WHERE CompanyType = ?";
        var results = await _db.QueryAsync<RiskPredictionHistory>(query, companyType);
        return results.Select(r => r.CompanyId).Distinct().ToList();
    }

    public async Task<List<Guid>> GetAllCompanyIdsFromRiskHistoryAsync()
    {
        var query = @"SELECT DISTINCT CompanyId FROM RiskPredictionHistory";
        var results = await _db.QueryAsync<RiskPredictionHistory>(query);
        return results.Select(r => r.CompanyId).Distinct().ToList();
    }

    public async Task<List<Company>> GetRiskHistoryCompaniesByTypeAsync(string companyType)
    {
        var riskEntries = await _db.Table<RiskPredictionHistory>()
            .Where(r => r.CompanyType == companyType)
            .ToListAsync();

        var companyNames = riskEntries
            .Select(r => r.CompanyName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();

        var companies = await _db.Table<Company>()
            .Where(c => c.Name != null && companyNames.Contains(c.Name))
            .ToListAsync();

        return companies;
    }

    public async Task<List<string>> GetCompanyTypesAsync()
    {
        await EnsureInitializedAsync();

        return (await _db.Table<Company>()
            .Where(c => !string.IsNullOrEmpty(c.NatureOfWork))
            .ToListAsync())
            .Select(c => c.NatureOfWork?.Trim() ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    public async Task<List<int>> GetYearsForCompanyLocationAsync(string companyType, string location)
    {
        var history = await _db.Table<RiskPredictionHistory>().ToListAsync();

        return history
            .Where(h => h.CompanyType == companyType && h.Location == location)
            .Select(h =>
            {
                if (DateTime.TryParse(h.DatePredicted, out var dt))
                    return dt.Year;
                return 0;
            })
            .Where(y => y > 0)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();
    }

    public async Task<List<string>> GetInspectorUsernamesAsync()
    {
        await EnsureInitializedAsync();

        var allWithInspector = await _db.Table<ScheduledInspection>()
            .Where(si => !string.IsNullOrWhiteSpace(si.InspectorUsername))
            .ToListAsync();

        var inspectors = allWithInspector
            .Select(si => si.InspectorUsername)
            .Distinct()
            .ToList();

        return inspectors;
    }

    public async Task<List<MonthlyRiskTrend>> GetMonthlyRiskTrendAsync(string companyType, string location, int year)
    {
        var results = new Dictionary<int, RiskTrendChartPoint>();

        for (int m = 1; m <= 12; m++)
        {
            results[m] = new RiskTrendChartPoint
            {
                MonthNumber = m,
                High = 0,
                Medium = 0,
                Low = 0
            };
        }

        var query = @"
        SELECT strftime('%m', r.DatePredicted) AS Month,
       r.RiskLevel,
       COUNT(*) AS Count
FROM RiskPredictionHistory r
INNER JOIN (
    SELECT CompanyId,
           strftime('%Y-%m', DatePredicted) AS YearMonth,
           MAX(DatePredicted) AS LatestDate
    FROM RiskPredictionHistory
    WHERE strftime('%Y', DatePredicted) = ?
      AND (? = '' OR CompanyType = ?)
      AND (? = '' OR Location = ?)
    GROUP BY CompanyId, YearMonth
) latest
ON r.CompanyId = latest.CompanyId
AND strftime('%Y-%m', r.DatePredicted) = latest.YearMonth
AND r.DatePredicted = latest.LatestDate
GROUP BY Month, r.RiskLevel
ORDER BY Month;
    ";

        var paramYear = year.ToString("D4");
        var companyParam = string.IsNullOrWhiteSpace(companyType) ? "" : companyType;
        var locationParam = string.IsNullOrWhiteSpace(location) ? "" : location;

        var rawResults = await _db.QueryAsync<RiskTrendResult>(
            query,
            paramYear, companyParam, companyParam, locationParam, locationParam
        );

        foreach (var item in rawResults)
        {
            if (!int.TryParse(item.Month, out int monthInt)) continue;
            if (!results.ContainsKey(monthInt)) continue;

            var point = results[monthInt];

            switch (item.RiskLevel)
            {
                case "High": point.High = item.Count; break;
                case "Medium": point.Medium = item.Count; break;
                case "Low": point.Low = item.Count; break;
            }
        }

        var months = new[] { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        return results.Values
            .OrderBy(p => p.MonthNumber)
            .Select(p => new MonthlyRiskTrend
            {
                Month = months[p.MonthNumber],
                High = p.High,
                Medium = p.Medium,
                Low = p.Low
            })
            .ToList();
    }
    public Task<List<Company>> GetUnsyncedCompaniesAsync()
    {
        return _db.Table<Company>()
                        .Where(c => c.IsSynced == false)
                        .ToListAsync();
    }
    public Task<int> MarkCompanyAsSyncedAsync(int companyId)
    {
        return _db.ExecuteAsync("UPDATE Company SET IsSynced = 1 WHERE Id = ?", companyId);
    }
    public async Task UpdateCompanySyncStatusAsync(Guid companyId, bool isSynced)
    {
        var company = await GetCompanyByIdAsync(companyId);
        if (company != null)
        {
            company.IsSynced = isSynced;
            await SaveCompanyAsync(company);
        }
    }

    private class RiskTrendResult
    {
        public string? Month { get; set; }
        public string? RiskLevel { get; set; }
        public int Count { get; set; }
    }

    public Task<int> SaveAppSettingsAsync(AppSettings s) => _db.InsertOrReplaceAsync(s);

    public async Task LogoutAsync()
    {
        // 1. Clear user data from SQLite
        var deleteTasks = new Task[]
        {
            _db.DeleteAllAsync<User>(),
            _db.DeleteAllAsync<Appointment>(),
            _db.DeleteAllAsync<AuditLog>(),
            _db.DeleteAllAsync<Company>(),
            _db.DeleteAllAsync<Inspection>(),
            _db.DeleteAllAsync<AppSettings>(),
            _db.DeleteAllAsync<AIReport>(),
            _db.DeleteAllAsync<CompanyRenewal>(),
            _db.DeleteAllAsync<InspectionAnswer>(),
            _db.DeleteAllAsync<PendingAiReport>(),
            _db.DeleteAllAsync<InspectionPhoto>(),
            _db.DeleteAllAsync<OutboxMessage>(),
            _db.DeleteAllAsync<ScheduledInspection>(),
            _db.DeleteAllAsync<ScheduledInspectionHistory>(),
            _db.DeleteAllAsync<RiskPredictionHistory>(),
            _db.DeleteAllAsync<MyEmailMessage>(),
            _db.DeleteAllAsync<Violation>()
        };
        await Task.WhenAll(deleteTasks);

        // 2. Optionally clear SecureStorage/session
        SecureStorage.RemoveAll();
        App.CurrentUser = null;

        // 3. Navigate to login page or exit
        // await _navigation.PopToRootAsync(); // <-- Remove or replace this line
    }
}