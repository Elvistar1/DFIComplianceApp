using SQLite;
using System;


namespace DFIComplianceApp.Models
{
    public class InspectionDisplay
    {
        public string CompanyName { get; set; }
        public string InspectorName { get; set; }
        public DateTime PlannedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Remarks { get; set; }
    }
}
