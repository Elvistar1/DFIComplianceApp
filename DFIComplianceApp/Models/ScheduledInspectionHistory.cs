using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class ScheduledInspectionHistory
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsDirty { get; set; } = false;   // Needs to be synced
        public bool IsDeleted { get; set; } = false; // Marked for deletion
        public Guid CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string InspectorUsername { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string Status { get; set; } = "Expired"; // "Expired" or "Completed"
        public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    }
}
