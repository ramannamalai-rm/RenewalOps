using Microsoft.EntityFrameworkCore;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Persistence.Repositories;

public class GoogleConnectionRepository : IGoogleConnectionRepository
{
    private readonly AppDbContext _db;

    public GoogleConnectionRepository(AppDbContext db) => _db = db;

    public async Task<GoogleConnection?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.GoogleConnections.FirstOrDefaultAsync(c => c.UserId == userId, ct);
    }

    public async Task UpsertAsync(GoogleConnection connection, CancellationToken ct = default)
    {
        var existing = await _db.GoogleConnections.FirstOrDefaultAsync(c => c.UserId == connection.UserId, ct);
        if (existing is null)
        {
            _db.GoogleConnections.Add(connection);
        }
        else
        {
            existing.EncryptedRefreshToken = connection.EncryptedRefreshToken;
            existing.Scopes = connection.Scopes;
            existing.IsRevoked = connection.IsRevoked;
            existing.UpdatedUtc = DateTime.UtcNow;
            _db.GoogleConnections.Update(existing);
        }

        await _db.SaveChangesAsync(ct);
    }
}
