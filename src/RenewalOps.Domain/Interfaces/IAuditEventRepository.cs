using RenewalOps.Domain.Entities;

namespace RenewalOps.Domain.Interfaces;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default);
    Task<List<AuditEvent>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
}
