using SQLite;
using System;

namespace DFIComplianceApp.Models
{
    public class PendingAiReport
    {
         [PrimaryKey]
            public string Id { get; set; } = Guid.NewGuid().ToString(); // default new ID
            public Guid InspectionId { get; set; }
            public string Json { get; set; } = string.Empty;
            public string Status { get; set; } = "Queued";
            public string Advice { get; set; } = string.Empty;
            public string ReviewerComment { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public string InspectorUsername { get; set; } = string.Empty;
            public string LastAiResult { get; set; } = "";
        public DateTime? RecommendationSentAt { get; set; }
        public string? RecommendationSentBy { get; set; }
    }

    }

