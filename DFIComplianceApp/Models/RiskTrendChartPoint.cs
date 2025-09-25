using SQLite;
using System.Globalization;

namespace DFIComplianceApp.Models
{
    public class RiskTrendChartPoint
    {
        public int MonthNumber { get; set; }
        public string Month => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(MonthNumber);
        public int High { get; set; }
        public int Medium { get; set; }
        public int Low { get; set; }
    }

}
