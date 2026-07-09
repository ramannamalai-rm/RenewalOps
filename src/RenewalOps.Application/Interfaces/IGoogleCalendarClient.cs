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
    /// changed expiry moves the same event). Otherwise the event is located by its document-id
    /// marker before falling back to creating a new one — so a re-run never duplicates, even
    /// if the event id was never persisted locally. Returns the event id.
    /// </summary>
    Task<string> UpsertExpiryEventAsync(
        Guid userId,
        Guid documentId,
        string? existingEventId,
        string summary,
        string description,
        DateTime eventDateUtc,
        CancellationToken ct = default);
}
