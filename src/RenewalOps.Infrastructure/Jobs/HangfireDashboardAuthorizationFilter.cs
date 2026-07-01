using System.Security.Claims;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace RenewalOps.Infrastructure.Jobs;

/// <summary>
/// Authorizes access to the Hangfire dashboard.
/// </summary>
/// <remarks>
/// The dashboard is browser-navigated, but this API is JWT-bearer only — a browser
/// request to /hangfire carries no Authorization header, so a normal [Authorize] check
/// would lock everyone out. As an MVP compromise:
///   - In Development: allow all (the stack runs behind a Docker port map, so a loopback
///     check on the remote IP does not work — requests arrive from the bridge gateway).
///   - Otherwise: require the caller to carry the Admin role claim.
/// A proper dashboard auth story (e.g. cookie exchange or a dedicated admin login) is
/// deferred to Phase 4 hardening.
/// </remarks>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly bool _isDevelopment;

    public HangfireDashboardAuthorizationFilter(bool isDevelopment)
    {
        _isDevelopment = isDevelopment;
    }

    public bool Authorize(DashboardContext context)
    {
        if (_isDevelopment)
            return true;

        var user = context.GetHttpContext().User;
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        return user.IsInRole("Admin")
            || user.HasClaim(ClaimTypes.Role, "Admin")
            || user.HasClaim("role", "Admin");
    }
}
