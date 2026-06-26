namespace RenewalOps.Application.Interfaces;

/// <summary>
/// Creates reminder schedules for a document based on its expiry date.
/// </summary>
public interface IReminderScheduler
{
    /// <summary>
    /// Schedules reminders at the configured offsets before <paramref name="expiryDateUtc"/>
    /// (default T-30d / T-7d / T-1d). Offsets that fall in the past are skipped. Persists a
    /// Pending ReminderRun per scheduled reminder and dispatches a delayed job for each.
    /// </summary>
    Task ScheduleForDocumentAsync(Guid documentId, DateTime expiryDateUtc, CancellationToken ct = default);
}
