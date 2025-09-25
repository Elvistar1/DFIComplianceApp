using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.Services
{
    public interface IFirebaseAuthService
    {
        // Authentication
        Task<User?> LoginAsync(string email, string password); // ✅ Returns a full User object
        Task LogoutAsync();
        // Add this method declaration
        void StartAuditLogListener();

        // If you need a stop method, declare it as well
        void StopAuditLogListener();
        Task<AppSettings?> GetAppSettingsAsync();
        Task SaveAppSettingsAsync(AppSettings settings);
        Task PushAuditLogAsync(AuditLog log);
        // User data
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserProfileAsync(string userId);
        Task PushUserAsync(User user);
        Task StartCompanyListener();

        Task UpdateUserRoleAsync(string userId, string newRole);
        Task UpdateUserAsync(User user);
        Task SetUserActiveStatusAsync(string userId, bool isActive);
        Task<List<User>> GetUsersAsync(); // ✅ fixed

        // 🔥 Add these new members
        event EventHandler? CompaniesChanged;
        
        void StopCompanyListener();
        // Real-time updates
        void StartUserListener();
        void StopUserListener();
        event EventHandler UsersChanged;
    }
}
