using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class EmailMessage
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }  // Local SQLite PK, auto incremented

        public string GmailMessageId { get; set; }  // Store Gmail message ID here
        public bool IsDirty { get; set; } = false;   // Needs to be synced
        public bool IsDeleted { get; set; } = false; // Marked for deletion
        public string Subject { get; set; }
        public string From { get; set; }
        public DateTime ReceivedDate { get; set; }
        public string Body { get; set; }
        public bool IsConvertedToAppointment { get; set; }
        public string BodyPlainText { get; set; }  // existing Body can be renamed or repurposed
        public string BodyHtml { get; set; }

    }
}
