using Microsoft.Extensions.Logging;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Jobs;

/// <summary>
/// Fires a scheduled reminder: marks the ReminderRun as sent and logs to console.
/// Real delivery channels (email, calendar) arrive in Phase 4.
/// </summary>
public class ReminderJob
{
    private readonly IReminderRunRepository _reminderRepo;
    private readonly IDocumentRepository _documentRepo;
    private readonly IAuditEventRepository _auditRepo;
    private readonly ILogger<ReminderJob> _logger;

    public ReminderJob(
        IReminderRunRepository reminderRepo,
        IDocumentRepository documentRepo,
        IAuditEventRepository auditRepo,
        ILogger<ReminderJob> logger)
    {
        _reminderRepo = reminderRepo;
        _documentRepo = documentRepo;
        _auditRepo = auditRepo;
        _logger = logger;
    }

    public async Task RunAsync(Guid reminderRunId, CancellationToken ct = default)
    {
        var reminder = await _reminderRepo.GetByIdAsync(reminderRunId, ct);
        if (reminder is null)
        {
            _logger.LogWarning("Reminder job skipped: ReminderRun {ReminderRunId} not found", reminderRunId);
            return;
        }

        if (reminder.Status == ReminderStatus.Sent)
        {
            _logger.LogDebug("Reminder {ReminderRunId} already sent; skipping (idempotent)", reminderRunId);
            return;
        }

        reminder.ExecutedUtc = DateTime.UtcNow;
        reminder.Status = ReminderStatus.Sent;
        await _reminderRepo.UpdateAsync(reminder, ct);

        var document = await _documentRepo.GetByIdAsync(reminder.DocumentId, ct);

        _logger.LogInformation(
            "REMINDER [{Channel}] document {DocId} ('{Title}') expires soon (scheduled {ScheduledFor:o})",
            reminder.Channel, reminder.DocumentId, document?.Title ?? "unknown", reminder.ScheduledForUtc);

        if (document is not null)
        {
            await _auditRepo.AddAsync(new AuditEvent
            {
                ActorUserId = document.OwnerId,
                DocumentId = document.Id,
                Action = nameof(AuditAction.DocumentUpdated),
                PayloadJson = $"{{\"reminder\":true,\"channel\":\"{reminder.Channel}\"}}"
            }, ct);
        }
    }
}
