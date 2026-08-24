using Microsoft.EntityFrameworkCore;
using Unifi_Entra_Portal.Server.Models;

namespace Unifi_Entra_Portal.Server.DbModel;

/// <summary>
/// EF Core context for the portal's own persistence (currently just the
/// MAC-to-user mapping needed for background re-validation). Backed by
/// SQLite — a single file, no separate database server to run or operate.
/// </summary>
public class PortalDbContext : DbContext
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options)
    {
    }

    public DbSet<AuthorizedGuest> AuthorizedGuests => Set<AuthorizedGuest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthorizedGuest>()
            .HasIndex(g => g.MacAddress)
            .IsUnique();
    }
}
