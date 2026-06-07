using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;

namespace RenewalOps.Domain.Interfaces;

public interface IDocumentRepository
{
    Task<Document> AddAsync(Document document, CancellationToken ct = default);
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Document>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);
    Task<(List<Document> Items, int TotalCount)> ListAsync(
        Guid? ownerId, DocumentType? type, DocumentStatus? status,
        int? expiringWithinDays, string? search,
        int page, int pageSize,
        CancellationToken ct = default);
    Task UpdateAsync(Document document, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}
