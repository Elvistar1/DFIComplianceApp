using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class CompanyRenewal
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CompanyId { get; set; }
        public bool IsDirty { get; set; } = false;   // Needs to be synced
        public bool IsDeleted { get; set; } = false; // Marked for deletion
        public int RenewalYear { get; set; }

        public DateTime RenewalDate { get; set; }
   
        public string RenewedBy { get; set; }
        // ✅ NEW: prevents duplicate reminders
        public bool ReminderSent { get; set; } = false;
    }
}
