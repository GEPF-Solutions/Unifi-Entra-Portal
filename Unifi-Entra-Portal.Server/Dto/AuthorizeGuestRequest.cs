using System.ComponentModel.DataAnnotations;

namespace Unifi_Entra_Portal.Server.Dto;

/// <summary>
/// Request to authorize the current browser's client device on the UniFi
/// network, sent by the frontend once Entra ID sign-in completes.
/// </summary>
public class AuthorizeGuestRequest
{
    /// <summary>
    /// MAC address of the client device to authorize, as reported in the
    /// UniFi captive portal redirect (colon-separated hex, e.g. "aa:bb:cc:dd:ee:ff").
    /// </summary>
    [Required]
    [RegularExpression(@"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$", ErrorMessage = "mac must be a colon-separated MAC address.")]
    public string Mac { get; set; } = string.Empty;
}
