using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DFIComplianceApp.Models
{
    public class ScheduledInspection
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string FileNumber { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public string Occupier { get; set; } = string.Empty;
        public string InspectorUsername { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string InspectorIdsJson { get; set; } = string.Empty;
        public string InspectorUsernamesJson { get; set; } = string.Empty;

        // 🔹 Sync Metadata
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public bool IsDirty { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        // ✅ Computed Property for Display
        public string DisplayInspectorNames
        {
            get
            {
                try
                {
                    var usernames = JsonSerializer.Deserialize<List<string>>(InspectorUsernamesJson ?? "[]");
                    return usernames != null && usernames.Any() ? string.Join(", ", usernames) : "(Unknown)";
                }
                catch
                {
                    return "(Unknown)";
                }
            }
        }
    }
}
