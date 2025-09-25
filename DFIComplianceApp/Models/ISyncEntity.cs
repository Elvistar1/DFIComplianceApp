using System;

namespace DFIComplianceApp.Models
{
    public interface ISyncEntity
    {
        Guid Id { get; set; }
        bool IsDirty { get; set; }
        bool IsDeleted { get; set; }
        DateTime LastModifiedUtc { get; set; }
    }
}
