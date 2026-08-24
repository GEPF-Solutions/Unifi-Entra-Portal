using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Unifi_Entra_Portal.Server.Controllers;
using Unifi_Entra_Portal.Server.Dto;
using Unifi_Entra_Portal.Server.Repository.Abstractions;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Tests;

public class PortalControllerTests
{
    private static PortalController CreateController(
        IUniFiClientService uniFiClient,
        IGatingService gatingService,
        IAuthorizedGuestRepository? authorizedGuestRepository = null)
    {
        var controller = new PortalController(
            uniFiClient,
            gatingService,
            authorizedGuestRepository ?? new Mock<IAuthorizedGuestRepository>().Object,
            NullLogger<PortalController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")) },
        };
        return controller;
    }

    [Fact]
    public async Task Authorize_WhenGatingAllows_AuthorizesGuestAndReturnsOk()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        var gatingService = new Mock<IGatingService>();
        gatingService.Setup(g => g.IsUserAllowed(It.IsAny<ClaimsPrincipal>())).Returns(true);
        var authorizedGuestRepository = new Mock<IAuthorizedGuestRepository>();

        var controller = CreateController(uniFiClient.Object, gatingService.Object, authorizedGuestRepository.Object);

        var result = await controller.Authorize(new AuthorizeGuestRequest { Mac = "AA:BB:CC:DD:EE:FF" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        uniFiClient.Verify(c => c.AuthorizeGuestAsync("AA:BB:CC:DD:EE:FF", It.IsAny<CancellationToken>()), Times.Once);
        authorizedGuestRepository.Verify(
            r => r.UpsertAsync("AA:BB:CC:DD:EE:FF", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Authorize_WhenGatingDenies_ReturnsForbidAndDoesNotCallUniFi()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        var gatingService = new Mock<IGatingService>();
        gatingService.Setup(g => g.IsUserAllowed(It.IsAny<ClaimsPrincipal>())).Returns(false);
        var authorizedGuestRepository = new Mock<IAuthorizedGuestRepository>();

        var controller = CreateController(uniFiClient.Object, gatingService.Object, authorizedGuestRepository.Object);

        var result = await controller.Authorize(new AuthorizeGuestRequest { Mac = "AA:BB:CC:DD:EE:FF" }, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        uniFiClient.Verify(c => c.AuthorizeGuestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        authorizedGuestRepository.Verify(
            r => r.UpsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
