using Microsoft.Extensions.Options;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Models;
using Unifi_Entra_Portal.Server.Repository.Abstractions;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Server.Services;

/// <summary>
/// Periodically re-validates every currently authorized guest device against
/// Entra ID (account enabled + group membership), revoking UniFi
/// authorization for anyone no longer eligible. This is the actual
/// offboarding mechanism — UniFi's own authorization only tracks MAC +
/// duration, not identity, so a long expiry alone would leave departed
/// members connected until it lapses.
/// </summary>
public class GuestRevalidationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RevalidationSettings _settings;
    private readonly ILogger<GuestRevalidationBackgroundService> _logger;

    public GuestRevalidationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<RevalidationSettings> settings,
        ILogger<GuestRevalidationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(_settings.IntervalHours));

        do
        {
            await RevalidateAllAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Runs one re-validation pass over every currently tracked authorized
    /// guest device. Public (rather than only reachable via
    /// <see cref="ExecuteAsync"/>) so it can be invoked directly in tests
    /// without driving the timer loop. Scoped services are resolved from a
    /// freshly created scope, since this class itself is registered as a
    /// singleton hosted service.
    /// </summary>
    public async Task RevalidateAllAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuthorizedGuestRepository>();
        var eligibilityService = scope.ServiceProvider.GetRequiredService<IGuestEligibilityService>();
        var uniFiClient = scope.ServiceProvider.GetRequiredService<IUniFiClientService>();

        var guests = await repository.GetAllAsync(cancellationToken);
        foreach (var guest in guests)
        {
            await RevalidateOneAsync(guest, repository, eligibilityService, uniFiClient, cancellationToken);
        }
    }

    /// <summary>
    /// Re-validates a single guest, swallowing (and logging) any failure so
    /// one problematic lookup — e.g. Graph throttling — doesn't stop the
    /// rest of the batch from being processed this cycle.
    /// </summary>
    private async Task RevalidateOneAsync(
        AuthorizedGuest guest,
        IAuthorizedGuestRepository repository,
        IGuestEligibilityService eligibilityService,
        IUniFiClientService uniFiClient,
        CancellationToken cancellationToken)
    {
        try
        {
            var isEligible = await eligibilityService.IsEligibleAsync(guest.UserObjectId, cancellationToken);
            if (isEligible)
            {
                await repository.MarkValidatedAsync(guest.MacAddress, cancellationToken);
                return;
            }

            _logger.LogInformation("Revoking UniFi authorization for {Mac}: user no longer eligible", guest.MacAddress);
            await uniFiClient.UnauthorizeGuestAsync(guest.MacAddress, cancellationToken);
            await repository.DeleteAsync(guest.MacAddress, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-validate guest {Mac}; leaving their access unchanged this cycle", guest.MacAddress);
        }
    }
}
