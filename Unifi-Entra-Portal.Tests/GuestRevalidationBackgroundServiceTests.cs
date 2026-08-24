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
}
