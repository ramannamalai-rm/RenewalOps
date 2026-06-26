using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Infrastructure.Persistence;

namespace RenewalOps.IntegrationTests;

public class ReminderSchedulingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReminderSchedulingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static Document NewDocument(Guid ownerId) => new()
    {
        OwnerId = ownerId,
        Title = "Passport",
        DocumentType = DocumentType.Passport,
        StorageKey = $"{ownerId}/{Guid.NewGuid()}/passport.pdf",
        OriginalFileName = "passport.pdf",
        ContentType = "application/pdf",
        SizeBytes = 1,
        Status = DocumentStatus.Active
    };

    [Fact]
    public async Task Expiry_31_Days_Out_Schedules_Three_Pending_Reminders()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var scheduler = sp.GetRequiredService<IReminderScheduler>();

        var doc = NewDocument(Guid.NewGuid());
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var expiry = DateTime.UtcNow.AddDays(31);
        await scheduler.ScheduleForDocumentAsync(doc.Id, expiry);

        var reminders = await db.ReminderRuns
            .Where(r => r.DocumentId == doc.Id)
            .OrderBy(r => r.ScheduledForUtc)
            .ToListAsync();

        reminders.Should().HaveCount(3, "T-30, T-7 and T-1 all fall in the future for a 31-day expiry");
        reminders.Should().OnlyContain(r => r.Status == ReminderStatus.Pending);
        reminders.Select(r => (int)Math.Round((expiry - r.ScheduledForUtc).TotalDays))
            .Should().BeEquivalentTo(new[] { 30, 7, 1 });
    }

    [Fact]
    public async Task Expiry_Within_Five_Days_Schedules_Only_The_Near_Reminder()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var scheduler = sp.GetRequiredService<IReminderScheduler>();

        var doc = NewDocument(Guid.NewGuid());
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        // Expiry 5 days out: T-30 and T-7 are in the past and must be skipped; only T-1 remains.
        var expiry = DateTime.UtcNow.AddDays(5);
        await scheduler.ScheduleForDocumentAsync(doc.Id, expiry);

        var reminders = await db.ReminderRuns
            .Where(r => r.DocumentId == doc.Id)
            .ToListAsync();

        reminders.Should().HaveCount(1, "only the T-1 reminder is still in the future");
        reminders[0].ScheduledForUtc.Should().BeCloseTo(expiry.AddDays(-1), TimeSpan.FromSeconds(5));
    }
}
