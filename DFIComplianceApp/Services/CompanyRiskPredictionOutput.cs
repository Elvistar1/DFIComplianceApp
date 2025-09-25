using Microsoft.ML.Data;

namespace DFIComplianceApp.Models
{
    public class CompanyRiskPredictionOutput
    {
        [ColumnName("PredictedLabel")]
        public string RiskLevel { get; set; }
    }
}
