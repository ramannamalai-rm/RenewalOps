using RenewalOps.Application.DTOs.Documents;

namespace RenewalOps.Application.Interfaces;

public interface IDocumentService
{
    Task<DocumentResponse> UploadAsync(Guid ownerId, UploadDocumentCommand command, Stream fileStream, CancellationToken ct = default);
    Task<DocumentResponse?> GetByIdAsync(Guid documentId, Guid requesterId, string requesterRole, CancellationToken ct = default);
    Task<DocumentListResponse> ListAsync(Guid requesterId, string requesterRole, DocumentListQuery query, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid documentId, Guid requesterId, string requesterRole, CancellationToken ct = default);
}
