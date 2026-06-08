using RenewalOps.Domain.Enums;

namespace RenewalOps.Application.DTOs.Documents;

public sealed class UploadDocumentCommand
{
    public required string Title { get; init; }
    public DocumentType DocumentType { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public long SizeBytes { get; init; }
}
