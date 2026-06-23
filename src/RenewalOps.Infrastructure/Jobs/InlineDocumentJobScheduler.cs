using Microsoft.Extensions.DependencyInjection;
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

    public InlineDocumentJobScheduler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void EnqueueOcrProcessing(Guid documentId)
    {
        using var scope = _serviceProvider.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<OcrProcessingJob>();
        job.RunAsync(documentId, CancellationToken.None).GetAwaiter().GetResult();
    }
}
