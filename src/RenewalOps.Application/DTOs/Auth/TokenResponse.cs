namespace RenewalOps.Application.DTOs.Auth;

public sealed class TokenResponse
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public DateTime ExpiresUtc { get; init; }
}
