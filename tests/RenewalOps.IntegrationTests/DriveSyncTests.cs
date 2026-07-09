using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Infrastructure.Jobs;
using RenewalOps.Infrastructure.Persistence;

namespace RenewalOps.IntegrationTests;

public class DriveSyncTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DriveSyncTests(CustomWebApplicationFactory factory)
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

    private static Document NewDocument(Guid ownerId) => new()
    {
        OwnerId = ownerId,
        Title = "License",
        DocumentType = DocumentType.License,
        StorageKey = $"{ownerId}/{Guid.NewGuid()}/license.pdf",
        OriginalFileName = "license.pdf",
        ContentType = "application/pdf",
        SizeBytes = 3,
        Status = DocumentStatus.Active
    };

    private static async Task SeedFileAsync(IServiceProvider sp, Document doc)
    {
        var storage = sp.GetRequiredService<IStorageService>();
        using var ms = new MemoryStream("pdf"u8.ToArray());
        await storage.UploadAsync(doc.StorageKey, ms, doc.ContentType);
    }

    [Fact]
    public async Task Sync_With_Active_Connection_Persists_GoogleDriveFileId()
    {
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var doc = NewDocument(ownerId);
        db.Users.Add(NewUser(ownerId));
        db.Documents.Add(doc);
        db.GoogleConnections.Add(new GoogleConnection
        {
            UserId = ownerId,
            EncryptedRefreshToken = "encrypted-token",
            IsRevoked = false
        });
        await db.SaveChangesAsync();
        await SeedFileAsync(sp, doc);

        await sp.GetRequiredService<DriveSyncJob>().RunAsync(doc.Id);

        foreach (var e in db.ChangeTracker.Entries<Document>().ToList()) e.State = EntityState.Detached;
        var synced = await db.Documents.FindAsync(doc.Id);
        synced!.GoogleDriveFileId.Should().Be(FakeGoogleDriveClient.FileId);

        var audits = await db.AuditEvents.Where(a => a.DocumentId == doc.Id).ToListAsync();
        audits.Should().Contain(a => a.Action == nameof(AuditAction.DocumentUpdated));
    }

    [Fact]
    public async Task Sync_Without_Connection_Is_Skipped()
    {
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var doc = NewDocument(ownerId);
        db.Users.Add(NewUser(ownerId));
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        await SeedFileAsync(sp, doc);

        await sp.GetRequiredService<DriveSyncJob>().RunAsync(doc.Id);

        foreach (var e in db.ChangeTracker.Entries<Document>().ToList()) e.State = EntityState.Detached;
        var result = await db.Documents.FindAsync(doc.Id);
        result!.GoogleDriveFileId.Should().BeNull("no Google connection means Drive sync is skipped");
    }

    [Fact]
    public async Task Resync_After_Lost_FileId_Reuses_Same_Drive_File()
    {
        // Simulates a crash between Drive upload and persisting GoogleDriveFileId: the second
        // run has a null id but must converge on the same remote file (via the doc-id marker),
        // not create a duplicate.
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var doc = NewDocument(ownerId);
        db.Users.Add(NewUser(ownerId));
        db.Documents.Add(doc);
        db.GoogleConnections.Add(new GoogleConnection
        {
            UserId = ownerId,
            EncryptedRefreshToken = "encrypted-token",
            IsRevoked = false
        });
        await db.SaveChangesAsync();
        await SeedFileAsync(sp, doc);

        var job = sp.GetRequiredService<DriveSyncJob>();
        await job.RunAsync(doc.Id);
        var firstId = (await db.Documents.FindAsync(doc.Id))!.GoogleDriveFileId;

        // Wipe the persisted id to mimic the crash, then re-run.
        var reload = await db.Documents.FindAsync(doc.Id);
        reload!.GoogleDriveFileId = null;
        await db.SaveChangesAsync();

        await job.RunAsync(doc.Id);
        var secondId = (await db.Documents.FindAsync(doc.Id))!.GoogleDriveFileId;

        secondId.Should().Be(firstId, "the marker-based lookup must reuse the same Drive file");
    }

    [Fact]
    public async Task Sync_Is_Idempotent_When_Already_Synced()
    {
        var ownerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var doc = NewDocument(ownerId);
        doc.GoogleDriveFileId = "already-synced-id";
        db.Users.Add(NewUser(ownerId));
        db.Documents.Add(doc);
        db.GoogleConnections.Add(new GoogleConnection
        {
            UserId = ownerId,
            EncryptedRefreshToken = "encrypted-token",
            IsRevoked = false
        });
        await db.SaveChangesAsync();

        await sp.GetRequiredService<DriveSyncJob>().RunAsync(doc.Id);

        foreach (var e in db.ChangeTracker.Entries<Document>().ToList()) e.State = EntityState.Detached;
        var result = await db.Documents.FindAsync(doc.Id);
        result!.GoogleDriveFileId.Should().Be("already-synced-id", "an already-synced document must not be re-uploaded");
    }
}
