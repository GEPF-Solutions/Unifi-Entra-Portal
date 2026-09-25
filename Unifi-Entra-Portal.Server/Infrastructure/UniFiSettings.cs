namespace Unifi_Entra_Portal.Server.Infrastructure;

/// <summary>
/// Configuration for connecting to the official UniFi Network Integration
/// API (REST, API-key authenticated), used to authorize (and later
/// re-validate / unauthorize) guest devices on the captive portal SSID.
/// Bound from the "UniFi" configuration section.
/// </summary>
public class UniFiSettings
{
    /// <summary>
    /// API key created under Site Manager (unifi.ui.com → Settings → API
    /// Keys) or locally in the Network application (Settings → Control
    /// Plane → Integrations). Sent as the "X-API-Key" header.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether to reach the console through Ubiquiti's cloud connector
    /// (api.ui.com) rather than directly over the local network. Required
    /// when this app has no direct network path to the controller (e.g.
    /// hosted off-site, which is the common case). Set false only if this
    /// app has direct/VPN access to the console and the API key was created
    /// locally on the console rather than via Site Manager.
    /// </summary>
    public bool UseCloudConnector { get; set; } = true;

    /// <summary>
    /// Required when <see cref="UseCloudConnector"/> is true. The console's
    /// ID, as returned by "id" in GET https://api.ui.com/v1/hosts.
    /// </summary>
    public string ConsoleId { get; set; } = string.Empty;

    /// <summary>
    /// Required when <see cref="UseCloudConnector"/> is false. Base URL of
    /// the console on the local network (e.g. "https://192.168.1.1").
    /// </summary>
    public string ControllerUrl { get; set; } = string.Empty;

    /// <summary>
    /// UUID of the site the captive portal SSID belongs to (not the
    /// human-readable slug) — see the "id" field of the entry whose
    /// "internalReference" matches your site in
    /// GET .../network/integration/v1/sites.
    /// </summary>
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// How long (in minutes) an authorized member device stays authorized.
    /// Set to UniFi's documented maximum (1,000,000 ≈ 1.9 years) rather
    /// than a short/moderate window — this is deliberately not the real
    /// offboarding mechanism. <see cref="Services.GuestRevalidationBackgroundService"/>
    /// is: it periodically re-checks every authorized device against Entra
    /// and revokes access (UNAUTHORIZE_GUEST_ACCESS) the moment someone is
    /// no longer eligible, so this duration only matters as a last-resort
    /// ceiling if that job were ever silently broken for good — a known,
    /// deliberately accepted tradeoff of relying on the revalidation job
    /// rather than a short expiry (see Context.md, "Persistence / no
    /// repeat auth"). Not set to true "unlimited": UniFi documents
    /// timeLimitMinutes as optional but never documents what omitting it
    /// actually does, so this uses the documented, verified max instead.
    /// </summary>
    public int AuthorizeDurationMinutes { get; set; } = 1000000;

    /// <summary>
    /// How long (in minutes) a guest who accepted the AGB/terms checkbox but
    /// did not sign in via Entra stays authorized. Kept short relative to
    /// <see cref="AuthorizeDurationMinutes"/> since there is no tracked
    /// identity to revalidate or offboard early — UniFi's own expiry is the
    /// only mechanism that removes this guest's access, matching the
    /// previous UniFi-native Hotspot Portal's AGB/expiry behavior this
    /// replaces.
    /// </summary>
    public int GuestAuthorizeDurationMinutes { get; set; } = 1440;

    /// <summary>
    /// CIDR range (e.g. "10.10.60.0/24") of the subnet the guest network
    /// hands out addresses on. UniFi's captive-portal authorization is
    /// per-MAC, not per-network — it just unblocks whatever network a
    /// device is already connected to, which is fixed at Wi-Fi association
    /// time (see Context.md, "Dual guest/member portal"). Without this
    /// check, a device actually connected to the internal SSID could hit
    /// the anonymous guest-authorize path and get itself authorized on the
    /// internal network without ever going through Entra sign-in. UniFi's
    /// client API does not report which SSID/VLAN a client is on, but it
    /// does report the client's live-assigned IP — which is a reliable
    /// proxy once (as here) the guest network has its own distinct subnet.
    /// Required for the guest path: if unset, guest-authorize requests are
    /// refused rather than silently trusting an unverified MAC.
    /// </summary>
    public string GuestNetworkCidr { get; set; } = string.Empty;

    /// <summary>
    /// Whether to accept the console's TLS certificate without validation.
    /// Only relevant when <see cref="UseCloudConnector"/> is false and the
    /// console has a self-signed certificate on the local network.
    /// </summary>
    public bool AllowInsecureCertificates { get; set; }
}
