using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>facility-0092: the first Admin comes from the server's settings, only while no Admin exists at all.</summary>
public class FirstAdminTests
{
    private const string Password = "first-admin-pass-2026";
    private static readonly Pbkdf2PasswordHasher Hasher = new(iterations: 1_000);
    private static readonly TestTimeProvider Clock = new(ThaiClock.At(27, 9, 0));

    private static FirstAdminSettings Settings(string? username = "first.admin", string? password = Password, string? displayName = "แอดมินคนแรก") =>
        new() { Username = username, Password = password, DisplayName = displayName };

    [Fact]
    public async Task Creates_one_active_Admin_when_none_exists_and_logs_it_as_the_system()
    {
        await using var empty = await EmptyDatabase.CreateAsync();
        var logger = new ListLogger();

        var outcome = await FirstAdmin.EnsureAsync(empty.Db, Hasher, Clock, Settings(), logger);

        Assert.Equal(FirstAdminOutcome.Created, outcome);
        var admin = await empty.Db.Users.SingleAsync();
        Assert.Equal((UserRole.Admin, "first.admin", "แอดมินคนแรก", true), (admin.Role, admin.Username, admin.DisplayName, admin.IsActive));
        Assert.True(Hasher.Verify(Password, admin.SecretHash));
        var entry = await empty.Db.AuditLog.SingleAsync();
        Assert.Equal(("ADMIN_FIRST_CREATE", (int?)null, (long?)admin.Id), (entry.Action, entry.ActorId, entry.EntityId));
        Assert.DoesNotContain(Password, entry.Summary + entry.AfterJson);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains(Password));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("Created the first Admin 'first.admin'"));
    }

    [Fact]
    public async Task Does_nothing_while_any_Admin_exists_even_a_turned_off_one()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var seeded = await db.Users.SingleAsync(u => u.Username == "admin");
            seeded.IsActive = false;
            await db.SaveChangesAsync();
            var hashBefore = seeded.SecretHash;
            var logger = new ListLogger();

            var outcome = await FirstAdmin.EnsureAsync(db, Hasher, Clock, Settings(username: "admin"), logger);

            Assert.Equal(FirstAdminOutcome.AdminExists, outcome);
            var admin = await db.Users.SingleAsync(u => u.Role == UserRole.Admin);
            Assert.Equal((false, hashBefore), (admin.IsActive, admin.SecretHash));
            Assert.Empty(await db.AuditLog.ToListAsync());
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("FirstAdmin__Password is still set"));
        });
    }

    [Fact]
    public async Task With_no_Admin_and_no_settings_it_starts_and_warns_which_settings_to_set()
    {
        await using var empty = await EmptyDatabase.CreateAsync();
        var logger = new ListLogger();

        var outcome = await FirstAdmin.EnsureAsync(empty.Db, Hasher, Clock, new FirstAdminSettings(), logger);

        Assert.Equal(FirstAdminOutcome.NotConfigured, outcome);
        Assert.Empty(await empty.Db.Users.ToListAsync());
        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("FirstAdmin__Username, FirstAdmin__Password and FirstAdmin__DisplayName", warning.Message);
    }

    [Theory]
    [InlineData("short-pass")]
    [InlineData("1qaz2wsx3edc4rfv")]
    [InlineData(null)]
    public async Task A_password_against_the_rule_stops_the_startup_without_printing_it(string? password)
    {
        await using var empty = await EmptyDatabase.CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FirstAdmin.EnsureAsync(empty.Db, Hasher, Clock, Settings(password: password), new ListLogger()));

        Assert.StartsWith("FirstAdmin__Password", error.Message);
        if (password is not null)
        {
            Assert.DoesNotContain(password, error.Message);
        }

        Assert.Empty(await empty.Db.Users.ToListAsync());
    }

    [Theory]
    [InlineData(null, "แอดมินคนแรก", "FirstAdmin__Username")]
    [InlineData("a b", "แอดมินคนแรก", "FirstAdmin__Username")]
    [InlineData("first.admin", null, "FirstAdmin__DisplayName")]
    [InlineData("first.admin", "   ", "FirstAdmin__DisplayName")]
    public async Task A_missing_or_bad_username_or_display_name_stops_the_startup(string? username, string? displayName, string named)
    {
        await using var empty = await EmptyDatabase.CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FirstAdmin.EnsureAsync(empty.Db, Hasher, Clock, Settings(username: username, displayName: displayName), new ListLogger()));

        Assert.StartsWith(named, error.Message);
        Assert.DoesNotContain(Password, error.Message);
        Assert.Empty(await empty.Db.Users.ToListAsync());
    }
}
