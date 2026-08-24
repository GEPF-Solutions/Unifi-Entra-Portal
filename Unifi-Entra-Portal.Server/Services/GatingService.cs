using System.Security.Claims;
using Microsoft.Extensions.Options;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Server.Services;

/// <summary>
/// Checks a signed-in user's Entra ID group membership against
/// <see cref="GatingSettings.AllowedGroupIds"/>. Fails closed: if
/// restriction groups are configured and the user's token carries no
/// matching group, access is denied.
/// </summary>
/// <remarks>
/// Reads group object IDs from the "groups" claim, which requires the
/// "groups" optional claim to be configured on the Entra App Registration.
/// For users in more groups than Entra will emit inline (the "groups
/// overage" case, surfaced as a "hasgroups"/"_claim_names" claim instead),
/// this class will not see their full group membership; that case would
/// need a Microsoft Graph lookup instead, not implemented here.
/// </remarks>
public class GatingService : IGatingService
{
    private const string GroupsClaimType = "groups";

    private readonly GatingSettings _settings;
    private readonly ILogger<GatingService> _logger;

    public GatingService(IOptions<GatingSettings> settings, ILogger<GatingService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsUserAllowed(ClaimsPrincipal user)
    {
        if (_settings.AllowedGroupIds.Length == 0)
        {
            return true;
        }

        var userGroupIds = user.FindAll(GroupsClaimType).Select(c => c.Value);
        var isAllowed = userGroupIds.Any(groupId => _settings.AllowedGroupIds.Contains(groupId, StringComparer.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            _logger.LogInformation(
                "Denied portal access for {User}: no group membership matched the configured AllowedGroupIds",
                user.Identity?.Name);
        }

        return isAllowed;
    }
}
