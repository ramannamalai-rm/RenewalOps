using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Jobs;

public sealed class ReminderScheduler : IReminderScheduler
{
    private static readonly int[] DefaultOffsetsDays = [30, 7, 1];

    private readonly IReminderRunRepository _reminderRepo;
    private readonly IDocumentJobScheduler _jobScheduler;
    private readonly IConfiguration _config;
    private readonly ILogger<ReminderScheduler> _logger;

    public ReminderScheduler(
        IReminderRunRepository reminderRepo,
        IDocumentJobScheduler jobScheduler,
        IConfiguration config,
        ILogger<ReminderScheduler> logger)
    {
        _reminderRepo = reminderRepo;
        _jobScheduler = jobScheduler;
        _config = config;
        _logger = logger;
    }

    public async Task ScheduleForDocumentAsync(Guid documentId, DateTime expiryDateUtc, CancellationToken ct = default)
    {
        var offsets = _config.GetSection("Reminders:OffsetsDays").Get<int[]>();
        if (offsets is null || offsets.Length == 0)
            offsets = DefaultOffsetsDays;

        var now = DateTime.UtcNow;
        var reminders = new List<ReminderRun>();

        foreach (var offset in offsets.Distinct())
        {
            var scheduledForUtc = DateTime.SpecifyKind(expiryDateUtc, DateTimeKind.Utc).AddDays(-offset);
            if (scheduledForUtc <= now)
            {
                _logger.LogDebug(
                    "Skipping T-{Offset}d reminder for document {DocId}: {ScheduledFor:o} is in the past",
                    offset, documentId, scheduledForUtc);
                continue;
            }

            reminders.Add(new ReminderRun
            {
                DocumentId = documentId,
                ScheduledForUtc = scheduledForUtc,
                Channel = ReminderChannel.Email,
                Status = ReminderStatus.Pending
            });
        }

        if (reminders.Count == 0)
        {
            _logger.LogInformation(
                "No future reminders to schedule for document {DocId} (expiry {Expiry:o})", documentId, expiryDateUtc);
            return;
        }

        await _reminderRepo.AddRangeAsync(reminders, ct);

        foreach (var reminder in reminders)
            _jobScheduler.ScheduleReminderDispatch(reminder.Id, reminder.ScheduledForUtc);

        _logger.LogInformation(
            "Scheduled {Count} reminder(s) for document {DocId} (expiry {Expiry:o})",
            reminders.Count, documentId, expiryDateUtc);
    }
}
