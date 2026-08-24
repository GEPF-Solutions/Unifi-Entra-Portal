namespace Unifi_Entra_Portal.Server.Infrastructure;

/// <summary>
/// Controls which signed-in Entra ID users are allowed through the captive
/// portal. Kept configurable (rather than hardcoded to one organization's
/// group) so other deployments of this project can scope access to their
/// own tenant's groups. Bound from the "Gating" configuration section.
/// </summary>
public class GatingSettings
{
    /// <summary>
    /// Entra ID group object IDs allowed to pass the portal. An empty array
    /// means no group restriction is applied — any authenticated user in the
    /// configured tenant is allowed through.
    /// </summary>
    public string[] AllowedGroupIds { get; set; } = [];
}
