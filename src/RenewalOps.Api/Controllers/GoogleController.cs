using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenewalOps.Application.DTOs.Google;
using RenewalOps.Application.Interfaces;

namespace RenewalOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GoogleController : ControllerBase
{
    private readonly IGoogleOAuthService _googleOAuth;

    public GoogleController(IGoogleOAuthService googleOAuth)
    {
        _googleOAuth = googleOAuth;
    }

    /// <summary>Returns the Google consent URL to start the OAuth connect flow.</summary>
    [HttpGet("connect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Connect()
    {
        var url = _googleOAuth.BuildConnectUrl(GetUserId());
        return Ok(new { authorizationUrl = url });
    }

    /// <summary>
    /// OAuth redirect target. Anonymous: the user is identified via the signed state parameter,
    /// since Google redirects the browser here without an Authorization header.
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error))
            return BadRequest(new { error = $"Google authorization was denied or failed: {error}" });

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest(new { error = "Missing 'code' or 'state'." });

        try
        {
            await _googleOAuth.HandleCallbackAsync(code, state, ct);
            return Ok(new { message = "Google account connected. You can close this window." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Reports whether the current user has a (non-revoked) Google connection.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(GoogleConnectionStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        return Ok(await _googleOAuth.GetStatusAsync(GetUserId(), ct));
    }

    private Guid GetUserId() => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("No user ID claim in token"));
}
