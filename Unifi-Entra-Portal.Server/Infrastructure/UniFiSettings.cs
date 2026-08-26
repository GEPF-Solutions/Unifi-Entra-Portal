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
    /// How long (in minutes) an authorized guest device stays authorized
    /// before it would need re-authorization. In practice this is kept long,
    /// with a background job handling early offboarding via
    /// UNAUTHORIZE_GUEST_ACCESS instead of relying on a short expiry.
    /// </summary>
    public int AuthorizeDurationMinutes { get; set; } = 43200;

    /// <summary>
    /// Whether to accept the console's TLS certificate without validation.
    /// Only relevant when <see cref="UseCloudConnector"/> is false and the
    /// console has a self-signed certificate on the local network.
    /// </summary>
    public bool AllowInsecureCertificates { get; set; }
}
