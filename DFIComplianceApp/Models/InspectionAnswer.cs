using SQLite;
using System;
using System.Collections.Generic;

namespace DFIComplianceApp.Models
{
    public class InspectionAnswer
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid InspectionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public bool IsCompliant { get; set; }

        public string Notes { get; set; } = string.Empty;

        // 🔹 Sync Metadata
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public bool IsDirty { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        [Ignore] // Used at runtime to hold photo paths/strings
        public List<string> Photos { get; set; } = new();

        [Ignore] // Runtime-only, holds full InspectionPhoto objects
        public List<InspectionPhoto> PhotosObjects { get; set; } = new();
    }
}
