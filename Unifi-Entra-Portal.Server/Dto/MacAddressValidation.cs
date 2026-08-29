namespace Unifi_Entra_Portal.Server.Dto;

/// <summary>
/// Shared MAC address validation pattern for request DTOs, so
/// <see cref="AuthorizeGuestRequest"/> and <see cref="GuestAgbAuthorizeRequest"/>
/// can't silently drift apart if the accepted format ever changes.
/// </summary>
internal static class MacAddressValidation
{
    /// <summary>Matches a colon-separated hex MAC address, e.g. "aa:bb:cc:dd:ee:ff".</summary>
    public const string Pattern = @"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$";
}
