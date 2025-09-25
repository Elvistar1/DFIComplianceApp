using DFIComplianceApp.Models;
using Microsoft.Maui.Storage; // ✅ Needed for Preferences
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DFIComplianceApp.Services
{
    public class DirectorNotificationService
    {
        private readonly FirebaseAuthService _firebase;
        private const string SeenKey = "DirectorSeenNotifications";

        public DirectorNotificationService(FirebaseAuthService firebase)
        {
            _firebase = firebase;
        }

        private HashSet<string> LoadSeen()
        {
            var json = Preferences.Get(SeenKey, "[]");
            return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new();
        }

        private void SaveSeen(HashSet<string> seen)
        {
            Preferences.Set(SeenKey, JsonSerializer.Serialize(seen));
        }

        public async Task<List<string>> GetStartupNotificationsAsync()
        {
            var notifications = new List<string>();
            var seen = LoadSeen();

            // 1. New Companies
            var companies = await _firebase.GetCompaniesSafeAsync();
            var newCompanies = companies
                .Where(c => (DateTime.UtcNow - c.RegistrationDate).TotalHours < 24)
                .ToList();

            foreach (var c in newCompanies)
            {
                var key = $"company-{c.Id}";
                if (!seen.Contains(key))
                {
                    notifications.Add($"New company registered: {c.Name}");
                    seen.Add(key);
                }
            }

            // 2. New Users
            var users = await _firebase.GetAllUsersAsync();
            var newUsers = users
                .Where(u => (DateTime.UtcNow - u.CreatedAt).TotalHours < 24)
                .ToList();

            foreach (var u in newUsers)
            {
                var key = $"user-{u.Id}";
                if (!seen.Contains(key))
                {
                    notifications.Add($"New user added: {u.Email}");
                    seen.Add(key);
                }
            }

            // 3. Completed inspections
            var inspections = await _firebase.GetInspectionsAsync();
            var completedInspections = inspections.Where(i => i.CompletedDate != null).ToList();

            var aiReports = await _firebase.GetAIReportsAsync();
            var validReports = aiReports.Where(r => r.InspectionId != Guid.Empty).ToList();

            // 4. Pending AI reports
            var pendingReports = (await _firebase.GetPendingReportsAsync())
                .Where(r => r.InspectionId != Guid.Empty)
                .ToList();

            var pendingByInspection = pendingReports
                .GroupBy(r => r.InspectionId)
                .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
                .ToList();

            // 4a. Needing oversight
            var needingOversight = pendingByInspection
                .Where(r => r.Status is "Queued" or "Failed")
                .ToList();

            foreach (var r in needingOversight)
            {
                var key = $"oversight-{r.Id}";
                if (!seen.Contains(key))
                {
                    notifications.Add($"Inspection {r.InspectionId} needs AI oversight.");
                    seen.Add(key);
                }
            }

            // 4b. Flagged reports
            var flagged = pendingByInspection
                .Where(r => r.Status == "Flagged")
                .ToList();

            foreach (var r in flagged)
            {
                var key = $"flagged-{r.Id}";
                if (!seen.Contains(key))
                {
                    notifications.Add($"AI report for inspection {r.InspectionId} flagged for review.");
                    seen.Add(key);
                }
            }

            // 5. Overdue reports
            var overdue = pendingByInspection
                .Where(r =>
                    r.Status == "Queued" &&
                    completedInspections.Any(i => i.Id == r.InspectionId &&
                        (DateTime.UtcNow - i.CompletedDate.Value).TotalHours > 24)
                )
                .ToList();

            foreach (var r in overdue)
            {
                var key = $"overdue-{r.Id}";
                if (!seen.Contains(key))
                {
                    notifications.Add($"Inspection {r.InspectionId} waiting AI analysis for over 24 hours.");
                    seen.Add(key);
                }
            }

            // ✅ Save updated seen set
            SaveSeen(seen);

            return notifications;
        }
    }
}
