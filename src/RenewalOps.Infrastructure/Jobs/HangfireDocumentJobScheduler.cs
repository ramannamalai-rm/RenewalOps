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
}
