namespace RenewalOps.Application.Interfaces;

/// <summary>
/// Schedules background work for documents. Abstracts the job runner (Hangfire) so the
/// Application layer stays free of infrastructure concerns.
/// </summary>
public interface IDocumentJobScheduler
{
    /// <summary>Queues OCR + expiry parsing for a freshly uploaded document.</summary>
    void EnqueueOcrProcessing(Guid documentId);
}
