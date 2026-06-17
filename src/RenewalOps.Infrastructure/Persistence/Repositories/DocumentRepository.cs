using Microsoft.EntityFrameworkCore;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Persistence.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;

    public DocumentRepository(AppDbContext db) => _db = db;

    public async Task<Document> AddAsync(Document document, CancellationToken ct = default)
    {
        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);
        return document;
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Documents
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<List<Document>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await _db.Documents
            .Where(d => d.OwnerId == ownerId)
            .OrderByDescending(d => d.CreatedUtc)
            .ThenByDescending(d => d.Id)
            .ToListAsync(ct);
    }

    public async Task<(List<Document> Items, int TotalCount)> ListAsync(
        Guid? ownerId, DocumentType? type, DocumentStatus? status,
        int? expiringWithinDays, string? search,
        int page, int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Documents.AsQueryable();

        if (ownerId.HasValue)
            query = query.Where(d => d.OwnerId == ownerId.Value);

        if (type.HasValue)
            query = query.Where(d => d.DocumentType == type.Value);

        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        if (expiringWithinDays.HasValue)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(expiringWithinDays.Value);
            query = query.Where(d => d.ExpiryDate != null && d.ExpiryDate > now && d.ExpiryDate <= cutoff);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(d =>
                EF.Functions.ILike(d.Title, pattern) ||
                (d.RawExtractedText != null && EF.Functions.ILike(d.RawExtractedText, pattern)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.CreatedUtc)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task UpdateAsync(Document document, CancellationToken ct = default)
    {
        document.UpdatedUtc = DateTime.UtcNow;
        _db.Documents.Update(document);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var document = await _db.Documents.FindAsync([id], ct);
        if (document is null) return;

        document.IsDeleted = true;
        document.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
