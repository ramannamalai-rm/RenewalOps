namespace RenewalOps.Application.DTOs.Documents;

public sealed class DocumentListResponse
{
    public required List<DocumentResponse> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
