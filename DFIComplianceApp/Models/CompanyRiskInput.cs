using Microsoft.ML.Data;

namespace DFIComplianceApp.Models
{
    public class CompanyRiskInput
    {
        public float ViolationCount { get; set; }
        public float DaysSinceLastInspection { get; set; }
        public string RenewalStatus { get; set; }
        public string CompanyType { get; set; }

        // Dummy value to satisfy schema
        public string RiskLevel { get; set; } = "";
    }

}
