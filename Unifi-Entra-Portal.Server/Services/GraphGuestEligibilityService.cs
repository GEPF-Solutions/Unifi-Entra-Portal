using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Server.Services;

/// <summary>
/// Checks a user's continued eligibility directly against Microsoft Graph
/// (account enabled + group membership), rather than relying on ID token
/// claims like <see cref="GatingService"/> does — the background
/// re-validation job has no live token for a stored guest, only their
/// object ID, and Graph lookups also sidestep the ID token "groups claim
/// overage" limitation entirely.
/// </summary>
/// <remarks>
/// Registered as a singleton (see Program.cs) so the single <see cref="HttpClient"/>
/// built in the constructor is reused for the app's lifetime instead of a
/// fresh one per revalidation call. The bearer token is still fetched
/// per-call via <see cref="_tokenProvider"/>, which caches/refreshes it
/// independently.
/// </remarks>
public class GraphGuestEligibilityService : IGuestEligibilityService, IDisposable
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0/";
    private static readonly JsonSerializerOptions GraphJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IGraphTokenProvider _tokenProvider;
    private readonly GatingSettings _gatingSettings;
    private readonly ILogger<GraphGuestEligibilityService> _logger;
    private readonly HttpClient _client;

    /// <param name="handlerOverride">Substitutes the HTTP transport for tests. Left null in production.</param>
    public GraphGuestEligibilityService(
        IGraphTokenProvider tokenProvider,
        IOptions<GatingSettings> gatingSettings,
        ILogger<GraphGuestEligibilityService> logger,
        HttpMessageHandler? handlerOverride = null)
    {
        _tokenProvider = tokenProvider;
        _gatingSettings = gatingSettings.Value;
        _logger = logger;

        _client = new HttpClient(handlerOverride ?? new HttpClientHandler(), disposeHandler: handlerOverride is null)
        {
            BaseAddress = new Uri(GraphBaseUrl),
        };
    }

    /// <inheritdoc />
    public async Task<bool> IsEligibleAsync(string userObjectId, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!await IsAccountEnabledAsync(_client, userObjectId, cancellationToken))
        {
            return false;
        }

        if (_gatingSettings.AllowedGroupIds.Length == 0)
        {
            return true;
        }

        return await IsInAllowedGroupAsync(_client, userObjectId, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<bool> IsAccountEnabledAsync(HttpClient client, string userObjectId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"users/{userObjectId}?$select=accountEnabled", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Graph reports user {UserObjectId} no longer exists", userObjectId);
            return false;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AccountEnabledResponse>(GraphJsonOptions, cancellationToken);
        return payload?.AccountEnabled ?? false;
    }

    private async Task<bool> IsInAllowedGroupAsync(HttpClient client, string userObjectId, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            $"users/{userObjectId}/checkMemberGroups",
            new { groupIds = _gatingSettings.AllowedGroupIds },
            GraphJsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CheckMemberGroupsResponse>(GraphJsonOptions, cancellationToken);
        return payload?.Value is { Length: > 0 };
    }

    private record AccountEnabledResponse(bool AccountEnabled);

    private record CheckMemberGroupsResponse(string[]? Value);
}
