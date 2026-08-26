namespace Unifi_Entra_Portal.Server.Services.Abstractions;

/// <summary>
/// Determines whether an Entra ID user (identified by object ID) is still
/// eligible to remain authorized on the captive portal — used by the
/// background re-validation job, which has no live sign-in token for a
/// stored guest, only their object ID.
/// </summary>
public interface IGuestEligibilityService
{
    /// <summary>
    /// True if the user's account is still enabled and, when
    /// <see cref="Infrastructure.GatingSettings.AllowedGroupIds"/> is
    /// non-empty, they still belong to at least one of those groups.
    /// </summary>
    Task<bool> IsEligibleAsync(string userObjectId, CancellationToken cancellationToken);
}
