using Hangfire;
using RenewalOps.Application.Interfaces;

namespace RenewalOps.Infrastructure.Jobs;

/// <summary>
/// Production scheduler: enqueues document jobs onto Hangfire's PostgreSQL-backed queue.
/// </summary>
public sealed class HangfireDocumentJobScheduler : IDocumentJobScheduler
{
    private readonly IBackgroundJobClient _jobClient;

    public HangfireDocumentJobScheduler(IBackgroundJobClient jobClient)
    {
        _jobClient = jobClient;
    }

    public void EnqueueOcrProcessing(Guid documentId)
    {
        _jobClient.Enqueue<OcrProcessingJob>(job => job.RunAsync(documentId, CancellationToken.None));
    }

    public void EnqueueDriveSync(Guid documentId)
    {
        _jobClient.Enqueue<DriveSyncJob>(job => job.RunAsync(documentId, CancellationToken.None));
    }

    public void ScheduleReminderDispatch(Guid reminderRunId, DateTime runAtUtc)
    {
        var enqueueAt = new DateTimeOffset(DateTime.SpecifyKind(runAtUtc, DateTimeKind.Utc));
        _jobClient.Schedule<ReminderJob>(job => job.RunAsync(reminderRunId, CancellationToken.None), enqueueAt);
    }
}
