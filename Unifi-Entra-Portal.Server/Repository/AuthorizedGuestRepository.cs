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
        var existing = await _context.AuthorizedGuests.SingleOrDefaultAsync(g => g.MacAddress == macAddress, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            _context.AuthorizedGuests.Add(new AuthorizedGuest
            {
                MacAddress = macAddress,
                UserObjectId = userObjectId,
                UserPrincipalName = userPrincipalName,
                AuthorizedAtUtc = now,
            });
        }
        else
        {
            existing.UserObjectId = userObjectId;
            existing.UserPrincipalName = userPrincipalName;
            existing.AuthorizedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuthorizedGuest>> GetAllAsync(CancellationToken cancellationToken) =>
        await _context.AuthorizedGuests.AsNoTracking().ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task MarkValidatedAsync(string macAddress, CancellationToken cancellationToken)
    {
        var existing = await _context.AuthorizedGuests.SingleOrDefaultAsync(g => g.MacAddress == macAddress, cancellationToken);
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
        var existing = await _context.AuthorizedGuests.SingleOrDefaultAsync(g => g.MacAddress == macAddress, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _context.AuthorizedGuests.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
