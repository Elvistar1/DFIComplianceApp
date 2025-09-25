using SQLite;

namespace DFIComplianceApp.Models
{
    public class RiskPredictionHistory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public Guid CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string Location { get; set; }
        public string RiskLevel { get; set; }
        public int ViolationCount { get; set; }
        public int DaysSinceLastInspection { get; set; }
        public string RenewalStatus { get; set; }
        public string CompanyType { get; set; }
        public bool IsDirty { get; set; } = false;   // Needs to be synced
        public bool IsDeleted { get; set; } = false; // Marked for deletion


        public string DatePredicted { get; set; }
        
    }
}
