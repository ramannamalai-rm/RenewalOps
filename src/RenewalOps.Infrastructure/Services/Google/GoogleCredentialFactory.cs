using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Services.Google;

/// <summary>
/// Builds an authenticated Google <see cref="UserCredential"/> for a user from their stored
/// (encrypted) refresh token. Shared by the Drive and Calendar sync clients. Returns null
/// when the user has no connection or it is revoked.
/// </summary>
public sealed class GoogleCredentialFactory
{
    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/drive.file",
        "https://www.googleapis.com/auth/calendar.events"
    ];

    private readonly IGoogleConnectionRepository _connectionRepo;
    private readonly IConfiguration _config;
    private readonly IDataProtector _refreshTokenProtector;

    public GoogleCredentialFactory(
        IGoogleConnectionRepository connectionRepo,
        IConfiguration config,
        IDataProtectionProvider dataProtectionProvider)
    {
        _connectionRepo = connectionRepo;
        _config = config;
        _refreshTokenProtector = dataProtectionProvider.CreateProtector("Google:RefreshToken");
    }

    public async Task<UserCredential?> CreateAsync(Guid userId, CancellationToken ct = default)
    {
        var connection = await _connectionRepo.GetByUserIdAsync(userId, ct);
        if (connection is null || connection.IsRevoked)
            return null;

        var refreshToken = _refreshTokenProtector.Unprotect(connection.EncryptedRefreshToken);

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _config["Google:ClientId"],
                ClientSecret = _config["Google:ClientSecret"]
            },
            Scopes = Scopes
        });

        // The credential refreshes access tokens on demand using the stored refresh token.
        return new UserCredential(flow, userId.ToString("N"), new TokenResponse { RefreshToken = refreshToken });
    }
}
