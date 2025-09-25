using System;
using System.Collections.Generic;

namespace DFIComplianceApp.Models
{
    public class AiChecklistItemViewModel
    {
        public string Question { get; set; } = string.Empty;

        public bool Compliant { get; set; }

        public string Notes { get; set; } = string.Empty;

        public List<string> PhotoPaths { get; set; } = new();
    }
}
