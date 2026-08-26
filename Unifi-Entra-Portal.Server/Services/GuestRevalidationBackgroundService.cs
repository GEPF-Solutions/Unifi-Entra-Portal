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
        var interval = TimeSpan.FromHours(_settings.IntervalHours);
        if (_settings.IntervalHours <= 0)
        {
            _logger.LogWarning(
                "Revalidation:IntervalHours was {ConfiguredValue}, which is invalid; defaulting to 24 hours instead of crashing on startup",
                _settings.IntervalHours);
            interval = TimeSpan.FromHours(24);
        }

        using var timer = new PeriodicTimer(interval);

        do
        {
            // ASP.NET Core's default hosted-service behavior is to stop the
            // entire application if a BackgroundService's ExecuteAsync
            // throws — an unhandled failure here (DB unreachable, a bad
            // AzureAd secret breaking Graph token acquisition, etc.) would
            // otherwise take down guest sign-in along with revalidation.
            // RevalidateOneAsync already isolates per-guest failures; this
            // isolates whole-cycle failures the same way.
            try
            {
                await RevalidateAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Revalidation pass failed; will retry next cycle");
            }
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

        // A user with multiple devices produces one AuthorizedGuest row per
        // device, all sharing the same UserObjectId. Their eligibility
        // (account enabled + group membership) doesn't vary by device, so
        // cache the check per user for this pass — a member with 3 devices
        // costs one Graph lookup here instead of three identical ones.
        // Caching the Task itself (not just its result) means a failure is
        // shared too: every device is correctly treated as "couldn't
        // re-validate this cycle" rather than only the first one checked.
        var eligibilityByUser = new Dictionary<string, Task<bool>>();

        foreach (var guest in guests)
        {
            if (!eligibilityByUser.TryGetValue(guest.UserObjectId, out var eligibilityTask))
            {
                eligibilityTask = eligibilityService.IsEligibleAsync(guest.UserObjectId, cancellationToken);
                eligibilityByUser[guest.UserObjectId] = eligibilityTask;
            }

            await RevalidateOneAsync(guest, eligibilityTask, repository, uniFiClient, cancellationToken);
        }
    }

    /// <summary>
    /// Re-validates a single guest device against an already-started (and
    /// possibly shared, see <see cref="RevalidateAllAsync"/>) eligibility
    /// check, swallowing (and logging) any failure so one problematic
    /// lookup — e.g. Graph throttling — doesn't stop the rest of the batch
    /// from being processed this cycle.
    /// </summary>
    private async Task RevalidateOneAsync(
        AuthorizedGuest guest,
        Task<bool> eligibilityTask,
        IAuthorizedGuestRepository repository,
        IUniFiClientService uniFiClient,
        CancellationToken cancellationToken)
    {
        try
        {
            var isEligible = await eligibilityTask;
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
