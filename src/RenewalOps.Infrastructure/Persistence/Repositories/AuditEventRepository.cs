using Microsoft.EntityFrameworkCore;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Persistence.Repositories;

public class AuditEventRepository : IAuditEventRepository
{
    private readonly AppDbContext _db;

    public AuditEventRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        _db.AuditEvents.Add(auditEvent);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<AuditEvent>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        return await _db.AuditEvents
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.CreatedUtc)
            .ToListAsync(ct);
    }
}
