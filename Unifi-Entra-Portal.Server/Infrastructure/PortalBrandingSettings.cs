namespace Unifi_Entra_Portal.Server.Infrastructure;

/// <summary>
/// Branding and copy shown on the captive portal's landing/choice screen.
/// Runtime-configurable (rather than baked into the frontend bundle) so
/// other organizations can re-skin this project for their own deployment
/// without rebuilding the container — see
/// <see cref="Controllers.PortalController.GetConfig"/>. Bound from the
/// "PortalBranding" configuration section. Ships with neutral, non-org-
/// specific defaults.
/// </summary>
public class PortalBrandingSettings
{
    /// <summary>Organization name shown in copy and as the logo's alt text.</summary>
    public string OrgName { get; set; } = "Your Organization";

    /// <summary>SSID shown next to the Wi-Fi icon on the landing screen.</summary>
    public string Ssid { get; set; } = string.Empty;

    /// <summary>Accent color (hex) used throughout the portal UI. Defaults to the design system's neutral accent.</summary>
    public string AccentColor { get; set; } = "#e0695f";

    /// <summary>
    /// URL path to the organization's logo (transparent PNG/SVG recommended),
    /// e.g. "/branding/logo.png" — served from the folder configured by
    /// <see cref="AssetsPath"/>. Null shows no logo.
    /// </summary>
    public string? LogoPath { get; set; }

    /// <summary>Whether to render a light plate behind the logo — needed for logos with dark lettering.</summary>
    public bool LogoPlate { get; set; }

    /// <summary>
    /// URL path to the hero photo shown on the landing screen, e.g.
    /// "/branding/hero.jpg". Null shows a striped placeholder instead.
    /// </summary>
    public string? HeroImagePath { get; set; }

    /// <summary>Headline shown on the Choose step.</summary>
    public string Headline { get; set; } = "Welcome to the Wi-Fi";

    /// <summary>Intro line shown below the headline on the Choose step.</summary>
    public string Intro { get; set; } = "Choose how you'd like to connect.";

    /// <summary>Title of the member (Entra sign-in) choice button.</summary>
    public string MemberTitle { get; set; } = "Member";

    /// <summary>Subtitle of the member (Entra sign-in) choice button.</summary>
    public string MemberSubtitle { get; set; } = "Sign in with your organization account";

    /// <summary>Title of the anonymous guest choice button.</summary>
    public string GuestTitle { get; set; } = "Guest";

    /// <summary>Subtitle of the anonymous guest choice button.</summary>
    public string GuestSubtitle { get; set; } = "Internet access on the guest network";

    /// <summary>Footer line shown at the bottom of the Choose step.</summary>
    public string Footer { get; set; } = "Member and guest networks are kept separate.";

    /// <summary>
    /// Friendly tenant domain shown in the redirect interstitial's URL chip
    /// (e.g. "contoso.onmicrosoft.com"). Purely for display — kept separate
    /// from <see cref="AzureAdCredentialsSettings.TenantId"/>, which is a
    /// GUID used for the actual auth flow. Empty hides the chip.
    /// </summary>
    public string Tenant { get; set; } = string.Empty;

    /// <summary>
    /// Label for the network row on the guest Connected screen, e.g. "Guest
    /// network" or "Guest network · VLAN 30" if an operator wants to include
    /// the VLAN number in the display copy.
    /// </summary>
    public string GuestNetworkLabel { get; set; } = "Guest network";

    /// <summary>
    /// Physical folder branding assets (<see cref="LogoPath"/>,
    /// <see cref="HeroImagePath"/>) are served from, mounted at the
    /// "/branding" request path — see Program.cs. Relative paths are
    /// resolved against the app's content root.
    /// </summary>
    public string AssetsPath { get; set; } = "wwwroot/branding";
}
