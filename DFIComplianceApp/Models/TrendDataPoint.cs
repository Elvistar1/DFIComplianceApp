using SQLite;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DFIComplianceApp.Models
{
    public class TrendDataPoint
    {
        public DateTime Date { get; set; }

        [Ignore] // 🚫 SQLite will ignore this property
        public Dictionary<string, int> RiskLevelCounts { get; set; } = new();

        // ✅ This is what gets stored in SQLite instead
        public string RiskLevelCountsJson
        {
            get => JsonSerializer.Serialize(RiskLevelCounts);
            set => RiskLevelCounts = string.IsNullOrWhiteSpace(value)
                ? new Dictionary<string, int>()
                : JsonSerializer.Deserialize<Dictionary<string, int>>(value);
        }
    }
}
