using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>An in-memory SQLite database with the schema and no seed: a server's first start (facility-0092).</summary>
public sealed class EmptyDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    private EmptyDatabase()
    {
        _connection.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    }

    public AppDbContext Db { get; }

    public static async Task<EmptyDatabase> CreateAsync()
    {
        var empty = new EmptyDatabase();
        await empty.Db.Database.EnsureCreatedAsync();
        return empty;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
