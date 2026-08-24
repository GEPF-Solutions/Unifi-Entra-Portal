using System.Security.Claims;

namespace Unifi_Entra_Portal.Server.Services.Abstractions;

/// <summary>
/// Determines which signed-in Entra ID users are allowed through the
/// captive portal, per <see cref="Infrastructure.GatingSettings"/>.
/// </summary>
public interface IGatingService
{
    /// <summary>
    /// Returns whether the given signed-in user satisfies the configured
    /// gating policy.
    /// </summary>
    bool IsUserAllowed(ClaimsPrincipal user);
}
