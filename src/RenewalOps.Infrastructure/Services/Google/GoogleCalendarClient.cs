using Google;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using RenewalOps.Application.Interfaces;

namespace RenewalOps.Infrastructure.Services.Google;

public sealed class GoogleCalendarClient : IGoogleCalendarClient
{
    private const string ApplicationName = "RenewalOps";
    private const string CalendarId = "primary";
    private const string DocumentIdProperty = "renewalOpsDocumentId";

    private readonly GoogleCredentialFactory _credentialFactory;
    private readonly ILogger<GoogleCalendarClient> _logger;

    public GoogleCalendarClient(GoogleCredentialFactory credentialFactory, ILogger<GoogleCalendarClient> logger)
    {
        _credentialFactory = credentialFactory;
        _logger = logger;
    }

    public async Task<string> UpsertExpiryEventAsync(
        Guid userId, Guid documentId, string? existingEventId, string summary, string description, DateTime eventDateUtc, CancellationToken ct = default)
    {
        var credential = await _credentialFactory.CreateAsync(userId, ct)
            ?? throw new InvalidOperationException($"No active Google connection for user {userId}.");

        using var calendar = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });

        var payload = new Event
        {
            Summary = summary,
            Description = description,
            // All-day event on the reminder date.
            Start = new EventDateTime { Date = eventDateUtc.ToString("yyyy-MM-dd") },
            End = new EventDateTime { Date = eventDateUtc.AddDays(1).ToString("yyyy-MM-dd") },
            Reminders = new Event.RemindersData
            {
                UseDefault = false,
                Overrides = [new EventReminder { Method = "popup", Minutes = 0 }]
            },
            // Idempotency marker so the event can be located even if its id was never persisted.
            ExtendedProperties = new Event.ExtendedPropertiesData
            {
                Private__ = new Dictionary<string, string> { [DocumentIdProperty] = documentId.ToString("N") }
            }
        };

        // Prefer the explicitly persisted id; otherwise recover the event by its marker.
        var targetEventId = existingEventId ?? await FindExistingEventIdAsync(calendar, documentId, ct);

        if (!string.IsNullOrEmpty(targetEventId))
        {
            try
            {
                var updated = await calendar.Events.Update(payload, CalendarId, targetEventId).ExecuteAsync(ct);
                _logger.LogInformation("Updated calendar event {EventId} for user {UserId}", updated.Id, userId);
                return updated.Id;
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // The event was deleted upstream; fall through and create a fresh one.
                _logger.LogWarning("Calendar event {EventId} not found; creating a new one", targetEventId);
            }
        }

        var created = await calendar.Events.Insert(payload, CalendarId).ExecuteAsync(ct);
        _logger.LogInformation("Created calendar event {EventId} for user {UserId}", created.Id, userId);
        return created.Id;
    }

    private static async Task<string?> FindExistingEventIdAsync(CalendarService calendar, Guid documentId, CancellationToken ct)
    {
        var list = calendar.Events.List(CalendarId);
        list.PrivateExtendedProperty = $"{DocumentIdProperty}={documentId:N}";
        list.ShowDeleted = false;
        list.MaxResults = 1;
        var result = await list.ExecuteAsync(ct);
        return result.Items is { Count: > 0 } ? result.Items[0].Id : null;
    }
}
