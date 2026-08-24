using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using Unifi_Entra_Portal.Server.Dto;
using Unifi_Entra_Portal.Server.Repository.Abstractions;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Server.Controllers;

/// <summary>
/// Handles the captive portal's device-authorization step: once a guest has
/// signed in via Entra ID and passes the configured gating check, this
/// authorizes their device's MAC address on the UniFi network.
/// </summary>
[ApiController]
[Route("api/portal")]
public class PortalController : ControllerBase
{
    private readonly IUniFiClientService _uniFiClient;
    private readonly IGatingService _gatingService;
    private readonly IAuthorizedGuestRepository _authorizedGuestRepository;
    private readonly ILogger<PortalController> _logger;

    public PortalController(
        IUniFiClientService uniFiClient,
        IGatingService gatingService,
        IAuthorizedGuestRepository authorizedGuestRepository,
        ILogger<PortalController> logger)
    {
        _uniFiClient = uniFiClient;
        _gatingService = gatingService;
        _authorizedGuestRepository = authorizedGuestRepository;
        _logger = logger;
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

        await _uniFiClient.AuthorizeGuestAsync(request.Mac, cancellationToken);

        var userObjectId = User.GetObjectId() ?? string.Empty;
        var userPrincipalName = User.FindFirst("preferred_username")?.Value;
        await _authorizedGuestRepository.UpsertAsync(request.Mac, userObjectId, userPrincipalName, cancellationToken);

        return Ok(new { success = true });
    }
}
