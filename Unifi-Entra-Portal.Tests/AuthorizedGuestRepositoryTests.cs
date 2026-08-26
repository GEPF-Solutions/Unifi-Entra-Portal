using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unifi_Entra_Portal.Server.DbModel;
using Unifi_Entra_Portal.Server.Repository;

namespace Unifi_Entra_Portal.Tests;

/// <summary>
/// Uses a real (in-memory) SQLite connection rather than EF Core's InMemory
/// provider, so the unique index on MacAddress and other SQLite-specific
/// behavior are actually exercised.
/// </summary>
public class AuthorizedGuestRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PortalDbContext _context;
    private readonly AuthorizedGuestRepository _repository;

    public AuthorizedGuestRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new PortalDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new AuthorizedGuestRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UpsertAsync_WhenMacIsNew_AddsRecord()
    {
        await _repository.UpsertAsync("AA:BB:CC:DD:EE:FF", "user-oid-1", "user@example.com", CancellationToken.None);

        var all = await _repository.GetAllAsync(CancellationToken.None);
        var record = Assert.Single(all);
        Assert.Equal("AA:BB:CC:DD:EE:FF", record.MacAddress);
        Assert.Equal("user-oid-1", record.UserObjectId);
        Assert.Equal("user@example.com", record.UserPrincipalName);
    }

    [Fact]
    public async Task UpsertAsync_WhenMacAlreadyExists_UpdatesExistingRecordInsteadOfDuplicating()
    {
        await _repository.UpsertAsync("AA:BB:CC:DD:EE:FF", "user-oid-1", "old@example.com", CancellationToken.None);
        await _repository.UpsertAsync("AA:BB:CC:DD:EE:FF", "user-oid-2", "new@example.com", CancellationToken.None);

        var all = await _repository.GetAllAsync(CancellationToken.None);
        var record = Assert.Single(all);
        Assert.Equal("user-oid-2", record.UserObjectId);
        Assert.Equal("new@example.com", record.UserPrincipalName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRecord()
    {
        await _repository.UpsertAsync("AA:BB:CC:DD:EE:FF", "user-oid-1", "user@example.com", CancellationToken.None);

        await _repository.DeleteAsync("AA:BB:CC:DD:EE:FF", CancellationToken.None);

        Assert.Empty(await _repository.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WhenMacNotTracked_DoesNotThrow()
    {
        await _repository.DeleteAsync("00:00:00:00:00:00", CancellationToken.None);
    }

    [Fact]
    public async Task MarkValidatedAsync_SetsLastValidatedAtUtc_WithoutChangingAuthorizedAtUtc()
    {
        await _repository.UpsertAsync("AA:BB:CC:DD:EE:FF", "user-oid-1", "user@example.com", CancellationToken.None);
        var beforeValidation = (await _repository.GetAllAsync(CancellationToken.None)).Single();
        Assert.Null(beforeValidation.LastValidatedAtUtc);

        await _repository.MarkValidatedAsync("AA:BB:CC:DD:EE:FF", CancellationToken.None);

        var afterValidation = (await _repository.GetAllAsync(CancellationToken.None)).Single();
        Assert.NotNull(afterValidation.LastValidatedAtUtc);
        Assert.Equal(beforeValidation.AuthorizedAtUtc, afterValidation.AuthorizedAtUtc);
    }

    [Fact]
    public async Task MarkValidatedAsync_WhenMacNotTracked_DoesNotThrow()
    {
        await _repository.MarkValidatedAsync("00:00:00:00:00:00", CancellationToken.None);
    }
}
