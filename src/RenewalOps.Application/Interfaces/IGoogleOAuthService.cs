using RenewalOps.Application.DTOs.Google;

namespace RenewalOps.Application.Interfaces;

/// <summary>
/// Google OAuth 2.0 authorization-code flow: build the consent URL, handle the callback,
/// and report connection status. The refresh token obtained on callback is stored encrypted.
/// </summary>
public interface IGoogleOAuthService
{
    /// <summary>
    /// Builds the Google consent URL for the given user. The user id is carried in a signed
    /// <c>state</c> parameter so the (anonymous) callback can be correlated back to the user.
    /// </summary>
    string BuildConnectUrl(Guid userId);

    /// <summary>
    /// Handles the OAuth callback: validates <paramref name="state"/>, exchanges the
    /// authorization <paramref name="code"/> for tokens, and stores the encrypted refresh
    /// token for the user. Returns the resolved user id.
    /// </summary>
    Task<Guid> HandleCallbackAsync(string code, string state, CancellationToken ct = default);

    Task<GoogleConnectionStatusResponse> GetStatusAsync(Guid userId, CancellationToken ct = default);
}
