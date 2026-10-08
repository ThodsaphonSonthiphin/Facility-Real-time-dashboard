using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.UnitTests;

public class MigrationTests
{
    /// <summary>
    /// The API tests run on SQLite with EnsureCreated, so they cannot notice a missing MySQL migration.
    /// This compares the model with the latest migration snapshot; no database connection is opened.
    /// </summary>
    [Fact]
    public void Migrations_match_the_model()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL("Server=localhost;Database=unused;Uid=unused;Pwd=unused;")
            .Options;
        using var db = new AppDbContext(options);

        Assert.False(db.Database.HasPendingModelChanges(), "Run: dotnet ef migrations add <Name> (see Task 6 of the pilot data model plan)");
    }
}
