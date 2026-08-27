namespace Unifi_Entra_Portal.Server.Services.Abstractions;

/// <summary>
/// Authorizes and revokes guest devices on the UniFi Network Controller for
/// the captive portal SSID, identified by MAC address.
/// </summary>
public interface IUniFiClientService
{
    /// <summary>
    /// Authorizes a guest device to pass through the captive portal for the
    /// configured <see cref="Infrastructure.UniFiSettings.AuthorizeDurationMinutes"/>.
    /// </summary>
    /// <param name="macAddress">MAC address of the client device, as reported by the UniFi redirect.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task AuthorizeGuestAsync(string macAddress, CancellationToken cancellationToken);

    /// <summary>
    /// Authorizes a guest device for an explicit duration rather than the
    /// configured <see cref="Infrastructure.UniFiSettings.AuthorizeDurationMinutes"/> —
    /// used by the anonymous AGB-accept path, which authorizes for
    /// <see cref="Infrastructure.UniFiSettings.GuestAuthorizeDurationMinutes"/>
    /// instead.
    /// </summary>
    /// <param name="macAddress">MAC address of the client device, as reported by the UniFi redirect.</param>
    /// <param name="durationMinutes">Explicit authorization duration in minutes.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task AuthorizeGuestAsync(string macAddress, int durationMinutes, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes a previously authorized guest device, e.g. because the user
    /// is no longer eligible on re-validation.
    /// </summary>
    /// <param name="macAddress">MAC address of the client device to revoke.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task UnauthorizeGuestAsync(string macAddress, CancellationToken cancellationToken);
}
