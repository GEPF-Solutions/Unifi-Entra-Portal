using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Unifi_Entra_Portal.Server.Controllers;
using Unifi_Entra_Portal.Server.Dto;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Tests;

// GuestController's constructor structurally never accepts
// IGatingService/IAuthorizedGuestRepository, so there's nothing to mock or
// Verify(Times.Never) for "doesn't gate / doesn't persist" — the compiler
// is the proof.
public class GuestControllerTests
{
    private const int DefaultGuestAuthorizeDurationMinutes = 1440;
    private const string DefaultAgbText = "test agb text";

    private static GuestController CreateController(IUniFiClientService uniFiClient, string? agbText = DefaultAgbText)
    {
        var uniFiSettings = new UniFiSettings { GuestAuthorizeDurationMinutes = DefaultGuestAuthorizeDurationMinutes };
        var agbSettings = new GuestAgbSettings { AgbText = agbText ?? DefaultAgbText };

        return new GuestController(
            uniFiClient,
            Options.Create(uniFiSettings),
            Options.Create(agbSettings),
            NullLogger<GuestController>.Instance);
    }

    [Fact]
    public async Task Authorize_WhenAgbAccepted_AuthorizesGuestForConfiguredGuestDurationAndReturnsOk()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        var controller = CreateController(uniFiClient.Object);

        var result = await controller.Authorize(new GuestAgbAuthorizeRequest { Mac = "AA:BB:CC:DD:EE:FF", AgbAccepted = true }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        // Normalized to lowercase — see GuestController.Authorize.
        uniFiClient.Verify(
            c => c.AuthorizeGuestAsync("aa:bb:cc:dd:ee:ff", DefaultGuestAuthorizeDurationMinutes, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Authorize_WhenAgbAccepted_ReturnsExpiryMatchingConfiguredGuestDuration()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        var controller = CreateController(uniFiClient.Object);
        var beforeCall = DateTime.UtcNow;

        var result = await controller.Authorize(new GuestAgbAuthorizeRequest { Mac = "AA:BB:CC:DD:EE:FF", AgbAccepted = true }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var expiresAtUtc = (DateTime?)okResult.Value?.GetType().GetProperty("expiresAtUtc")?.GetValue(okResult.Value);
        Assert.NotNull(expiresAtUtc);
        var expectedExpiry = beforeCall.AddMinutes(DefaultGuestAuthorizeDurationMinutes);
        Assert.True(Math.Abs((expiresAtUtc!.Value - expectedExpiry).TotalSeconds) < 5);
    }

    [Fact]
    public async Task Authorize_WhenAgbNotAccepted_ReturnsBadRequestAndDoesNotCallUniFi()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        var controller = CreateController(uniFiClient.Object);

        var result = await controller.Authorize(new GuestAgbAuthorizeRequest { Mac = "AA:BB:CC:DD:EE:FF", AgbAccepted = false }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        uniFiClient.Verify(
            c => c.AuthorizeGuestAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Authorize_WhenUniFiCallFails_ReturnsBadGateway()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        uniFiClient
            .Setup(c => c.AuthorizeGuestAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no connected client with that MAC"));
        var controller = CreateController(uniFiClient.Object);

        var result = await controller.Authorize(new GuestAgbAuthorizeRequest { Mac = "AA:BB:CC:DD:EE:FF", AgbAccepted = true }, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, statusResult.StatusCode);
    }

    [Fact]
    public void GetAgbText_ReturnsConfiguredAgbText()
    {
        var uniFiClient = new Mock<IUniFiClientService>();
        var controller = CreateController(uniFiClient.Object, agbText: "custom text");

        var result = Assert.IsType<OkObjectResult>(controller.GetAgbText());

        Assert.Equal("custom text", result.Value?.GetType().GetProperty("text")?.GetValue(result.Value));
    }
}
