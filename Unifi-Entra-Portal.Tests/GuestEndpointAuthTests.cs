using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Tests;

/// <summary>
/// Exercises the real ASP.NET Core pipeline (not just the controller
/// method in isolation) to confirm the guest path is truly reachable
/// without any authentication — no [Authorize] short-circuit, no OIDC
/// redirect toward Microsoft's login page. Mirrors
/// <see cref="PortalEndpointAuthTests"/>'s WebApplicationFactory setup.
/// </summary>
public class GuestEndpointAuthTests : IClassFixture<WebApplicationFactory<Unifi_Entra_Portal.Server.Program>>
{
    /// <summary>
    /// Stands in for the real <see cref="UniFiClientService"/>, whose
    /// production registration would otherwise make a live outbound call
    /// to Ubiquiti's real api.ui.com from CI — this test only cares about
    /// the auth pipeline, not UniFi connectivity, so it always fails the
    /// same way PortalController's tests fake outbound failures.
    /// </summary>
    private sealed class ThrowingUniFiClientService : IUniFiClientService
    {
        public Task AuthorizeGuestAsync(string macAddress, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no real UniFi controller in tests");

        public Task AuthorizeGuestAsync(string macAddress, int durationMinutes, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no real UniFi controller in tests");

        public Task UnauthorizeGuestAsync(string macAddress, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no real UniFi controller in tests");
    }

    private readonly WebApplicationFactory<Unifi_Entra_Portal.Server.Program> _factory;

    public GuestEndpointAuthTests(WebApplicationFactory<Unifi_Entra_Portal.Server.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            // See PortalEndpointAuthTests for why these placeholders are
            // needed just to let the app boot in CI.
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
                    ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000000",
                    ["AzureAd:ClientSecret"] = "test-secret-not-a-real-credential",

                    // See PortalEndpointAuthTests: without this override,
                    // this factory's Database.Migrate() races every other
                    // WebApplicationFactory-based test class against the
                    // same fallback data/portal.db file.
                    ["ConnectionStrings:Portal"] = $"Data Source={Path.Combine(Path.GetTempPath(), $"portal-test-{Guid.NewGuid():N}.db")}",
                });
            });

            // Unlike PortalController, GuestController has no [Authorize]
            // short-circuit, so a request here really reaches
            // IUniFiClientService — replace the real, network-calling
            // singleton so this test stays hermetic.
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUniFiClientService>();
                services.AddSingleton<IUniFiClientService, ThrowingUniFiClientService>();
            });
        });
    }

    [Fact]
    public async Task Authorize_WhenNotSignedIn_DoesNotRedirectOrReturn401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/api/guest/authorize?mac=AA:BB:CC:DD:EE:FF&agbAccepted=true", content: null);

        // IUniFiClientService is faked to always throw (see
        // ThrowingUniFiClientService), so the expected terminal status is
        // 502 — the point of this test is what it's NOT: no 401 (would
        // mean [Authorize] leaked onto this route) and no redirect (would
        // mean the OIDC default challenge scheme engaged).
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False((int)response.StatusCode is >= 300 and < 400, $"Expected no redirect, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetAgbText_WhenNotSignedIn_ReturnsOkInsteadOfRedirectOrUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/guest/agb-text");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
