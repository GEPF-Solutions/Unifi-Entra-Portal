using Unifi_Entra_Portal.Server.Models;

namespace Unifi_Entra_Portal.Server.Repository.Abstractions;

/// <summary>
/// Persists which Entra ID user authorized which device MAC address, so a
/// background job can later re-check eligibility per user.
/// </summary>
public interface IAuthorizedGuestRepository
{
    /// <summary>
    /// Records or refreshes the authorization for a device, keyed by MAC
    /// address (one row per device — re-authorizing an existing MAC updates
    /// it rather than adding a duplicate).
    /// </summary>
    Task UpsertAsync(string macAddress, string userObjectId, string? userPrincipalName, CancellationToken cancellationToken);

    /// <summary>Returns every currently tracked authorized device.</summary>
    Task<IReadOnlyList<AuthorizedGuest>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records that a device's owner was just re-checked and is still
    /// eligible, without disturbing <see cref="AuthorizedGuest.AuthorizedAtUtc"/>
    /// (which reflects when the device was actually (re-)authorized on UniFi,
    /// not when it was last validated).
    /// </summary>
    Task MarkValidatedAsync(string macAddress, CancellationToken cancellationToken);

    /// <summary>Removes a device's tracked authorization, e.g. after it's been revoked on UniFi.</summary>
    Task DeleteAsync(string macAddress, CancellationToken cancellationToken);
}
