using System.Collections.Generic;
using DFIComplianceApp.Models;




namespace DFIComplianceApp.Models
{
    public class ChecklistItemView
    {
        public string Question { get; set; } = "";
        public string Compliant { get; set; } = "No"; // use "Yes"/"No" as string
        public string? Notes { get; set; }
        public List<string> PhotoPaths { get; set; } = new();
        public string RiskLevel { get; set; } = "Low";

    }
}
