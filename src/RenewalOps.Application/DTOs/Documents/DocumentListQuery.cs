using RenewalOps.Domain.Enums;

namespace RenewalOps.Application.DTOs.Documents;

public sealed class DocumentListQuery
{
    public DocumentType? Type { get; init; }
    public DocumentStatus? Status { get; init; }
    public int? ExpiringWithinDays { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
