using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class AuditLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // 🔹 Core Fields
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // 🔹 Extended Info
        public string Details { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;

        // 🔹 Sync Metadata
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public bool IsDirty { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        [Ignore]
        public string DisplayText => $"{Timestamp:G} - [{Role}] {Username}: {Action}";
    }
}
