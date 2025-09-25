using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class MonthlyRiskTrend
    {
        public string Month { get; set; }  // "Jan", "Feb", etc.
        public int High { get; set; }      // Number of "High" risks in that month
        public int Medium { get; set; }    // Number of "Medium" risks
        public int Low { get; set; }       // Number of "Low" risks
    }

}
