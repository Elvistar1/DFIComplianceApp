using DFIComplianceApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyEmailMessage = DFIComplianceApp.Models.EmailMessage;


namespace DFIComplianceApp.Services
{
    public interface IAppDatabase
    {
        Task EnsureInitializedAsync();

        /* ═════════════════ Users ═════════════════ */
        Task<List<User>> GetUsersAsync();
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByEmailAsync(string email);
        Task<int> GetUsersCountAsync();
        Task<int> SaveUserAsync(User user);
        Task<int> DeleteUserAsync(User user);
        Task<List<User>> GetAllUsersAsync();
        
       
        Task<List<int>> GetYearsForCompanyLocationAsync(string companyType, string location);
        // In IAppDatabase.cs
        Task<Dictionary<string, int>> GetRiskLevelDistributionAsync(string companyType, string location, int? year);

        Task<List<Company>> GetRiskHistoryCompaniesByTypeAsync(string companyType);
        Task<List<Company>> GetRiskHistoryCompaniesByTypeAndLocationAsync(string companyType, string location);
        Task<List<MonthlyRiskTrend>> GetMonthlyRiskTrendAsync(string companyType, string location, int year);
        Task<List<string>> GetLocationsForCompanyAsync(Guid companyId);
        Task<List<string>> GetCompanyTypesAsync();
        Task<List<string>> GetCompanyLocationsAsync();
        Task<List<Company>> GetCompaniesByTypeAsync(string companyType);
        Task<List<int>> GetAvailablePredictionYearsByTypeAndLocationAsync(string companyType, string location);
        Task<List<int>> GetAvailablePredictionYearsByCompanyTypeAsync(string companyType);
        Task<List<int>> GetAvailableInspectionYearsAsync();


        /* ═════════════════ Companies / Renewals ═════════════════ */
        Task<List<Company>> GetCompaniesAsync();
        Task<int> SaveCompanyAsync(Company company);
        Task<List<Appointment>> GetAllAppointmentsAsync();
        Task<List<CompanyRenewal>> GetCompanyRenewalsAsync(Guid companyId);
        Task<int> SaveCompanyRenewalAsync(CompanyRenewal renewal);
        Task<DateTime> GetEffectiveLastRenewalDateAsync(Guid companyId);
        Task<CompanyRenewal?> GetCompanyRenewalAsync(Guid companyId, int year);
        Task MarkReminderSentAsync(Guid companyId, int year);
        Task<DateTime?> GetLastRenewalDateAsync(Guid companyId);
        Task<List<Company>> GetAllCompaniesAsync();
        Task InsertEmailMessageAsync(MyEmailMessage email);
        Task SaveEmailAsync(MyEmailMessage email);
        Task<IEnumerable<MyEmailMessage>> GetAllEmailMessagesAsync();


        /* ═════════════════ Outbox ═════════════════ */
        Task<List<OutboxMessage>> GetPendingOutboxAsync();
        Task<int> SaveOutboxAsync(OutboxMessage email);
        Task<int> UpdateOutboxAsync(OutboxMessage email);
        Task<int> DeleteOutboxAsync(OutboxMessage email);
        Task<int> GetUnsentOutboxCountAsync();
        Task<int> MarkOutboxSentAsync(int id, DateTime sentAt);
        Task<int> SaveMessageOutboxAsync(OutboxMessage msg);
        Task<bool> EmailExistsAsync(string gmailMessageId);



        /* ═════════════════ Inspections ═════════════════ */
        Task<int> SaveInspectionAsync(Inspection inspection);
        Task<int> SaveInspectionAnswerAsync(InspectionAnswer answer);
        Task<int> SaveInspectionPhotoAsync(InspectionPhoto photo);
        Task<List<Inspection>> GetInspectionsAsync();
        Task<List<InspectionAnswer>> GetInspectionAnswersAsync(Guid inspectionId);
        Task<List<InspectionPhoto>> GetPhotosForAnswerAsync(Guid answerId);

        Task<Inspection?> GetInspectionByScheduledIdAsync(Guid scheduledInspectionId);

        // In IAppDatabase interface
        Task<List<Appointment>> GetAppointmentsAsync();
  
        Task<int> SaveAppointmentAsync(Appointment appointment);
        Task<int> DeleteAppointmentAsync(Appointment appointment);
        Task<Appointment?> GetAppointmentByIdAsync(Guid id);



        /* ═════════════════ Scheduled Inspections ═════════════════ */
        Task<List<ScheduledInspection>> GetAllScheduledInspectionsAsync();
        Task<int> SaveScheduledInspectionAsync(ScheduledInspection inspection);
        Task<List<ScheduledInspection>> GetUpcomingInspectionsForInspectorAsync(Guid inspectorId);
        Task MoveApprovedOrExpiredInspectionsToHistoryAsync();
        Task<List<ScheduledInspectionHistory>> GetScheduledInspectionHistoryAsync();
        /* ═════════════════ Scheduled Inspections ═════════════════ */
       
        Task<ScheduledInspection?> GetScheduledInspectionByIdAsync(Guid id);
        
        Task<int> DeleteScheduledInspectionAsync(ScheduledInspection inspection);
        Task<List<ScheduledInspection>> GetDirtyScheduledInspectionsAsync();

        /* ═════════════════ AI Reports ═════════════════ */
        Task<List<AIReport>> GetAIReportsAsync();
        Task<AIReport?> GetAIReportByInspectionIdAsync(Guid inspectionId);
        Task<AIReport?> GetAIReportByIdAsync(Guid id);
        Task<int> SaveAIReportAsync(AIReport report);

        Task<List<PendingAiReport>> GetPendingReportsAsync();
        Task<PendingAiReport?> GetPendingReportByInspectionIdAsync(Guid inspectionId);
        Task<int> SavePendingReportAsync(PendingAiReport report);
        Task<int> UpdatePendingReportAsync(PendingAiReport report);

        /* ═════════════════ Settings ═════════════════ */
        Task<AppSettings> GetAppSettingsAsync();
        Task<int> SaveAppSettingsAsync(AppSettings settings);

        /* ═════════════════ Audit Logs ═════════════════ */
        Task<int> SaveAuditLogAsync(AuditLog log);
        Task<List<AuditLog>> GetAuditLogsAsync();
       
        Task<List<AIReport>> GetAllAIReportsAsync();
       
        Task DeleteAIReportAsync(Guid id);
        Task<List<InspectionPhoto>> GetDirtyInspectionPhotosAsync();
        Task<InspectionPhoto?> GetInspectionPhotoByIdAsync(Guid id);
        Task LogoutAsync();

    }
}
