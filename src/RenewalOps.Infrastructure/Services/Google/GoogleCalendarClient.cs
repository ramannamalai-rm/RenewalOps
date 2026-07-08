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

    private readonly GoogleCredentialFactory _credentialFactory;
    private readonly ILogger<GoogleCalendarClient> _logger;

    public GoogleCalendarClient(GoogleCredentialFactory credentialFactory, ILogger<GoogleCalendarClient> logger)
    {
        _credentialFactory = credentialFactory;
        _logger = logger;
    }

    public async Task<string> UpsertExpiryEventAsync(
        Guid userId, string? existingEventId, string summary, string description, DateTime eventDateUtc, CancellationToken ct = default)
    {
        var credential = await _credentialFactory.CreateAsync(userId, ct)
            ?? throw new InvalidOperationException($"No active Google connection for user {userId}.");

        using var calendar = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });

        var date = eventDateUtc.ToString("yyyy-MM-dd");
        var payload = new Event
        {
            Summary = summary,
            Description = description,
            // All-day event on the reminder date.
            Start = new EventDateTime { Date = date },
            End = new EventDateTime { Date = eventDateUtc.AddDays(1).ToString("yyyy-MM-dd") },
            Reminders = new Event.RemindersData
            {
                UseDefault = false,
                Overrides = [new EventReminder { Method = "popup", Minutes = 0 }]
            }
        };

        if (!string.IsNullOrEmpty(existingEventId))
        {
            try
            {
                var updated = await calendar.Events.Update(payload, CalendarId, existingEventId).ExecuteAsync(ct);
                _logger.LogInformation("Updated calendar event {EventId} for user {UserId}", updated.Id, userId);
                return updated.Id;
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // The stored event was deleted upstream; fall through and create a fresh one.
                _logger.LogWarning("Calendar event {EventId} not found; creating a new one", existingEventId);
            }
        }

        var created = await calendar.Events.Insert(payload, CalendarId).ExecuteAsync(ct);
        _logger.LogInformation("Created calendar event {EventId} for user {UserId}", created.Id, userId);
        return created.Id;
    }
}
