namespace Unifi_Entra_Portal.Server.Infrastructure;

/// <summary>
/// Controls the background job that periodically re-checks each authorized
/// guest's Entra ID eligibility (account enabled + group membership) and
/// revokes UniFi authorization for anyone no longer eligible. Bound from
/// the "Revalidation" configuration section.
/// </summary>
public class RevalidationSettings
{
    /// <summary>Hours between re-validation passes.</summary>
    public double IntervalHours { get; set; } = 24;
}
