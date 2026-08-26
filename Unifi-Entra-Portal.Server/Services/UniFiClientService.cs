using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Server.Services;

/// <summary>
/// Talks to the official UniFi Network Integration API (API-key
/// authenticated) to authorize or revoke guest devices. Since the
/// Integration API identifies clients by an internal UUID rather than MAC
/// address, every call first looks up that UUID by filtering the site's
/// connected-clients list on MAC.
/// </summary>
/// <remarks>
/// Registered as a singleton (see Program.cs) so the single <see cref="HttpClient"/>
/// built in the constructor is reused — and its underlying connections
/// pooled — for the app's lifetime, instead of paying a fresh TCP/TLS
/// handshake to the UniFi controller on every guest sign-in.
/// </remarks>
public class UniFiClientService : IUniFiClientService, IDisposable
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly UniFiSettings _settings;
    private readonly ILogger<UniFiClientService> _logger;
    private readonly HttpClient _client;

    /// <param name="handlerOverride">
    /// Substitutes the HTTP transport for tests. Left null in production.
    /// </param>
    public UniFiClientService(
        IOptions<UniFiSettings> settings,
        ILogger<UniFiClientService> logger,
        HttpMessageHandler? handlerOverride = null)
    {
        _settings = settings.Value;
        _logger = logger;

        _client = new HttpClient(handlerOverride ?? CreateDefaultHandler(), disposeHandler: handlerOverride is null)
        {
            BaseAddress = new Uri(BuildBaseUrl()),
        };
        _client.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
    }

    /// <inheritdoc />
    public Task AuthorizeGuestAsync(string macAddress, CancellationToken cancellationToken) =>
        SendGuestActionAsync(macAddress, "AUTHORIZE_GUEST_ACCESS", _settings.AuthorizeDurationMinutes, cancellationToken);

    /// <inheritdoc />
    public Task UnauthorizeGuestAsync(string macAddress, CancellationToken cancellationToken) =>
        SendGuestActionAsync(macAddress, "UNAUTHORIZE_GUEST_ACCESS", minutes: null, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SendGuestActionAsync(string macAddress, string action, int? minutes, CancellationToken cancellationToken)
    {
        var clientId = await FindClientIdByMacAsync(_client, macAddress, cancellationToken)
            ?? throw new InvalidOperationException($"No connected UniFi client found with MAC address {macAddress}.");

        object body = minutes.HasValue
            ? new { action, timeLimitMinutes = minutes.Value }
            : new { action };

        using var response = await _client.PostAsJsonAsync($"v1/sites/{_settings.SiteId}/clients/{clientId}/actions", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("UniFi {Action} succeeded for {Mac}", action, macAddress);
    }

    /// <summary>
    /// Looks up a connected client's internal UUID by MAC address via the
    /// site's clients list, filtered server-side on the "macAddress" field.
    /// Returns null if no connected client currently has that MAC.
    /// </summary>
    private async Task<string?> FindClientIdByMacAsync(HttpClient client, string macAddress, CancellationToken cancellationToken)
    {
        var filter = Uri.EscapeDataString($"macAddress.eq('{macAddress}')");
        using var response = await client.GetAsync($"v1/sites/{_settings.SiteId}/clients?filter={filter}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ClientListResponse>(ResponseJsonOptions, cancellationToken);
        return payload?.Data?.FirstOrDefault()?.Id;
    }

    private string BuildBaseUrl() => _settings.UseCloudConnector
        ? $"https://api.ui.com/v1/connector/consoles/{_settings.ConsoleId}/proxy/network/integration/"
        : $"{_settings.ControllerUrl.TrimEnd('/')}/proxy/network/integration/";

    private HttpClientHandler CreateDefaultHandler()
    {
        var handler = new HttpClientHandler();
        if (_settings.AllowInsecureCertificates)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }

    private record ClientListResponse(ClientSummary[]? Data);

    private record ClientSummary(string Id);
}
