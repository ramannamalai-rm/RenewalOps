using RenewalOps.Domain.Entities;

namespace RenewalOps.Domain.Interfaces;

public interface IGoogleConnectionRepository
{
    Task<GoogleConnection?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Inserts a new connection for the user or updates the existing one (one per user).</summary>
    Task UpsertAsync(GoogleConnection connection, CancellationToken ct = default);
}
