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
///   - In Development: allow local (loopback) requests so the dashboard is usable while developing.
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
        var httpContext = context.GetHttpContext();

        if (_isDevelopment && IsLocalRequest(httpContext))
            return true;

        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        return user.IsInRole("Admin")
            || user.HasClaim(ClaimTypes.Role, "Admin")
            || user.HasClaim("role", "Admin");
    }

    private static bool IsLocalRequest(HttpContext context)
    {
        var connection = context.Connection;
        var remote = connection.RemoteIpAddress;

        if (remote is null)
            return true; // in-process / test host

        if (connection.LocalIpAddress is not null)
            return remote.Equals(connection.LocalIpAddress);

        return System.Net.IPAddress.IsLoopback(remote);
    }
}
