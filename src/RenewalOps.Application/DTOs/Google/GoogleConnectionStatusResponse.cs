namespace RenewalOps.Application.DTOs.Google;

public sealed class GoogleConnectionStatusResponse
{
    public bool Connected { get; init; }
    public bool Revoked { get; init; }
    public string? Scopes { get; init; }
    public DateTime? ConnectedUtc { get; init; }
}
