namespace RenewalOps.Application.Interfaces;

/// <summary>
/// Thin wrapper over Google Calendar operations used by the sync job. Implemented in
/// Infrastructure with the Calendar v3 SDK; faked in tests.
/// </summary>
public interface IGoogleCalendarClient
{
    /// <summary>
    /// Creates or updates ("upserts") the expiry reminder event for a document. When
    /// <paramref name="existingEventId"/> is supplied the existing event is updated (so a
    /// changed expiry moves the same event); otherwise a new event is created. Returns the
    /// event id.
    /// </summary>
    Task<string> UpsertExpiryEventAsync(
        Guid userId,
        string? existingEventId,
        string summary,
        string description,
        DateTime eventDateUtc,
        CancellationToken ct = default);
}
