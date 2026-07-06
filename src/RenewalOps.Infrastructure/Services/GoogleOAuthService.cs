using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RenewalOps.Application.DTOs.Google;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;
using RenewalOps.Domain.Interfaces;

namespace RenewalOps.Infrastructure.Services;

/// <summary>
/// Google OAuth 2.0 authorization-code flow. Builds the consent URL, exchanges the code for
/// tokens, and persists the refresh token encrypted via ASP.NET Core Data Protection.
/// </summary>
public sealed class GoogleOAuthService : IGoogleOAuthService
{
    private const string DefaultAuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string DefaultTokenEndpoint = "https://oauth2.googleapis.com/token";
    private static readonly string[] DefaultScopes =
    [
        "https://www.googleapis.com/auth/drive.file",
        "https://www.googleapis.com/auth/calendar.events"
    ];
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    private readonly IGoogleConnectionRepository _connectionRepo;
    private readonly IAuditEventRepository _auditRepo;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _refreshTokenProtector;
    private readonly IDataProtector _stateProtector;
    private readonly ILogger<GoogleOAuthService> _logger;

    public GoogleOAuthService(
        IGoogleConnectionRepository connectionRepo,
        IAuditEventRepository auditRepo,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<GoogleOAuthService> logger)
    {
        _connectionRepo = connectionRepo;
        _auditRepo = auditRepo;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _refreshTokenProtector = dataProtectionProvider.CreateProtector("Google:RefreshToken");
        _stateProtector = dataProtectionProvider.CreateProtector("Google:OAuthState");
        _logger = logger;
    }

    public string BuildConnectUrl(Guid userId)
    {
        var clientId = RequireConfig("Google:ClientId");
        var redirectUri = RequireConfig("Google:RedirectUri");

        var state = _stateProtector.Protect($"{userId:N}|{DateTime.UtcNow.Ticks}");

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = clientId;
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["scope"] = string.Join(' ', GetScopes());
        query["access_type"] = "offline";       // required to receive a refresh token
        query["prompt"] = "consent";            // force refresh-token issuance on reconnect
        query["include_granted_scopes"] = "true";
        query["state"] = state;

        var authEndpoint = _config["Google:AuthEndpoint"] ?? DefaultAuthEndpoint;
        return $"{authEndpoint}?{query}";
    }

    public async Task<Guid> HandleCallbackAsync(string code, string state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Authorization code is required.", nameof(code));

        var userId = ValidateState(state);

        var tokenResponse = await ExchangeCodeAsync(code, ct);
        if (string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            // Google only returns a refresh token with access_type=offline + prompt=consent.
            throw new InvalidOperationException(
                "Google did not return a refresh token. Ensure the consent screen was shown (prompt=consent).");
        }

        var connection = new GoogleConnection
        {
            UserId = userId,
            EncryptedRefreshToken = _refreshTokenProtector.Protect(tokenResponse.RefreshToken),
            Scopes = tokenResponse.Scope,
            IsRevoked = false,
            ConnectedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        await _connectionRepo.UpsertAsync(connection, ct);

        await _auditRepo.AddAsync(new AuditEvent
        {
            ActorUserId = userId,
            Action = nameof(AuditAction.GoogleConnected)
        }, ct);

        _logger.LogInformation("Google account connected for user {UserId}", userId);
        return userId;
    }

    public async Task<GoogleConnectionStatusResponse> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var connection = await _connectionRepo.GetByUserIdAsync(userId, ct);
        if (connection is null)
            return new GoogleConnectionStatusResponse { Connected = false };

        return new GoogleConnectionStatusResponse
        {
            Connected = !connection.IsRevoked,
            Revoked = connection.IsRevoked,
            Scopes = connection.Scopes,
            ConnectedUtc = connection.ConnectedUtc
        };
    }

    private async Task<GoogleTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct)
    {
        var tokenEndpoint = _config["Google:TokenEndpoint"] ?? DefaultTokenEndpoint;
        var form = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = RequireConfig("Google:ClientId"),
            ["client_secret"] = RequireConfig("Google:ClientSecret"),
            ["redirect_uri"] = RequireConfig("Google:RedirectUri"),
            ["grant_type"] = "authorization_code"
        };

        var http = _httpClientFactory.CreateClient();
        using var response = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Google token exchange failed ({Status}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Google token exchange failed with status {(int)response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: ct)
               ?? throw new InvalidOperationException("Google token response could not be parsed.");
    }

    private Guid ValidateState(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("OAuth state is required.", nameof(state));

        string unprotected;
        try
        {
            unprotected = _stateProtector.Unprotect(state);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("OAuth state is invalid or tampered.", ex);
        }

        var parts = unprotected.Split('|');
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var userId) || !long.TryParse(parts[1], out var ticks))
            throw new InvalidOperationException("OAuth state is malformed.");

        var issuedUtc = new DateTime(ticks, DateTimeKind.Utc);
        if (DateTime.UtcNow - issuedUtc > StateLifetime)
            throw new InvalidOperationException("OAuth state has expired; restart the connect flow.");

        return userId;
    }

    private string[] GetScopes()
    {
        var configured = _config.GetSection("Google:Scopes").Get<string[]>();
        return configured is { Length: > 0 } ? configured : DefaultScopes;
    }

    private string RequireConfig(string key) =>
        _config[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing configuration '{key}'. Set Google OAuth credentials in .env / appsettings.");

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
        [JsonPropertyName("scope")] public string? Scope { get; init; }
        [JsonPropertyName("token_type")] public string? TokenType { get; init; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    }
}
