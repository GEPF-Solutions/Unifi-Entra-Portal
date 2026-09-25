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
    /// Authorizes an anonymous guest's device for an explicit duration —
    /// but only after verifying UniFi currently reports that MAC's IP as
    /// belonging to <see cref="Infrastructure.UniFiSettings.GuestNetworkCidr"/>.
    /// Used exclusively by the anonymous AGB-accept path, which authorizes
    /// for <see cref="Infrastructure.UniFiSettings.GuestAuthorizeDurationMinutes"/>.
    /// This check exists because UniFi's captive-portal authorization is
    /// per-MAC, not per-network — without it, a device actually connected
    /// to a different (e.g. internal) SSID could self-authorize via this
    /// anonymous path instead of signing in via Entra.
    /// </summary>
    /// <param name="macAddress">MAC address of the client device, as reported by the UniFi redirect.</param>
    /// <param name="durationMinutes">Explicit authorization duration in minutes.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="GuestNotOnGuestNetworkException">
    /// The MAC's current IP is outside the configured guest network, or
    /// couldn't be determined — refused rather than trusted.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Infrastructure.UniFiSettings.GuestNetworkCidr"/> is not
    /// configured, or no connected client currently has that MAC.
    /// </exception>
    Task AuthorizeGuestIfOnGuestNetworkAsync(string macAddress, int durationMinutes, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes a previously authorized guest device, e.g. because the user
    /// is no longer eligible on re-validation.
    /// </summary>
    /// <param name="macAddress">MAC address of the client device to revoke.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task UnauthorizeGuestAsync(string macAddress, CancellationToken cancellationToken);
}
