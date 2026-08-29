using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Unifi_Entra_Portal.Tests;

/// <summary>
/// Confirms the "guest" rate-limiting policy registered in Program.cs is
/// actually wired up on <see cref="Unifi_Entra_Portal.Server.Controllers.GuestController"/>
/// — i.e. that [EnableRateLimiting]/UseRateLimiter weren't silently dropped
/// or misconfigured, not just that the .NET middleware itself works.
/// </summary>
/// <remarks>
/// Deliberately its own test class with its own <see cref="WebApplicationFactory{TEntryPoint}"/>
/// (own DI container, own rate-limiter state), rather than sharing
/// <see cref="GuestEndpointAuthTests"/>'s factory — consuming most of the
/// per-IP quota here would otherwise make those other tests flaky
/// depending on xUnit's execution order.
/// </remarks>
public class GuestRateLimitingTests : IClassFixture<WebApplicationFactory<Unifi_Entra_Portal.Server.Program>>
{
    private readonly WebApplicationFactory<Unifi_Entra_Portal.Server.Program> _factory;

    public GuestRateLimitingTests(WebApplicationFactory<Unifi_Entra_Portal.Server.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            // See PortalEndpointAuthTests for why these AzureAd placeholders
            // are needed just to let the app boot in CI — without them,
            // Microsoft.Identity.Web throws validating options on the first
            // request, turning every response (including the 21st, which
            // this test expects to be 429) into an unrelated 500.
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
        });
    }

    [Fact]
    public async Task GetAgbText_WhenCalledPastTheLimit_ReturnsTooManyRequests()
    {
        var client = _factory.CreateClient();

        // The "guest" policy allows 20 requests per minute per IP (see
        // Program.cs) — the 21st from the same client should be rejected.
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 21; i++)
        {
            lastResponse = await client.GetAsync("/api/guest/agb-text");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}
