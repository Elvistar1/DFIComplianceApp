using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DFIComplianceApp.Models.ViewModels
{
    public class AiReportJson
    {
        [JsonPropertyName("premises")]
        public string? Premises { get; set; }

        [JsonPropertyName("natureOfWork")]
        public string? NatureOfWork { get; set; }

        [JsonPropertyName("answers")]
        public List<AiAnswer>? Answers { get; set; }
    }

    public class AiAnswer
    {
        [JsonPropertyName("question")]
        public string? Question { get; set; }

        [JsonPropertyName("compliant")]
        public bool Compliant { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("photos")]
        public List<string>? PhotoPaths { get; set; }
    }

    public class ChecklistItemView
    {
        public string Question { get; set; } = "";
        public string Compliant { get; set; } = "";
        public string? Notes { get; set; }
        public List<string> PhotoPaths { get; set; } = new();
    }
}
