namespace RenewalOps.Application.Interfaces;

/// <summary>
/// Schedules background work for documents. Abstracts the job runner (Hangfire) so the
/// Application layer stays free of infrastructure concerns.
/// </summary>
public interface IDocumentJobScheduler
{
    /// <summary>Queues OCR + expiry parsing for a freshly uploaded document.</summary>
    void EnqueueOcrProcessing(Guid documentId);

    /// <summary>Queues syncing a document's original file to the owner's Google Drive.</summary>
    void EnqueueDriveSync(Guid documentId);

    /// <summary>Queues upserting the document's expiry reminder event in the owner's Google Calendar.</summary>
    void EnqueueCalendarSync(Guid documentId);

    /// <summary>
    /// Dispatches a single reminder to fire at <paramref name="runAtUtc"/>. With a job server
    /// running this becomes a delayed Hangfire job; without one it is a no-op (the Pending
    /// ReminderRun row still records that the reminder was scheduled).
    /// </summary>
    void ScheduleReminderDispatch(Guid reminderRunId, DateTime runAtUtc);
}
