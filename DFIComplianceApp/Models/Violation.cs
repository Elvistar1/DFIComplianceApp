using SQLite;

namespace DFIComplianceApp.Models
{
    public class Violation : ISyncEntity
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🔹 Sync Metadata
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public bool IsDirty { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        public Guid CompanyId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public DateTime DateIssued { get; set; } = DateTime.UtcNow;
        public DateTime? DateResolved { get; set; }

        public string Severity { get; set; } = string.Empty; // e.g. Low, Medium, High

        [Ignore]
        public bool IsResolved => DateResolved.HasValue;
    }
}
