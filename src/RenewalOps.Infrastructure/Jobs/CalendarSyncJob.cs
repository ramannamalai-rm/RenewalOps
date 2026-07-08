using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Jobs;

/// <summary>
/// Upserts a Google Calendar reminder event for a document at (ExpiryDate - offset), default
/// 7 days. Runs when the expiry is set or changed; because it upserts by the stored
/// GoogleCalendarEventId, re-running moves the same event rather than creating duplicates.
/// Skips when there is no expiry or no active Google connection.
/// </summary>
public class CalendarSyncJob
{
    private const int DefaultOffsetDays = 7;

    private readonly IDocumentRepository _documentRepo;
    private readonly IGoogleConnectionRepository _connectionRepo;
    private readonly IGoogleCalendarClient _calendarClient;
    private readonly IAuditEventRepository _auditRepo;
    private readonly IConfiguration _config;
    private readonly ILogger<CalendarSyncJob> _logger;

    public CalendarSyncJob(
        IDocumentRepository documentRepo,
        IGoogleConnectionRepository connectionRepo,
        IGoogleCalendarClient calendarClient,
        IAuditEventRepository auditRepo,
        IConfiguration config,
        ILogger<CalendarSyncJob> logger)
    {
        _documentRepo = documentRepo;
        _connectionRepo = connectionRepo;
        _calendarClient = calendarClient;
        _auditRepo = auditRepo;
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _documentRepo.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            _logger.LogWarning("Calendar sync skipped: document {DocId} not found", documentId);
            return;
        }

        if (document.ExpiryDate is not { } expiry)
        {
            _logger.LogDebug("Calendar sync skipped: document {DocId} has no expiry date", documentId);
            return;
        }

        var connection = await _connectionRepo.GetByUserIdAsync(document.OwnerId, ct);
        if (connection is null || connection.IsRevoked)
        {
            _logger.LogInformation(
                "Calendar sync skipped: user {UserId} has no active Google connection", document.OwnerId);
            return;
        }

        var offsetDays = _config.GetValue("Google:CalendarReminderOffsetDays", DefaultOffsetDays);
        var eventDateUtc = DateTime.SpecifyKind(expiry, DateTimeKind.Utc).AddDays(-offsetDays);
        var summary = $"{document.Title} expires soon";
        var description =
            $"RenewalOps: '{document.Title}' ({document.DocumentType}) expires on {expiry:yyyy-MM-dd}.";

        var eventId = await _calendarClient.UpsertExpiryEventAsync(
            document.OwnerId, document.GoogleCalendarEventId, summary, description, eventDateUtc, ct);

        if (document.GoogleCalendarEventId != eventId)
        {
            document.GoogleCalendarEventId = eventId;
            await _documentRepo.UpdateAsync(document, ct);
        }

        await _auditRepo.AddAsync(new AuditEvent
        {
            ActorUserId = document.OwnerId,
            DocumentId = document.Id,
            Action = nameof(AuditAction.DocumentUpdated),
            PayloadJson = $"{{\"calendarSync\":true,\"eventId\":\"{eventId}\"}}"
        }, ct);

        _logger.LogInformation("Calendar sync completed for document {DocId}: event {EventId}", documentId, eventId);
    }
}
