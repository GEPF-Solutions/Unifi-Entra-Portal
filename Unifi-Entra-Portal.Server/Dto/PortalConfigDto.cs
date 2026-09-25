namespace Unifi_Entra_Portal.Server.Dto;

/// <summary>
/// Branding and copy for the captive portal landing screen, returned by
/// <see cref="Controllers.PortalController.GetConfig"/>. Assembled from
/// <see cref="Infrastructure.PortalBrandingSettings"/> plus a couple of
/// values derived from other config sections so the frontend never has to
/// duplicate that math (and risk drifting out of sync with it).
/// </summary>
public class PortalConfigDto
{
    /// <summary>Organization name shown in copy and as the logo's alt text.</summary>
    public string OrgName { get; set; } = string.Empty;

    /// <summary>SSID shown next to the Wi-Fi icon on the landing screen.</summary>
    public string Ssid { get; set; } = string.Empty;

    /// <summary>Accent color (hex) used throughout the portal UI.</summary>
    public string AccentColor { get; set; } = string.Empty;

    /// <summary>URL of the organization's logo, or null to show no logo.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Whether to render a light plate behind the logo.</summary>
    public bool LogoPlate { get; set; }

    /// <summary>URL of the browser-tab favicon, or null to show the project's bundled default icon.</summary>
    public string? FaviconUrl { get; set; }

    /// <summary>URL of the hero photo, or null to show a striped placeholder.</summary>
    public string? HeroImageUrl { get; set; }

    /// <summary>Headline shown on the Choose step.</summary>
    public string Headline { get; set; } = string.Empty;

    /// <summary>Intro line shown below the headline on the Choose step.</summary>
    public string Intro { get; set; } = string.Empty;

    /// <summary>Title of the member (Entra sign-in) choice button.</summary>
    public string MemberTitle { get; set; } = string.Empty;

    /// <summary>Subtitle of the member (Entra sign-in) choice button.</summary>
    public string MemberSubtitle { get; set; } = string.Empty;

    /// <summary>Title of the anonymous guest choice button.</summary>
    public string GuestTitle { get; set; } = string.Empty;

    /// <summary>Subtitle of the anonymous guest choice button.</summary>
    public string GuestSubtitle { get; set; } = string.Empty;

    /// <summary>Friendly tenant domain shown in the redirect interstitial's URL chip. Empty hides the chip.</summary>
    public string Tenant { get; set; } = string.Empty;

    /// <summary>Label for the network row on the guest Connected screen.</summary>
    public string GuestNetworkLabel { get; set; } = string.Empty;

    /// <summary>Label for the network row on the member Connected screen.</summary>
    public string MemberNetworkLabel { get; set; } = string.Empty;

    /// <summary>
    /// How long an anonymous guest's authorization lasts, in hours. Derived
    /// from <see cref="Infrastructure.UniFiSettings.GuestAuthorizeDurationMinutes"/>
    /// so the Terms step's "valid for" tag can't drift out of sync with the
    /// actual authorization duration.
    /// </summary>
    public double GuestSessionHours { get; set; }
}
