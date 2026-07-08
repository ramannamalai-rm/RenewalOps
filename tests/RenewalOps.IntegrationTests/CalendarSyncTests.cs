using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Infrastructure.Jobs;
using RenewalOps.Infrastructure.Persistence;

namespace RenewalOps.IntegrationTests;

public class CalendarSyncTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CalendarSyncTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static User NewUser(Guid id) => new()
    {
        Id = id,
        UserName = $"{id:N}@test.com",
        Email = $"{id:N}@test.com",
        Role = UserRole.Owner
    };

    private static Document NewDocument(Guid ownerId, DateTime? expiry) => new()
    {
        OwnerId = ownerId,
        Title = "Insurance",
        DocumentType = DocumentType.Insurance,
        StorageKey = $"{ownerId}/{Guid.NewGuid()}/ins.pdf",
        OriginalFileName = "ins.pdf",
        ContentType = "application/pdf",
        SizeBytes = 3,
        ExpiryDate = expiry,
        Status = DocumentStatus.Active
    };

    private static GoogleConnection ActiveConnection(Guid ownerId) => new()
    {
        UserId = ownerId,
        EncryptedRefreshToken = "encrypted-token",
        IsRevoked = false
    };

    [Fact]
    public async Task Sync_With_Expiry_And_Connection_Persists_CalendarEventId()
    {
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var doc = NewDocument(ownerId, DateTime.UtcNow.AddDays(60));
        db.Users.Add(NewUser(ownerId));
        db.Documents.Add(doc);
        db.GoogleConnections.Add(ActiveConnection(ownerId));
        await db.SaveChangesAsync();

        await sp.GetRequiredService<CalendarSyncJob>().RunAsync(doc.Id);

        foreach (var e in db.ChangeTracker.Entries<Document>().ToList()) e.State = EntityState.Detached;
        var synced = await db.Documents.FindAsync(doc.Id);
        synced!.GoogleCalendarEventId.Should().Be(FakeGoogleCalendarClient.NewEventId);
    }

    [Fact]
    public async Task Sync_Without_Expiry_Is_Skipped()
    {
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var doc = NewDocument(ownerId, expiry: null);
        db.Users.Add(NewUser(ownerId));
        db.Documents.Add(doc);
        db.GoogleConnections.Add(ActiveConnection(ownerId));
        await db.SaveChangesAsync();

        await sp.GetRequiredService<CalendarSyncJob>().RunAsync(doc.Id);

        foreach (var e in db.ChangeTracker.Entries<Document>().ToList()) e.State = EntityState.Detached;
        (await db.Documents.FindAsync(doc.Id))!.GoogleCalendarEventId.Should().BeNull();
    }

    [Fact]
    public async Task Sync_Without_Connection_Is_Skipped()
    {
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var doc = NewDocument(ownerId, DateTime.UtcNow.AddDays(60));
        db.Users.Add(NewUser(ownerId));
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        await sp.GetRequiredService<CalendarSyncJob>().RunAsync(doc.Id);

        foreach (var e in db.ChangeTracker.Entries<Document>().ToList()) e.State = EntityState.Detached;
        (await db.Documents.FindAsync(doc.Id))!.GoogleCalendarEventId.Should().BeNull();
    }

    [Fact]
    public async Task Resync_Upserts_The_Same_Event()
    {
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var doc = NewDocument(ownerId, DateTime.UtcNow.AddDays(60));
        doc.GoogleCalendarEventId = "existing-event-id";
        db.Users.Add(NewUser(ownerId));
        db.Documents.Add(doc);
        db.GoogleConnections.Add(ActiveConnection(ownerId));
        await db.SaveChangesAsync();

        await sp.GetRequiredService<CalendarSyncJob>().RunAsync(doc.Id);

        foreach (var e in db.ChangeTracker.Entries<Document>().ToList()) e.State = EntityState.Detached;
        (await db.Documents.FindAsync(doc.Id))!.GoogleCalendarEventId
            .Should().Be("existing-event-id", "re-sync must update the same event, not create a duplicate");
    }
}
