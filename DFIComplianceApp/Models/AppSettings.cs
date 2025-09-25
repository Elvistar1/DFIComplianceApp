using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class AppSettings
    {
        [PrimaryKey]
        public int Id { get; set; } = 1; // Always 1, singleton row
        public bool IsDirty { get; set; } = false;   // Needs to be synced
        public bool IsDeleted { get; set; } = false; // Marked for deletion
        public bool EnableNotifications { get; set; } = true;
        public bool MaintenanceMode { get; set; } = false;
        public int InspectionReminderDays { get; set; } = 3;
        public string PasswordPolicy { get; set; } = "Minimum 6 characters";
        public string OpenRouterKey { get; set; }
    }
}
