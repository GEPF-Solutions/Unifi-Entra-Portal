using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Unifi_Entra_Portal.Tests;

/// <summary>
/// Exercises the real ASP.NET Core authentication/authorization pipeline
/// (not just the controller method in isolation) to catch scheme-resolution
/// bugs a direct method call can't see — e.g. a bare [Authorize]/Forbid()
/// resolving to the app's default challenge scheme (OpenIdConnect, which
/// issues a real redirect toward Microsoft's login page) instead of the
/// Cookie scheme that's actually configured to return plain JSON-friendly
/// 401/403 responses for API routes.
/// </summary>
public class PortalEndpointAuthTests : IClassFixture<WebApplicationFactory<Unifi_Entra_Portal.Server.Program>>
{
    private readonly WebApplicationFactory<Unifi_Entra_Portal.Server.Program> _factory;

    public PortalEndpointAuthTests(WebApplicationFactory<Unifi_Entra_Portal.Server.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            // Real AzureAd:ClientId/TenantId/ClientSecret only ever come
            // from the gitignored, developer-local appsettings.Development.json
            // — which structurally never exists in CI. Microsoft.Identity.Web
            // validates those options eagerly on the first request and
            // throws if they're missing, turning this test's expected 401
            // into an unrelated 500 in any environment without that file.
            // This test never performs a real OIDC handshake — it only
            // exercises the pre-authentication short-circuit — so
            // syntactically valid placeholders are enough.
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
                    ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000000",
                    ["AzureAd:ClientSecret"] = "test-secret-not-a-real-credential",

                    // Program.cs falls back to a fixed data/portal.db file
                    // under the content root when ConnectionStrings:Portal
                    // isn't set. Every WebApplicationFactory-based test
                    // class shares that same content root, so without this
                    // override this factory's Database.Migrate() call races
                    // the other test classes' factories against the same
                    // on-disk SQLite file — intermittently failing in CI
                    // with SQLite locking errors. Each factory gets its own
                    // throwaway file instead.
                    ["ConnectionStrings:Portal"] = $"Data Source={Path.Combine(Path.GetTempPath(), $"portal-test-{Guid.NewGuid():N}.db")}",
                });
            });
        });
    }

    [Fact]
    public async Task Authorize_WhenNotSignedIn_Returns401InsteadOfRedirectingToMicrosoftLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/api/portal/authorize?mac=AA:BB:CC:DD:EE:FF", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
