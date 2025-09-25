using System;
using System.Collections.Generic;

namespace DFIComplianceApp.Models
{
    public class AiReportJson
    {
        public string Premises { get; set; } = "";
        public string NatureOfWork { get; set; } = "";

        public List<AiReportAnswer> Answers { get; set; } = new();
    }

    public class AiReportAnswer
    {
        public string Question { get; set; } = "";
        public bool Compliant { get; set; }
        public string? Notes { get; set; }

        public List<string> PhotoPaths { get; set; } = new(); // supports multiple photos per answer
    }
}
