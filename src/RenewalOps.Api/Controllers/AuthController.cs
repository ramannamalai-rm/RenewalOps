using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenewalOps.Application.DTOs.Auth;
using RenewalOps.Application.Interfaces;

namespace RenewalOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    /// <summary>Register a new user account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        try
        {
            var result = await _authService.RegisterAsync(request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Authenticate and receive a JWT token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        try
        {
            var result = await _authService.LoginAsync(request, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }
    }

    /// <summary>Refresh an expired access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request.RefreshToken, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }
    }
}
