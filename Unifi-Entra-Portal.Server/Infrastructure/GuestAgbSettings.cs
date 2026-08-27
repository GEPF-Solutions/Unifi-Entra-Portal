namespace Unifi_Entra_Portal.Server.Infrastructure;

/// <summary>
/// Content shown on the anonymous guest AGB/terms-acceptance screen.
/// Runtime-configurable (rather than baked into the frontend bundle) so an
/// operator can update legal text without rebuilding the container. Bound
/// from the "GuestAgb" configuration section.
/// </summary>
public class GuestAgbSettings
{
    /// <summary>
    /// AGB/terms text shown above the acceptance checkbox on the guest
    /// path. Ships with an obvious placeholder — replace via config for a
    /// real deployment; never hardcode real legal copy in code.
    /// </summary>
    public string AgbText { get; set; } = "TODO: replace with your organization's actual terms and conditions text.";
}
