namespace Unifi_Entra_Portal.Server.Services.Abstractions;

/// <summary>
/// Thrown when an anonymous guest-authorize request targets a MAC address
/// that UniFi does not currently report as connected via the configured
/// guest network (see <see cref="Infrastructure.UniFiSettings.GuestNetworkCidr"/>)
/// — e.g. a device actually connected to the internal SSID trying to skip
/// Entra sign-in via the guest path. Kept distinct from a generic UniFi-call
/// failure so callers can return 403 (a deliberate rejection) instead of
/// 502 (UniFi itself failed/unreachable).
/// </summary>
public class GuestNotOnGuestNetworkException(string macAddress)
    : Exception($"Client {macAddress} is not connected via the configured guest network.")
{
    /// <summary>MAC address of the device that was rejected.</summary>
    public string MacAddress { get; } = macAddress;
}
