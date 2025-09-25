using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class Appointment
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? CompanyId { get; set; }   // Link to a Company if matched

        public string CompanyName { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string Occupier { get; set; } = string.Empty;

        public string EmailContact { get; set; } = string.Empty;

        public DateTime MeetingDate { get; set; }

        public string Subject { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        public bool IsDirty { get; set; } = false;   // Needs to be synced
        public bool IsDeleted { get; set; } = false; // Marked for deletion
        public string Source { get; set; } = "Email"; // Email, Manual, etc.

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🔹 For offline/online sync tracking
        public bool IsSynced { get; set; } = false;

        // 🔹 Add this so SaveAppointmentAsync compiles
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
    }
}
