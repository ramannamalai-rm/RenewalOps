using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RenewalOps.Application.Interfaces;
using RenewalOps.Infrastructure.Persistence;
using System.Web;

namespace RenewalOps.IntegrationTests;

public class GoogleOAuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GoogleOAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void BuildConnectUrl_Produces_Google_Consent_Url_With_Offline_Access()
    {
        using var scope = _factory.Services.CreateScope();
        var oauth = scope.ServiceProvider.GetRequiredService<IGoogleOAuthService>();

        var userId = Guid.NewGuid();
        var url = oauth.BuildConnectUrl(userId);

        url.Should().StartWith("https://accounts.google.com/o/oauth2/v2/auth?");

        var query = HttpUtility.ParseQueryString(new Uri(url).Query);
        query["client_id"].Should().Be("test-client-id");
        query["response_type"].Should().Be("code");
        query["access_type"].Should().Be("offline", "a refresh token is only issued for offline access");
        query["prompt"].Should().Be("consent");
        query["scope"].Should().Contain("drive.file").And.Contain("calendar.events");
        query["redirect_uri"].Should().Be("http://localhost/api/google/callback");
        query["state"].Should().NotBeNullOrWhiteSpace("the signed state carries the user id");
        query["state"].Should().NotContain(userId.ToString("N"), "state must be protected, not plaintext");
    }

    [Fact]
    public async Task HandleCallback_Stores_Encrypted_RefreshToken_For_User()
    {
        // Build a valid signed state via the real service (round-trips through Data Protection),
        // then invoke the callback; the faked token endpoint returns a known refresh token.
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var oauth = sp.GetRequiredService<IGoogleOAuthService>();
        var db = sp.GetRequiredService<AppDbContext>();

        var userId = Guid.NewGuid();
        var connectUrl = oauth.BuildConnectUrl(userId);
        var state = HttpUtility.ParseQueryString(new Uri(connectUrl).Query)["state"]!;

        var resolvedUserId = await oauth.HandleCallbackAsync("fake-auth-code", state);
        resolvedUserId.Should().Be(userId);

        var connection = await db.GoogleConnections.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
        connection.Should().NotBeNull();
        connection!.IsRevoked.Should().BeFalse();
        connection.EncryptedRefreshToken.Should().NotBeNullOrWhiteSpace();
        connection.EncryptedRefreshToken.Should().NotBe(FakeGoogleTokenHandler.RefreshToken,
            "the refresh token must be encrypted at rest, not stored in plaintext");
    }
}
