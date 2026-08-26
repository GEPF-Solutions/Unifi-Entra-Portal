namespace Unifi_Entra_Portal.Server.Models;

/// <summary>
/// Records which Entra ID user authorized a given device (by MAC address) on
/// the UniFi network. UniFi itself only tracks MAC + duration, not identity,
/// so this mapping is what lets a background job re-check a device owner's
/// group membership later and revoke access if they're no longer eligible.
/// </summary>
public class AuthorizedGuest
{
    public int Id { get; set; }

    /// <summary>MAC address of the authorized client device (colon-separated hex).</summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>Entra ID object ID (oid claim) of the user who authorized this device.</summary>
    public string UserObjectId { get; set; } = string.Empty;

    /// <summary>User principal name / email of the authorizing user, for display and troubleshooting.</summary>
    public string? UserPrincipalName { get; set; }

    /// <summary>When this device was most recently authorized (or re-authorized).</summary>
    public DateTimeOffset AuthorizedAtUtc { get; set; }

    /// <summary>When a background job last confirmed this user is still eligible. Null until the first re-validation run.</summary>
    public DateTimeOffset? LastValidatedAtUtc { get; set; }
}
