namespace Unifi_Entra_Portal.Server.Services.Abstractions;

/// <summary>
/// Acquires an app-only (client credentials) access token for calling
/// Microsoft Graph, using the same Entra ID app registration configured for
/// user sign-in.
/// </summary>
public interface IGraphTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
