using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Services;

namespace Unifi_Entra_Portal.Tests;

public class GatingServiceTests
{
    private static GatingService CreateService(params string[] allowedGroupIds)
    {
        var settings = Options.Create(new GatingSettings { AllowedGroupIds = allowedGroupIds });
        return new GatingService(settings, NullLogger<GatingService>.Instance);
    }

    private static ClaimsPrincipal UserWithGroups(params string[] groupIds)
    {
        var identity = new ClaimsIdentity(groupIds.Select(id => new Claim("groups", id)), authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void IsUserAllowed_WhenNoGroupsConfigured_AllowsAnyUser()
    {
        var service = CreateService();

        Assert.True(service.IsUserAllowed(UserWithGroups()));
    }

    [Fact]
    public void IsUserAllowed_WhenUserHasMatchingGroup_ReturnsTrue()
    {
        var service = CreateService("group-1", "group-2");

        Assert.True(service.IsUserAllowed(UserWithGroups("group-2")));
    }

    [Fact]
    public void IsUserAllowed_WhenUserHasNoMatchingGroup_ReturnsFalse()
    {
        var service = CreateService("group-1");

        Assert.False(service.IsUserAllowed(UserWithGroups("group-99")));
    }

    [Fact]
    public void IsUserAllowed_WhenGroupsConfiguredButUserHasNoGroupsClaim_FailsClosed()
    {
        var service = CreateService("group-1");

        Assert.False(service.IsUserAllowed(UserWithGroups()));
    }

    [Fact]
    public void IsUserAllowed_WhenGroupsOverageClaimPresent_FailsClosedAndLogsAWarning()
    {
        var logger = new Mock<ILogger<GatingService>>();
        var settings = Options.Create(new GatingSettings { AllowedGroupIds = ["group-1"] });
        var service = new GatingService(settings, logger.Object);

        // Entra's groups-overage shape: no "groups" claim, an opaque
        // "_claim_names" claim pointing at Graph instead.
        var identity = new ClaimsIdentity([new Claim("_claim_names", "{\"groups\":\"src1\"}")], authenticationType: "Test");
        var user = new ClaimsPrincipal(identity);

        Assert.False(service.IsUserAllowed(user));
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
