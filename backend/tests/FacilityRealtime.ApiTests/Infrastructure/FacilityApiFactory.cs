using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>
/// Boots the real Program in the "Testing" environment: Program skips its MySQL registration and
/// startup migration there, and this factory supplies in-memory SQLite plus a movable clock.
/// Create one per test when the test mutates data or the clock.
/// </summary>
public sealed class FacilityApiFactory(string signingKey = FacilityApiFactory.TestSigningKey) : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "test-signing-key-that-is-at-least-32-bytes-long-0123456789";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting reaches Program before builder.Build(), unlike ConfigureAppConfiguration
        builder.UseSetting("Jwt:SigningKey", signingKey);

        builder.ConfigureServices(services =>
        {
            _connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        DbInitializer.SeedAsync(db).GetAwaiter().GetResult();
        return host;
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
