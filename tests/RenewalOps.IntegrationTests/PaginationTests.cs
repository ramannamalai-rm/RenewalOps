using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Infrastructure.Persistence;
using RenewalOps.Infrastructure.Persistence.Repositories;

namespace RenewalOps.IntegrationTests;

public class PaginationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PaginationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Paging_With_Identical_Timestamps_Should_Not_Duplicate_Or_Skip()
    {
        var ownerId = Guid.NewGuid();
        var sharedTimestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed 5 documents that all share the exact same CreatedUtc. Without a stable
        // secondary sort key, offset paging over these is non-deterministic and can
        // duplicate or skip rows at page boundaries.
        for (var i = 0; i < 5; i++)
        {
            db.Documents.Add(new Document
            {
                OwnerId = ownerId,
                Title = $"Doc {i}",
                DocumentType = DocumentType.Other,
                StorageKey = $"{ownerId}/{Guid.NewGuid()}/doc{i}.pdf",
                OriginalFileName = $"doc{i}.pdf",
                ContentType = "application/pdf",
                SizeBytes = 100,
                Status = DocumentStatus.Active,
                CreatedUtc = sharedTimestamp,
                UpdatedUtc = sharedTimestamp
            });
        }
        await db.SaveChangesAsync();

        var repo = new DocumentRepository(db);

        // Page through 5 rows two-at-a-time: pages of size 2 => [2, 2, 1].
        var seenIds = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var (items, totalCount) = await repo.ListAsync(
                ownerId, type: null, status: null,
                expiringWithinDays: null, search: null,
                page: page, pageSize: 2);

            totalCount.Should().Be(5);
            seenIds.AddRange(items.Select(d => d.Id));
        }

        // Every row appears exactly once across all pages: no duplicates, no skips.
        seenIds.Should().HaveCount(5);
        seenIds.Should().OnlyHaveUniqueItems("stable Id tiebreaker prevents boundary duplicates/skips");
    }
}
