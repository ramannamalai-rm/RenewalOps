using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RenewalOps.Application.Interfaces;

namespace RenewalOps.Infrastructure.Jobs;

/// <summary>
/// Fallback scheduler used when the Hangfire server is not running (e.g. BackgroundJobs
/// disabled, or the DB-free integration-test host). Runs the job synchronously in a fresh
/// DI scope — mirroring how Hangfire would activate it — so behavior stays correct, just
/// not off-thread. Production with jobs enabled uses <see cref="HangfireDocumentJobScheduler"/>.
/// </summary>
public sealed class InlineDocumentJobScheduler : IDocumentJobScheduler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InlineDocumentJobScheduler> _logger;

    public InlineDocumentJobScheduler(
        IServiceProvider serviceProvider,
        ILogger<InlineDocumentJobScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void EnqueueOcrProcessing(Guid documentId)
    {
        using var scope = _serviceProvider.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<OcrProcessingJob>();
        job.RunAsync(documentId, CancellationToken.None).GetAwaiter().GetResult();
    }

    public void ScheduleReminderDispatch(Guid reminderRunId, DateTime runAtUtc)
    {
        // No background server to hold a delayed job. The Pending ReminderRun row already
        // records the schedule; firing it would require a running Hangfire server.
        _logger.LogDebug(
            "Inline scheduler: reminder {ReminderRunId} recorded for {RunAt:o} but not dispatched (no job server)",
            reminderRunId, runAtUtc);
    }
}
