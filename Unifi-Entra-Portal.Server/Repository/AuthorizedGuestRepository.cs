using Microsoft.EntityFrameworkCore;
using Unifi_Entra_Portal.Server.DbModel;
using Unifi_Entra_Portal.Server.Models;
using Unifi_Entra_Portal.Server.Repository.Abstractions;

namespace Unifi_Entra_Portal.Server.Repository;

/// <inheritdoc cref="IAuthorizedGuestRepository" />
public class AuthorizedGuestRepository : IAuthorizedGuestRepository
{
    private readonly PortalDbContext _context;

    public AuthorizedGuestRepository(PortalDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(string macAddress, string userObjectId, string? userPrincipalName, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await FindTrackedOrQueryAsync(macAddress, cancellationToken);

        if (existing is not null)
        {
            existing.UserObjectId = userObjectId;
            existing.UserPrincipalName = userPrincipalName;
            existing.AuthorizedAtUtc = now;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        _context.AuthorizedGuests.Add(new AuthorizedGuest
        {
            MacAddress = macAddress,
            UserObjectId = userObjectId,
            UserPrincipalName = userPrincipalName,
            AuthorizedAtUtc = now,
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // MacAddress has a unique index — another concurrent authorize
            // call for the same device won the insert race. Drop our failed
            // insert attempt and fall back to updating their row instead of
            // surfacing a 500 for what is really just a duplicate request.
            _context.ChangeTracker.Clear();
            var winner = await _context.AuthorizedGuests.SingleAsync(g => g.MacAddress == macAddress, cancellationToken);
            winner.UserObjectId = userObjectId;
            winner.UserPrincipalName = userPrincipalName;
            winner.AuthorizedAtUtc = now;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuthorizedGuest>> GetAllAsync(CancellationToken cancellationToken) =>
        await _context.AuthorizedGuests.ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task MarkValidatedAsync(string macAddress, CancellationToken cancellationToken)
    {
        var existing = await FindTrackedOrQueryAsync(macAddress, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.LastValidatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string macAddress, CancellationToken cancellationToken)
    {
        var existing = await FindTrackedOrQueryAsync(macAddress, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _context.AuthorizedGuests.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Looks for an already-tracked entity in this context's local change
    /// tracker before issuing a database query. <see cref="GetAllAsync"/>
    /// now returns tracked entities specifically so that the revalidation
    /// job — which loads every guest via GetAllAsync and then immediately
    /// calls MarkValidatedAsync/DeleteAsync per guest on the same scoped
    /// context — doesn't re-query a row it just fetched.
    /// </summary>
    private async Task<AuthorizedGuest?> FindTrackedOrQueryAsync(string macAddress, CancellationToken cancellationToken)
    {
        var tracked = _context.AuthorizedGuests.Local.FirstOrDefault(g => g.MacAddress == macAddress);
        return tracked ?? await _context.AuthorizedGuests.SingleOrDefaultAsync(g => g.MacAddress == macAddress, cancellationToken);
    }
}
