using System.ComponentModel.DataAnnotations;

namespace Unifi_Entra_Portal.Server.Dto;

/// <summary>
/// Request to authorize an anonymous guest's device after they accept the
/// AGB/terms checkbox, sent by the frontend's guest path (no Entra sign-in
/// involved). Distinct from <see cref="AuthorizeGuestRequest"/>, which is
/// the member path's authenticated equivalent.
/// </summary>
public class GuestAgbAuthorizeRequest
{
    /// <summary>
    /// MAC address of the client device to authorize, as reported in the
    /// UniFi captive portal redirect (colon-separated hex, e.g. "aa:bb:cc:dd:ee:ff").
    /// </summary>
    [Required]
    [RegularExpression(MacAddressValidation.Pattern, ErrorMessage = "mac must be a colon-separated MAC address.")]
    public string Mac { get; set; } = string.Empty;

    /// <summary>
    /// Whether the guest checked the AGB/terms acceptance checkbox.
    /// Enforced server-side (not just via a disabled Continue button)
    /// since this endpoint is anonymous and callable directly.
    /// </summary>
    public bool AgbAccepted { get; set; }
}
