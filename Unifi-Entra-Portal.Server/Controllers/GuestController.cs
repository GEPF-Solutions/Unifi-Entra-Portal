using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Unifi_Entra_Portal.Server.Dto;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Server.Controllers;

/// <summary>
/// Handles the captive portal's anonymous "I'm a guest" path: a visitor who
/// accepts the AGB/terms checkbox is authorized directly on UniFi for a
/// short duration, without signing in via Entra ID. Deliberately carries no
/// [Authorize] attribute — this endpoint must be reachable without a
/// session. Unlike <see cref="PortalController.Authorize"/>, no gating
/// check is performed and no <c>AuthorizedGuest</c> row is written: there
/// is no tracked identity to revalidate later, so access simply expires on
/// UniFi's side. See Context.md, "Planned: dual guest/member portal".
/// </summary>
/// <remarks>
/// Rate-limited (see Program.cs's "guest" policy) since being anonymous
/// means there's no signed-in identity naturally capping request volume,
/// unlike <see cref="PortalController"/>.
/// </remarks>
[ApiController]
[Route("api/guest")]
[EnableRateLimiting("guest")]
public class GuestController : ControllerBase
{
    private readonly IUniFiClientService _uniFiClient;
    private readonly UniFiSettings _uniFiSettings;
    private readonly GuestAgbSettings _agbSettings;
    private readonly ILogger<GuestController> _logger;

    public GuestController(
        IUniFiClientService uniFiClient,
        IOptions<UniFiSettings> uniFiSettings,
        IOptions<GuestAgbSettings> agbSettings,
        ILogger<GuestController> logger)
    {
        _uniFiClient = uniFiClient;
        _uniFiSettings = uniFiSettings.Value;
        _agbSettings = agbSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns the AGB/terms text to display on the guest acceptance
    /// screen. Anonymous and read-only — lets the frontend show
    /// operator-configured legal copy without baking it into the bundle.
    /// </summary>
    [HttpGet("agb-text")]
    public IActionResult GetAgbText()
    {
        return Ok(new { text = _agbSettings.AgbText });
    }

    /// <summary>
    /// Authorizes an anonymous guest's device on UniFi for
    /// <see cref="UniFiSettings.GuestAuthorizeDurationMinutes"/>, after
    /// they've accepted the AGB/terms checkbox.
    /// </summary>
    [HttpPost("authorize")]
    public async Task<IActionResult> Authorize([FromQuery] GuestAgbAuthorizeRequest request, CancellationToken cancellationToken)
    {
        if (!request.AgbAccepted)
        {
            _logger.LogWarning("Guest authorize request for {Mac} rejected: AGB not accepted", request.Mac);
            return BadRequest(new { success = false, error = "agb_not_accepted" });
        }

        // Same normalization rationale as PortalController.Authorize: keep
        // the MAC lowercase/consistent regardless of the redirect's casing.
        var mac = request.Mac.ToLowerInvariant();

        try
        {
            await _uniFiClient.AuthorizeGuestIfOnGuestNetworkAsync(mac, _uniFiSettings.GuestAuthorizeDurationMinutes, cancellationToken);
        }
        catch (GuestNotOnGuestNetworkException ex)
        {
            // Not a UniFi failure — a deliberate rejection. Most likely a
            // device actually connected to the internal SSID trying to
            // skip Entra sign-in via this anonymous path.
            _logger.LogWarning(ex, "Guest authorize request for {Mac} rejected: not on the configured guest network", mac);
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, error = "not_on_guest_network" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authorize guest {Mac} on UniFi", mac);
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, error = "unifi_authorize_failed" });
        }

        // Computed here (rather than left for the frontend to derive from
        // GuestSessionHours) so the Connected screen's "valid until" time
        // reflects when this authorization actually started, not an
        // estimate that could drift if the request took a while to process.
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_uniFiSettings.GuestAuthorizeDurationMinutes);
        return Ok(new { success = true, expiresAtUtc });
    }
}
