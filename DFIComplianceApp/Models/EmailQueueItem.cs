using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class EmailQueueItem
    {
        public string CompanyName { get; set; }
        public DateTime MeetingDate { get; set; }
        public string Subject { get; set; }
        public string BodyText { get; set; }
    }
}

