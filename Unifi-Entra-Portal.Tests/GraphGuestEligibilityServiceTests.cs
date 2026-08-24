using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Services;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Tests;

public class GraphGuestEligibilityServiceTests
{
    private static Mock<IGraphTokenProvider> CreateTokenProvider()
    {
        var tokenProvider = new Mock<IGraphTokenProvider>();
        tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("fake-token");
        return tokenProvider;
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task IsEligibleAsync_WhenAccountDisabled_ReturnsFalseWithoutCheckingGroups()
    {
        var handler = new FakeHttpMessageHandler(JsonResponse("""{"accountEnabled":false}"""));
        var service = new GraphGuestEligibilityService(
            CreateTokenProvider().Object,
            Options.Create(new GatingSettings { AllowedGroupIds = ["group-1"] }),
            NullLogger<GraphGuestEligibilityService>.Instance,
            handler);

        var result = await service.IsEligibleAsync("user-1", CancellationToken.None);

        Assert.False(result);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task IsEligibleAsync_WhenUserNotFound_ReturnsFalse()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = new GraphGuestEligibilityService(
            CreateTokenProvider().Object,
            Options.Create(new GatingSettings()),
            NullLogger<GraphGuestEligibilityService>.Instance,
            handler);

        var result = await service.IsEligibleAsync("user-1", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsEligibleAsync_WhenEnabledAndNoGroupRestriction_ReturnsTrueWithoutCheckingGroups()
    {
        var handler = new FakeHttpMessageHandler(JsonResponse("""{"accountEnabled":true}"""));
        var service = new GraphGuestEligibilityService(
            CreateTokenProvider().Object,
            Options.Create(new GatingSettings()),
            NullLogger<GraphGuestEligibilityService>.Instance,
            handler);

        var result = await service.IsEligibleAsync("user-1", CancellationToken.None);

        Assert.True(result);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task IsEligibleAsync_WhenEnabledAndInAllowedGroup_ReturnsTrue()
    {
        var handler = new FakeHttpMessageHandler(
            JsonResponse("""{"accountEnabled":true}"""),
            JsonResponse("""{"value":["group-1"]}"""));
        var service = new GraphGuestEligibilityService(
            CreateTokenProvider().Object,
            Options.Create(new GatingSettings { AllowedGroupIds = ["group-1"] }),
            NullLogger<GraphGuestEligibilityService>.Instance,
            handler);

        var result = await service.IsEligibleAsync("user-1", CancellationToken.None);

        Assert.True(result);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task IsEligibleAsync_WhenEnabledButNotInAllowedGroup_ReturnsFalse()
    {
        var handler = new FakeHttpMessageHandler(
            JsonResponse("""{"accountEnabled":true}"""),
            JsonResponse("""{"value":[]}"""));
        var service = new GraphGuestEligibilityService(
            CreateTokenProvider().Object,
            Options.Create(new GatingSettings { AllowedGroupIds = ["group-1"] }),
            NullLogger<GraphGuestEligibilityService>.Instance,
            handler);

        var result = await service.IsEligibleAsync("user-1", CancellationToken.None);

        Assert.False(result);
    }
}
