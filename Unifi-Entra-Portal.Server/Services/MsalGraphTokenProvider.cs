using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Server.Services;

/// <summary>
/// Acquires an app-only Microsoft Graph token via MSAL's client credentials
/// flow. Registered as a singleton per MSAL's own guidance — the underlying
/// <see cref="IConfidentialClientApplication"/> has its own token cache and
/// is meant to be long-lived, not rebuilt per call.
/// </summary>
public class MsalGraphTokenProvider : IGraphTokenProvider
{
    private static readonly string[] GraphDefaultScope = ["https://graph.microsoft.com/.default"];

    private readonly IConfidentialClientApplication _confidentialClientApplication;

    public MsalGraphTokenProvider(IOptions<AzureAdCredentialsSettings> settings)
    {
        var credentials = settings.Value;
        _confidentialClientApplication = ConfidentialClientApplicationBuilder
            .Create(credentials.ClientId)
            .WithClientSecret(credentials.ClientSecret)
            .WithAuthority($"{credentials.Instance}{credentials.TenantId}")
            .Build();
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var result = await _confidentialClientApplication
            .AcquireTokenForClient(GraphDefaultScope)
            .ExecuteAsync(cancellationToken);

        return result.AccessToken;
    }
}
