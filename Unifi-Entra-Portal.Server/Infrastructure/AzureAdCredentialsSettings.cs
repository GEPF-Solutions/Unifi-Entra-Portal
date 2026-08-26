namespace Unifi_Entra_Portal.Server.Infrastructure;

/// <summary>
/// Entra ID app registration credentials used to acquire an app-only
/// (client credentials) Microsoft Graph token for the guest re-validation
/// background job. Bound from the same "AzureAd" section used for user
/// sign-in — this is a separate, minimal view of it, kept independent of
/// Microsoft.Identity.Web's own options type since app-only Graph access is
/// a different scenario from the web app's OIDC sign-in flow. The app
/// registration must additionally be granted the relevant Graph application
/// permissions (User.Read.All, and GroupMember.Read.All if group gating is
/// used) with admin consent.
/// </summary>
public class AzureAdCredentialsSettings
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
