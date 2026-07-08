using Microsoft.Extensions.Logging;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Jobs;

/// <summary>
/// Background job that runs OCR + expiry/issue-date parsing for an uploaded document and
/// persists the results. Triggered after upload (Phase 2) so the request thread is not
/// blocked on Tesseract.
/// </summary>
public class OcrProcessingJob
{
    private readonly IDocumentRepository _documentRepo;
    private readonly IAuditEventRepository _auditRepo;
    private readonly IStorageService _storage;
    private readonly IOcrService _ocr;
    private readonly IReminderScheduler _reminderScheduler;
    private readonly IDocumentJobScheduler _jobScheduler;
    private readonly ILogger<OcrProcessingJob> _logger;

    public OcrProcessingJob(
        IDocumentRepository documentRepo,
        IAuditEventRepository auditRepo,
        IStorageService storage,
        IOcrService ocr,
        IReminderScheduler reminderScheduler,
        IDocumentJobScheduler jobScheduler,
        ILogger<OcrProcessingJob> logger)
    {
        _documentRepo = documentRepo;
        _auditRepo = auditRepo;
        _storage = storage;
        _ocr = ocr;
        _reminderScheduler = reminderScheduler;
        _jobScheduler = jobScheduler;
        _logger = logger;
    }

    public async Task RunAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _documentRepo.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            _logger.LogWarning("OCR job skipped: document {DocId} not found", documentId);
            return;
        }

        await using var fileStream = await _storage.DownloadAsync(document.StorageKey, ct);
        var ocrResult = await _ocr.ExtractTextAsync(fileStream, document.ContentType, ct);

        document.RawExtractedText = string.IsNullOrWhiteSpace(ocrResult.RawText) ? null : ocrResult.RawText;
        document.ExpiryDate = ocrResult.DetectedExpiryDate;
        document.IssueDate = ocrResult.DetectedIssueDate;

        await _documentRepo.UpdateAsync(document, ct);

        await _auditRepo.AddAsync(new AuditEvent
        {
            ActorUserId = document.OwnerId,
            DocumentId = document.Id,
            Action = nameof(AuditAction.DocumentUpdated),
            PayloadJson = $"{{\"ocr\":true,\"expiryDate\":\"{ocrResult.DetectedExpiryDate:o}\"}}"
        }, ct);

        if (ocrResult.DetectedExpiryDate is { } expiry)
        {
            await _reminderScheduler.ScheduleForDocumentAsync(document.Id, expiry, ct);
            // Expiry is now known/changed — upsert the Google Calendar reminder event.
            _jobScheduler.EnqueueCalendarSync(document.Id);
        }

        _logger.LogInformation(
            "OCR completed for document {DocId}: expiry={Expiry}", documentId, ocrResult.DetectedExpiryDate);
    }
}
