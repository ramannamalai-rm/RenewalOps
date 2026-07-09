using Microsoft.Extensions.Logging;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Jobs;

/// <summary>
/// Copies a document's original file into the owner's Google Drive "RenewalOps" folder and
/// persists the resulting Drive file id. Skips silently when the owner has no active Google
/// connection, and is idempotent: a document that already has a GoogleDriveFileId is not
/// re-uploaded.
/// </summary>
public class DriveSyncJob
{
    private readonly IDocumentRepository _documentRepo;
    private readonly IGoogleConnectionRepository _connectionRepo;
    private readonly IStorageService _storage;
    private readonly IGoogleDriveClient _driveClient;
    private readonly IAuditEventRepository _auditRepo;
    private readonly ILogger<DriveSyncJob> _logger;

    public DriveSyncJob(
        IDocumentRepository documentRepo,
        IGoogleConnectionRepository connectionRepo,
        IStorageService storage,
        IGoogleDriveClient driveClient,
        IAuditEventRepository auditRepo,
        ILogger<DriveSyncJob> logger)
    {
        _documentRepo = documentRepo;
        _connectionRepo = connectionRepo;
        _storage = storage;
        _driveClient = driveClient;
        _auditRepo = auditRepo;
        _logger = logger;
    }

    public async Task RunAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _documentRepo.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            _logger.LogWarning("Drive sync skipped: document {DocId} not found", documentId);
            return;
        }

        if (!string.IsNullOrEmpty(document.GoogleDriveFileId))
        {
            _logger.LogDebug("Drive sync skipped: document {DocId} already synced ({FileId})",
                documentId, document.GoogleDriveFileId);
            return;
        }

        var connection = await _connectionRepo.GetByUserIdAsync(document.OwnerId, ct);
        if (connection is null || connection.IsRevoked)
        {
            _logger.LogInformation(
                "Drive sync skipped: user {UserId} has no active Google connection", document.OwnerId);
            return;
        }

        await using var fileStream = await _storage.DownloadAsync(document.StorageKey, ct);
        var fileId = await _driveClient.UploadToRenewalOpsFolderAsync(
            document.OwnerId, document.Id, document.OriginalFileName, document.ContentType, fileStream, ct);

        document.GoogleDriveFileId = fileId;
        await _documentRepo.UpdateAsync(document, ct);

        await _auditRepo.AddAsync(new AuditEvent
        {
            ActorUserId = document.OwnerId,
            DocumentId = document.Id,
            Action = nameof(AuditAction.DocumentUpdated),
            PayloadJson = $"{{\"driveSync\":true,\"fileId\":\"{fileId}\"}}"
        }, ct);

        _logger.LogInformation("Drive sync completed for document {DocId}: {FileId}", documentId, fileId);
    }
}
