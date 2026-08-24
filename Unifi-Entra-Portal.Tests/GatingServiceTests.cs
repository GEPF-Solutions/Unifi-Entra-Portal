using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
}
