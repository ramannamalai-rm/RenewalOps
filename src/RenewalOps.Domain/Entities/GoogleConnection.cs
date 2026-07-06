namespace RenewalOps.Domain.Entities;

/// <summary>
/// A user's Google account connection. Stores the OAuth refresh token (encrypted at rest)
/// used to obtain access tokens for Drive/Calendar sync.
/// </summary>
public class GoogleConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Refresh token, encrypted via ASP.NET Core Data Protection. Never stored in plaintext.</summary>
    public required string EncryptedRefreshToken { get; set; }

    /// <summary>Space-delimited granted scopes.</summary>
    public string? Scopes { get; set; }

    /// <summary>Set when Google reports the token as revoked/invalid; sync is disabled until reconnected.</summary>
    public bool IsRevoked { get; set; }

    public DateTime ConnectedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
