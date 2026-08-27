using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Services;

namespace Unifi_Entra_Portal.Tests;

public class UniFiClientServiceTests
{
    private static UniFiSettings DefaultSettings() => new()
    {
        ApiKey = "test-api-key",
        UseCloudConnector = true,
        ConsoleId = "console-1",
        SiteId = "11111111-1111-1111-1111-111111111111",
        AuthorizeDurationMinutes = 1000000,
    };

    private static HttpResponseMessage ClientListResponse(string clientId) => new(HttpStatusCode.OK)
    {
        Content = System.Net.Http.Json.JsonContent.Create(new { data = new[] { new { id = clientId } } }),
    };

    private static HttpResponseMessage EmptyClientListResponse() => new(HttpStatusCode.OK)
    {
        Content = System.Net.Http.Json.JsonContent.Create(new { data = Array.Empty<object>() }),
    };

    [Fact]
    public async Task AuthorizeGuestAsync_LooksUpClientByMacThenSendsAuthorizeAction()
    {
        var handler = new FakeHttpMessageHandler(ClientListResponse("client-uuid-1"), new HttpResponseMessage(HttpStatusCode.OK));
        var service = new UniFiClientService(Options.Create(DefaultSettings()), NullLogger<UniFiClientService>.Instance, handler);

        await service.AuthorizeGuestAsync("AA:BB:CC:DD:EE:FF", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);

        var lookup = handler.Requests[0];
        Assert.Equal(HttpMethod.Get, lookup.Method);
        Assert.Contains("/v1/sites/11111111-1111-1111-1111-111111111111/clients?filter=", lookup.Path);
        Assert.Contains("macAddress.eq", Uri.UnescapeDataString(lookup.Path ?? string.Empty));

        var action = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, action.Method);
        Assert.EndsWith("/v1/sites/11111111-1111-1111-1111-111111111111/clients/client-uuid-1/actions", action.Path);
        Assert.Contains("\"action\":\"AUTHORIZE_GUEST_ACCESS\"", action.Body);
        Assert.Contains("\"timeLimitMinutes\":1000000", action.Body);
    }

    [Fact]
    public async Task AuthorizeGuestAsync_WithExplicitDuration_SendsThatDurationInsteadOfConfiguredDefault()
    {
        var handler = new FakeHttpMessageHandler(ClientListResponse("client-uuid-1"), new HttpResponseMessage(HttpStatusCode.OK));
        var settings = DefaultSettings();
        settings.AuthorizeDurationMinutes = 43200;
        var service = new UniFiClientService(Options.Create(settings), NullLogger<UniFiClientService>.Instance, handler);

        await service.AuthorizeGuestAsync("AA:BB:CC:DD:EE:FF", 1440, CancellationToken.None);

        var action = handler.Requests[1];
        Assert.Contains("\"action\":\"AUTHORIZE_GUEST_ACCESS\"", action.Body);
        Assert.Contains("\"timeLimitMinutes\":1440", action.Body);
    }

    [Fact]
    public async Task UnauthorizeGuestAsync_SendsActionWithoutTimeLimit()
    {
        var handler = new FakeHttpMessageHandler(ClientListResponse("client-uuid-1"), new HttpResponseMessage(HttpStatusCode.OK));
        var service = new UniFiClientService(Options.Create(DefaultSettings()), NullLogger<UniFiClientService>.Instance, handler);

        await service.UnauthorizeGuestAsync("AA:BB:CC:DD:EE:FF", CancellationToken.None);

        var action = handler.Requests[1];
        Assert.Contains("\"action\":\"UNAUTHORIZE_GUEST_ACCESS\"", action.Body);
        Assert.DoesNotContain("timeLimitMinutes", action.Body);
    }

    [Fact]
    public async Task AuthorizeGuestAsync_SetsApiKeyHeaderOnBothRequests()
    {
        var handler = new FakeHttpMessageHandler(ClientListResponse("client-uuid-1"), new HttpResponseMessage(HttpStatusCode.OK));
        var service = new UniFiClientService(Options.Create(DefaultSettings()), NullLogger<UniFiClientService>.Instance, handler);

        await service.AuthorizeGuestAsync("AA:BB:CC:DD:EE:FF", CancellationToken.None);

        Assert.All(handler.Requests, r => Assert.Equal("test-api-key", r.ApiKeyHeader));
    }

    [Fact]
    public async Task AuthorizeGuestAsync_UsesCloudConnectorBaseUrl_WhenUseCloudConnectorIsTrue()
    {
        var handler = new FakeHttpMessageHandler(ClientListResponse("client-uuid-1"), new HttpResponseMessage(HttpStatusCode.OK));
        var service = new UniFiClientService(Options.Create(DefaultSettings()), NullLogger<UniFiClientService>.Instance, handler);

        await service.AuthorizeGuestAsync("AA:BB:CC:DD:EE:FF", CancellationToken.None);

        Assert.All(handler.Requests, r => Assert.Equal("api.ui.com", r.Host));
    }

    [Fact]
    public async Task AuthorizeGuestAsync_WhenNoConnectedClientHasThatMac_ThrowsAndDoesNotSendAction()
    {
        var handler = new FakeHttpMessageHandler(EmptyClientListResponse());
        var service = new UniFiClientService(Options.Create(DefaultSettings()), NullLogger<UniFiClientService>.Instance, handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuthorizeGuestAsync("AA:BB:CC:DD:EE:FF", CancellationToken.None));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AuthorizeGuestAsync_WhenLookupFails_ThrowsAndDoesNotSendAction()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var service = new UniFiClientService(Options.Create(DefaultSettings()), NullLogger<UniFiClientService>.Instance, handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.AuthorizeGuestAsync("AA:BB:CC:DD:EE:FF", CancellationToken.None));

        Assert.Single(handler.Requests);
    }
}
