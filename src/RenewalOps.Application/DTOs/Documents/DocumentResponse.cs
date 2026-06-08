using RenewalOps.Domain.Entities;

namespace RenewalOps.Application.DTOs.Documents;

public sealed class DocumentResponse
{
    public Guid Id { get; init; }
    public Guid OwnerId { get; init; }
    public required string Title { get; init; }
    public required string DocumentType { get; init; }
    public required string StorageKey { get; init; }
    public required string OriginalFileName { get; init; }
    public required string ContentType { get; init; }
    public long SizeBytes { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public DateTime? IssueDate { get; init; }
    public string? RawExtractedText { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime? UpdatedUtc { get; init; }

    public static DocumentResponse FromEntity(Document doc) => new()
    {
        Id = doc.Id,
        OwnerId = doc.OwnerId,
        Title = doc.Title,
        DocumentType = doc.DocumentType.ToString(),
        StorageKey = doc.StorageKey,
        OriginalFileName = doc.OriginalFileName,
        ContentType = doc.ContentType,
        SizeBytes = doc.SizeBytes,
        ExpiryDate = doc.ExpiryDate,
        IssueDate = doc.IssueDate,
        RawExtractedText = doc.RawExtractedText,
        Status = doc.Status.ToString(),
        CreatedUtc = doc.CreatedUtc,
        UpdatedUtc = doc.UpdatedUtc
    };
}
