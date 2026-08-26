using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Unifi_Entra_Portal.Server.Controllers;

/// <summary>
/// Thin controller exposing the Entra ID sign-in/sign-out endpoints the
/// React frontend redirects the browser to. The OpenID Connect handshake
/// itself is handled by the authentication middleware configured in
/// Program.cs; this controller only triggers challenges/sign-outs and
/// reports the current session's authentication state.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Starts the Entra ID sign-in flow by challenging the OpenID Connect
    /// scheme, redirecting the browser to Microsoft's login page. Always
    /// requests the account picker (<c>prompt=select_account</c>) rather
    /// than allowing a silent seamless-SSO sign-in, since this portal is
    /// typically used from a shared/kiosk-style device where a stale
    /// cached Entra session belonging to a different person must not be
    /// authorized without the guest explicitly confirming who they are.
    /// </summary>
    /// <param name="returnUrl">
    /// Relative path to return the browser to after a successful sign-in
    /// (e.g. the original captive portal URL with the client's MAC/AP/site
    /// query params attached).
    /// </param>
    [HttpGet("login")]
    public IActionResult Login(string returnUrl = "/")
    {
        var redirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        properties.Items[OpenIdConnectParameterNames.Prompt] = "select_account";

        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Returns whether the current browser session is signed in, and if so,
    /// the signed-in user's basic profile claims, for the SPA to render.
    /// Deliberately not [Authorize]-protected so it can be polled without
    /// forcing a sign-in redirect.
    /// </summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        if (User.Identity is not { IsAuthenticated: true })
        {
            return Ok(new { isAuthenticated = false });
        }

        return Ok(new
        {
            isAuthenticated = true,
            name = User.GetDisplayName(),
            email = User.FindFirst("preferred_username")?.Value,
        });
    }

    /// <summary>
    /// Signs the current user out of both the local cookie session and the
    /// Entra ID session, then redirects the browser back to the portal.
    /// </summary>
    [HttpGet("logout")]
    public IActionResult Logout(string returnUrl = "/")
    {
        var redirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        return SignOut(
            new AuthenticationProperties { RedirectUri = redirectUri },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
