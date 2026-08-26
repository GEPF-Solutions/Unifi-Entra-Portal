using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Web;
using Moq;
using Unifi_Entra_Portal.Server.Controllers;
using Unifi_Entra_Portal.Server.Dto;
using Unifi_Entra_Portal.Server.Repository.Abstractions;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Tests;

public class PortalControllerTests
{
    private const string DefaultUserObjectId = "11111111-1111-1111-1111-111111111111";

    private static PortalController CreateController(
        IUniFiClientService uniFiClient,
        IGatingService gatingService,
        IAuthorizedGuestRepository? authorizedGuestRepository = null,
        string? userObjectId = DefaultUserObjectId)
    {
        var controller = new PortalController(
            uniFiClient,
            gatingService,
            authorizedGuestRepository ?? new Mock<IAuthorizedGuestRepository>().Object,
            NullLogger<PortalController>.Instance);

        var claims = userObjectId is null
            ? []
            : new[] { new Claim(ClaimConstants.ObjectId, userObjectId) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")) },
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
        // Normalized to lowercase — see PortalController.Authorize.
        uniFiClient.Verify(c => c.AuthorizeGuestAsync("aa:bb:cc:dd:ee:ff", It.IsAny<CancellationToken>()), Times.Once);
        authorizedGuestRepository.Verify(
            r => r.UpsertAsync("aa:bb:cc:dd:ee:ff", DefaultUserObjectId, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
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

    [Fact]
    public async Task Authorize_WhenUserHasNoObjectIdClaim_ReturnsServerErrorAndDoesNotCallUniFi()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        var gatingService = new Mock<IGatingService>();
        gatingService.Setup(g => g.IsUserAllowed(It.IsAny<ClaimsPrincipal>())).Returns(true);
        var authorizedGuestRepository = new Mock<IAuthorizedGuestRepository>();

        var controller = CreateController(uniFiClient.Object, gatingService.Object, authorizedGuestRepository.Object, userObjectId: null);

        var result = await controller.Authorize(new AuthorizeGuestRequest { Mac = "AA:BB:CC:DD:EE:FF" }, CancellationToken.None);

        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        uniFiClient.Verify(c => c.AuthorizeGuestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        authorizedGuestRepository.Verify(
            r => r.UpsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Authorize_WhenUniFiCallFails_ReturnsBadGatewayAndDoesNotPersist()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        uniFiClient
            .Setup(c => c.AuthorizeGuestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no connected client with that MAC"));
        var gatingService = new Mock<IGatingService>();
        gatingService.Setup(g => g.IsUserAllowed(It.IsAny<ClaimsPrincipal>())).Returns(true);
        var authorizedGuestRepository = new Mock<IAuthorizedGuestRepository>();

        var controller = CreateController(uniFiClient.Object, gatingService.Object, authorizedGuestRepository.Object);

        var result = await controller.Authorize(new AuthorizeGuestRequest { Mac = "AA:BB:CC:DD:EE:FF" }, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, statusResult.StatusCode);
        authorizedGuestRepository.Verify(
            r => r.UpsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Authorize_WhenPersistingFails_StillReturnsOkSinceUniFiAlreadyAuthorizedTheDevice()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        var gatingService = new Mock<IGatingService>();
        gatingService.Setup(g => g.IsUserAllowed(It.IsAny<ClaimsPrincipal>())).Returns(true);
        var authorizedGuestRepository = new Mock<IAuthorizedGuestRepository>();
        authorizedGuestRepository
            .Setup(r => r.UpsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unavailable"));

        var controller = CreateController(uniFiClient.Object, gatingService.Object, authorizedGuestRepository.Object);

        var result = await controller.Authorize(new AuthorizeGuestRequest { Mac = "AA:BB:CC:DD:EE:FF" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        uniFiClient.Verify(c => c.AuthorizeGuestAsync("aa:bb:cc:dd:ee:ff", It.IsAny<CancellationToken>()), Times.Once);
    }
}
