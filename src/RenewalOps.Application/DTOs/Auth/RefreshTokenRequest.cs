namespace RenewalOps.Application.DTOs.Auth;

public sealed class RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}
