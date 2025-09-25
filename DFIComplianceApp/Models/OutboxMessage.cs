using SQLite;

public class OutboxMessage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public Guid FirebaseId { get; set; } = Guid.NewGuid(); // For Firebase

    public string Type { get; set; } = "Email";
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";

    // New fields
    public string CompanyName { get; set; } = "";
    public DateTime? MeetingDate { get; set; }
    public bool IsDirty { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public string Recipient { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string Body { get; set; } = "";
    public bool IsHtml { get; set; }
    public bool IsSent { get; set; } = false;
    public DateTime? SentAt { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
