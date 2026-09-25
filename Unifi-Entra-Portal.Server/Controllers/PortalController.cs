using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Unifi_Entra_Portal.Server.Dto;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Repository.Abstractions;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Server.Controllers;

/// <summary>
/// Handles the captive portal's device-authorization step: once a guest has
/// signed in via Entra ID and passes the configured gating check, this
/// authorizes their device's MAC address on the UniFi network. Also serves
/// the anonymous, read-only branding/config endpoint the frontend uses to
/// render the landing screen.
/// </summary>
[ApiController]
[Route("api/portal")]
public class PortalController : ControllerBase
{
    private readonly IUniFiClientService _uniFiClient;
    private readonly IGatingService _gatingService;
    private readonly IAuthorizedGuestRepository _authorizedGuestRepository;
    private readonly PortalBrandingSettings _brandingSettings;
    private readonly UniFiSettings _uniFiSettings;
    private readonly ILogger<PortalController> _logger;

    public PortalController(
        IUniFiClientService uniFiClient,
        IGatingService gatingService,
        IAuthorizedGuestRepository authorizedGuestRepository,
        IOptions<PortalBrandingSettings> brandingSettings,
        IOptions<UniFiSettings> uniFiSettings,
        ILogger<PortalController> logger)
    {
        _uniFiClient = uniFiClient;
        _gatingService = gatingService;
        _authorizedGuestRepository = authorizedGuestRepository;
        _brandingSettings = brandingSettings.Value;
        _uniFiSettings = uniFiSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns the branding and copy for the captive portal landing screen.
    /// Anonymous and read-only — lets the frontend render operator-specific
    /// branding without baking it into the bundle, so the same container
    /// image can be reused across deployments.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new PortalConfigDto
        {
            OrgName = _brandingSettings.OrgName,
            Ssid = _brandingSettings.Ssid,
            AccentColor = _brandingSettings.AccentColor,
            LogoUrl = _brandingSettings.LogoPath,
            LogoPlate = _brandingSettings.LogoPlate,
            FaviconUrl = _brandingSettings.FaviconPath,
            HeroImageUrl = _brandingSettings.HeroImagePath,
            Headline = _brandingSettings.Headline,
            Intro = _brandingSettings.Intro,
            MemberTitle = _brandingSettings.MemberTitle,
            MemberSubtitle = _brandingSettings.MemberSubtitle,
            GuestTitle = _brandingSettings.GuestTitle,
            GuestSubtitle = _brandingSettings.GuestSubtitle,
            Tenant = _brandingSettings.Tenant,
            GuestNetworkLabel = _brandingSettings.GuestNetworkLabel,
            MemberNetworkLabel = _brandingSettings.MemberNetworkLabel,
            GuestSessionHours = _uniFiSettings.GuestAuthorizeDurationMinutes / 60.0,
        });
    }

    /// <summary>
    /// Authorizes the signed-in user's device to pass through the captive
    /// portal. Called by the frontend once Entra ID sign-in completes and
    /// the client MAC address from the original UniFi redirect is known.
    /// Requires an authenticated session; the caller is also checked
    /// against the configured gating policy before UniFi is contacted.
    /// </summary>
    /// <remarks>
    /// Pinned to the Cookie scheme explicitly (rather than relying on the
    /// app's default challenge scheme, which is OpenIdConnect — see
    /// Program.cs). Without this, an unauthenticated or gating-denied call
    /// here would trigger a real OIDC redirect toward Microsoft's login
    /// page instead of the plain 401/403 JSON response this API endpoint
    /// needs; the Cookie scheme is the one whose OnRedirectToLogin /
    /// OnRedirectToAccessDenied events are configured for that in Program.cs.
    /// </remarks>
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [HttpPost("authorize")]
    public async Task<IActionResult> Authorize([FromQuery] AuthorizeGuestRequest request, CancellationToken cancellationToken)
    {
        if (!_gatingService.IsUserAllowed(User))
        {
            _logger.LogWarning("Gating policy denied portal access for {User}", User.Identity?.Name);
            return Forbid(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        // UniFi's own convention (and the equipment we've observed) uses
        // lowercase, colon-separated MACs — normalizing here keeps this
        // value consistent everywhere it's used downstream (the UniFi
        // filter lookup and the DB's unique key), regardless of the casing
        // the redirect's "id" query param happened to arrive in.
        var mac = request.Mac.ToLowerInvariant();

        var userObjectId = User.GetObjectId();
        if (string.IsNullOrEmpty(userObjectId))
        {
            // Should never happen for a properly configured Entra sign-in,
            // but without a stable user identity we can't record who this
            // device belongs to for later revalidation/offboarding — fail
            // closed rather than authorizing an untrackable device.
            _logger.LogError("Signed-in user for {User} has no oid claim; refusing to authorize a device without a stable identity", User.Identity?.Name);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        try
        {
            await _uniFiClient.AuthorizeGuestAsync(mac, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authorize {Mac} on UniFi for {User}", mac, User.Identity?.Name);
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, error = "unifi_authorize_failed" });
        }

        var userPrincipalName = User.FindFirst("preferred_username")?.Value;
        try
        {
            await _authorizedGuestRepository.UpsertAsync(mac, userObjectId, userPrincipalName, cancellationToken);
        }
        catch (Exception ex)
        {
            // The guest is already authorized on the network at this point
            // — don't fail the request over a tracking-only write, but log
            // loudly, since this guest won't be included in the next
            // revalidation/offboarding pass until this is resolved.
            _logger.LogError(ex, "UniFi authorized {Mac} but persisting the record failed; this guest won't be tracked for revalidation until this is resolved", mac);
        }

        return Ok(new { success = true });
    }
}
