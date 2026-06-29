using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Infrastructure.Jobs;
using RenewalOps.Infrastructure.Persistence;

namespace RenewalOps.IntegrationTests;

public class StatusRecomputeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StatusRecomputeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static Document NewDocument(Guid ownerId, string title, DateTime expiryUtc) => new()
    {
        OwnerId = ownerId,
        Title = title,
        DocumentType = DocumentType.License,
        StorageKey = $"{ownerId}/{Guid.NewGuid()}/{title}.pdf",
        OriginalFileName = $"{title}.pdf",
        ContentType = "application/pdf",
        SizeBytes = 1,
        ExpiryDate = expiryUtc,
        Status = DocumentStatus.Active
    };

    [Fact]
    public async Task Recompute_Sets_Expired_ExpiringSoon_And_Leaves_Active()
    {
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var job = sp.GetRequiredService<StatusRecomputeJob>();

        var expired = NewDocument(ownerId, "expired", DateTime.UtcNow.AddDays(-2));
        var soon = NewDocument(ownerId, "soon", DateTime.UtcNow.AddDays(10));   // within 30d window
        var active = NewDocument(ownerId, "active", DateTime.UtcNow.AddDays(100));
        db.Documents.AddRange(expired, soon, active);
        await db.SaveChangesAsync();

        await job.RunAsync();

        // Detach so the reads reflect persisted state, not the tracked instances.
        foreach (var entry in db.ChangeTracker.Entries<Document>().ToList())
            entry.State = EntityState.Detached;

        (await db.Documents.FindAsync(expired.Id))!.Status.Should().Be(DocumentStatus.Expired);
        (await db.Documents.FindAsync(soon.Id))!.Status.Should().Be(DocumentStatus.ExpiringSoon);
        (await db.Documents.FindAsync(active.Id))!.Status.Should().Be(DocumentStatus.Active);
    }

    [Fact]
    public async Task Recompute_Leaves_Renewed_Documents_Untouched()
    {
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var job = sp.GetRequiredService<StatusRecomputeJob>();

        var renewed = NewDocument(ownerId, "renewed", DateTime.UtcNow.AddDays(-5));
        renewed.Status = DocumentStatus.Renewed;
        db.Documents.Add(renewed);
        await db.SaveChangesAsync();

        await job.RunAsync();

        foreach (var entry in db.ChangeTracker.Entries<Document>().ToList())
            entry.State = EntityState.Detached;

        (await db.Documents.FindAsync(renewed.Id))!.Status.Should().Be(DocumentStatus.Renewed,
            "Renewed is terminal and must not be recomputed to Expired");
    }
}
