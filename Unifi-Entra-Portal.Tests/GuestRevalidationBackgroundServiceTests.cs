using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Models;
using Unifi_Entra_Portal.Server.Repository.Abstractions;
using Unifi_Entra_Portal.Server.Services;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Tests;

public class GuestRevalidationBackgroundServiceTests
{
    private static (
        GuestRevalidationBackgroundService Service,
        Mock<IAuthorizedGuestRepository> Repository,
        Mock<IGuestEligibilityService> Eligibility,
        Mock<IUniFiClientService> UniFiClient) CreateService()
    {
        var repository = new Mock<IAuthorizedGuestRepository>();
        var eligibility = new Mock<IGuestEligibilityService>();
        var uniFiClient = new Mock<IUniFiClientService>();

        var services = new ServiceCollection();
        services.AddSingleton(repository.Object);
        services.AddSingleton(eligibility.Object);
        services.AddSingleton(uniFiClient.Object);
        var provider = services.BuildServiceProvider();

        var service = new GuestRevalidationBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RevalidationSettings { IntervalHours = 24 }),
            NullLogger<GuestRevalidationBackgroundService>.Instance);

        return (service, repository, eligibility, uniFiClient);
    }

    [Fact]
    public async Task RevalidateAllAsync_WhenGuestStillEligible_MarksValidatedAndDoesNotUnauthorize()
    {
        var (service, repository, eligibility, uniFiClient) = CreateService();
        var guest = new AuthorizedGuest { MacAddress = "AA:BB:CC:DD:EE:FF", UserObjectId = "user-1" };
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([guest]);
        eligibility.Setup(e => e.IsEligibleAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await service.RevalidateAllAsync(CancellationToken.None);

        repository.Verify(r => r.MarkValidatedAsync("AA:BB:CC:DD:EE:FF", It.IsAny<CancellationToken>()), Times.Once);
        uniFiClient.Verify(c => c.UnauthorizeGuestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevalidateAllAsync_WhenGuestNoLongerEligible_UnauthorizesAndDeletesRecord()
    {
        var (service, repository, eligibility, uniFiClient) = CreateService();
        var guest = new AuthorizedGuest { MacAddress = "AA:BB:CC:DD:EE:FF", UserObjectId = "user-1" };
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([guest]);
        eligibility.Setup(e => e.IsEligibleAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await service.RevalidateAllAsync(CancellationToken.None);

        uniFiClient.Verify(c => c.UnauthorizeGuestAsync("AA:BB:CC:DD:EE:FF", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.DeleteAsync("AA:BB:CC:DD:EE:FF", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.MarkValidatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevalidateAllAsync_WhenOneGuestCheckThrows_StillProcessesRemainingGuests()
    {
        var (service, repository, eligibility, uniFiClient) = CreateService();
        var guestA = new AuthorizedGuest { MacAddress = "AA:AA:AA:AA:AA:AA", UserObjectId = "user-a" };
        var guestB = new AuthorizedGuest { MacAddress = "BB:BB:BB:BB:BB:BB", UserObjectId = "user-b" };
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([guestA, guestB]);
        eligibility.Setup(e => e.IsEligibleAsync("user-a", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Graph unavailable"));
        eligibility.Setup(e => e.IsEligibleAsync("user-b", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await service.RevalidateAllAsync(CancellationToken.None);

        repository.Verify(r => r.MarkValidatedAsync("BB:BB:BB:BB:BB:BB", It.IsAny<CancellationToken>()), Times.Once);
        uniFiClient.Verify(c => c.UnauthorizeGuestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevalidateAllAsync_WhenSameUserHasMultipleDevices_ChecksEligibilityOnlyOnce()
    {
        var (service, repository, eligibility, uniFiClient) = CreateService();
        var phone = new AuthorizedGuest { MacAddress = "AA:AA:AA:AA:AA:AA", UserObjectId = "user-1" };
        var laptop = new AuthorizedGuest { MacAddress = "BB:BB:BB:BB:BB:BB", UserObjectId = "user-1" };
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([phone, laptop]);
        eligibility.Setup(e => e.IsEligibleAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await service.RevalidateAllAsync(CancellationToken.None);

        eligibility.Verify(e => e.IsEligibleAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.MarkValidatedAsync("AA:AA:AA:AA:AA:AA", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.MarkValidatedAsync("BB:BB:BB:BB:BB:BB", It.IsAny<CancellationToken>()), Times.Once);
        uniFiClient.Verify(c => c.UnauthorizeGuestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevalidateAllAsync_WhenSameUserIneligible_RevokesAllOfTheirDevices()
    {
        var (service, repository, eligibility, uniFiClient) = CreateService();
        var phone = new AuthorizedGuest { MacAddress = "AA:AA:AA:AA:AA:AA", UserObjectId = "user-1" };
        var laptop = new AuthorizedGuest { MacAddress = "BB:BB:BB:BB:BB:BB", UserObjectId = "user-1" };
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([phone, laptop]);
        eligibility.Setup(e => e.IsEligibleAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await service.RevalidateAllAsync(CancellationToken.None);

        eligibility.Verify(e => e.IsEligibleAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
        uniFiClient.Verify(c => c.UnauthorizeGuestAsync("AA:AA:AA:AA:AA:AA", It.IsAny<CancellationToken>()), Times.Once);
        uniFiClient.Verify(c => c.UnauthorizeGuestAsync("BB:BB:BB:BB:BB:BB", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenARevalidationPassThrows_DoesNotFaultTheHostedService()
    {
        var (service, repository, _, _) = CreateService();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db unavailable"));

        // BackgroundService's default failure behavior is to let an
        // unhandled ExecuteAsync exception stop the whole host — this
        // guards against a single bad revalidation pass (DB down, bad
        // Graph credentials, ...) taking guest sign-in down with it.
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        Assert.False(service.ExecuteTask is { IsFaulted: true });
        repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIntervalHoursIsNotPositive_DoesNotThrowOnStartup()
    {
        var repository = new Mock<IAuthorizedGuestRepository>();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var services = new ServiceCollection();
        services.AddSingleton(repository.Object);
        services.AddSingleton(new Mock<IGuestEligibilityService>().Object);
        services.AddSingleton(new Mock<IUniFiClientService>().Object);
        var provider = services.BuildServiceProvider();

        var service = new GuestRevalidationBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RevalidationSettings { IntervalHours = 0 }),
            NullLogger<GuestRevalidationBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        Assert.False(service.ExecuteTask is { IsFaulted: true });
    }
}
