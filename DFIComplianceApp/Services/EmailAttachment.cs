namespace DFIComplianceApp.Services
{
    /// <summary>
    /// Simple DTO for an email attachment.
    /// </summary>
    public class EmailAttachment
    {
        /// <summary>File name that appears in the recipient’s mail client.</summary>
        public string FileName { get; set; } = default!;

        /// <summary>Raw file bytes.</summary>
        public byte[] Content { get; set; } = default!;

        /// <summary>MIME type (e.g., \"application/pdf\", \"image/png\").</summary>
        public string MimeType { get; set; } = "application/octet-stream";
    }
}
