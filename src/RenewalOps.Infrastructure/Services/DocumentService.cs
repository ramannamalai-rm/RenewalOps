using Microsoft.Extensions.Logging;
using RenewalOps.Application.DTOs.Documents;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepo;
    private readonly IAuditEventRepository _auditRepo;
    private readonly IStorageService _storage;
    private readonly IOcrService _ocr;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documentRepo,
        IAuditEventRepository auditRepo,
        IStorageService storage,
        IOcrService ocr,
        ILogger<DocumentService> logger)
    {
        _documentRepo = documentRepo;
        _auditRepo = auditRepo;
        _storage = storage;
        _ocr = ocr;
        _logger = logger;
    }

    public async Task<DocumentResponse> UploadAsync(
        Guid ownerId, UploadDocumentCommand command, Stream fileStream, CancellationToken ct = default)
    {
        var storageKey = $"{ownerId}/{Guid.NewGuid()}/{command.FileName}";

        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        await _storage.UploadAsync(storageKey, buffer, command.ContentType, ct);

        buffer.Position = 0;
        var ocrResult = await _ocr.ExtractTextAsync(buffer, command.ContentType, ct);

        var document = new Document
        {
            OwnerId = ownerId,
            Title = command.Title,
            DocumentType = command.DocumentType,
            StorageKey = storageKey,
            OriginalFileName = command.FileName,
            ContentType = command.ContentType,
            SizeBytes = command.SizeBytes,
            ExpiryDate = ocrResult.DetectedExpiryDate,
            IssueDate = ocrResult.DetectedIssueDate,
            RawExtractedText = string.IsNullOrWhiteSpace(ocrResult.RawText) ? null : ocrResult.RawText,
            Status = DocumentStatus.Active
        };

        await _documentRepo.AddAsync(document, ct);

        await _auditRepo.AddAsync(new AuditEvent
        {
            ActorUserId = ownerId,
            DocumentId = document.Id,
            Action = nameof(AuditAction.DocumentUploaded)
        }, ct);

        _logger.LogInformation("Document {DocId} uploaded by {OwnerId}", document.Id, ownerId);
        return DocumentResponse.FromEntity(document);
    }

    public async Task<DocumentResponse?> GetByIdAsync(
        Guid documentId, Guid requesterId, string requesterRole, CancellationToken ct = default)
    {
        var doc = await _documentRepo.GetByIdAsync(documentId, ct);
        if (doc is null) return null;

        if (!CanAccess(doc, requesterId, requesterRole))
            return null;

        await _auditRepo.AddAsync(new AuditEvent
        {
            ActorUserId = requesterId,
            DocumentId = doc.Id,
            Action = nameof(AuditAction.DocumentViewed)
        }, ct);

        return DocumentResponse.FromEntity(doc);
    }

    public async Task<DocumentListResponse> ListAsync(
        Guid requesterId, string requesterRole, DocumentListQuery query, CancellationToken ct = default)
    {
        var isAdmin = string.Equals(requesterRole, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase);
        Guid? ownerId = isAdmin ? null : requesterId;

        var (items, totalCount) = await _documentRepo.ListAsync(
            ownerId, query.Type, query.Status,
            query.ExpiringWithinDays, query.Search,
            query.Page, query.PageSize, ct);

        return new DocumentListResponse
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<bool> DeleteAsync(
        Guid documentId, Guid requesterId, string requesterRole, CancellationToken ct = default)
    {
        var doc = await _documentRepo.GetByIdAsync(documentId, ct);
        if (doc is null) return false;

        if (!CanAccess(doc, requesterId, requesterRole))
            return false;

        await _documentRepo.SoftDeleteAsync(documentId, ct);

        await _auditRepo.AddAsync(new AuditEvent
        {
            ActorUserId = requesterId,
            DocumentId = documentId,
            Action = nameof(AuditAction.DocumentDeleted)
        }, ct);

        _logger.LogInformation("Document {DocId} soft-deleted by {UserId}", documentId, requesterId);
        return true;
    }

    private static bool CanAccess(Document doc, Guid requesterId, string requesterRole)
    {
        if (string.Equals(requesterRole, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase))
            return true;

        return doc.OwnerId == requesterId;
    }
}
