using RenewalOps.Domain.Enums;

namespace RenewalOps.Domain.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public required string Title { get; set; }
    public DocumentType DocumentType { get; set; }

    public required string StorageKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }

    public DateTime? ExpiryDate { get; set; }
    public DateTime? IssueDate { get; set; }

    public string? RawExtractedText { get; set; }
    public string? GoogleDriveFileId { get; set; }
    public string? GoogleCalendarEventId { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public bool IsDeleted { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
