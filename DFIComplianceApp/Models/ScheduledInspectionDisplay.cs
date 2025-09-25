using System;
using DFIComplianceApp.Models;


namespace DFIComplianceApp.Models
{
    public class ScheduledInspectionDisplay
    {
        public string CompanyName { get; set; } = string.Empty;
        public string FileNumber { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public string Occupier { get; set; } = string.Empty;
        public string InspectorUsernames { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
