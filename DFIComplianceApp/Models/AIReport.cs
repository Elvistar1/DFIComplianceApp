using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class AIReport
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid InspectionId { get; set; }
        public Guid CompanyId { get; set; }

        // AI-generated summary/report
        public string GeneratedContent { get; set; } = string.Empty;

        // Status: "Pending", "Approved", "Flagged"
        public string Status { get; set; } = "Pending";

        // Optional reviewer comments
        public string? ReviewerComment { get; set; }

        // Inspector username
        public string InspectorUsername { get; set; } = string.Empty;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DateCompleted { get; set; } = null;

        // 🔹 Sync metadata
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow; // for conflict resolution
        public bool IsDirty { get; set; } = true;     // needs sync
        public bool IsDeleted { get; set; } = false;  // soft delete flag
    }
}
