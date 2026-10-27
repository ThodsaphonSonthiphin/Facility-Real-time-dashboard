using System.Net;
using FacilityRealtime.Api.Commands;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0092 decision 5: reset-admin recovers Admin access from the server's console.</summary>
public class ResetAdminCommandTests
{
    private const string NewPassword = "recovered-pass-2026";

    /// <summary>Answers the prompts in order, and keeps what was written and which prompts hid the typing.</summary>
    private sealed class FakeConsole(params string?[] answers) : IAdminConsole
    {
        private readonly Queue<string?> _answers = new(answers);

        public List<string> Written { get; } = [];
        public List<string> ShownPrompts { get; } = [];
        public List<string> HiddenPrompts { get; } = [];

        public string? ReadLine(string prompt)
        {
            ShownPrompts.Add(prompt);
            return Next();
        }

        public string? ReadSecret(string prompt)
        {
            HiddenPrompts.Add(prompt);
            return Next();
        }

        public void WriteLine(string text) => Written.Add(text);

        private string? Next() => _answers.Count > 0 ? _answers.Dequeue() : null;
    }

    private static async Task<int> RunAsync(FacilityApiFactory factory, FakeConsole console, params string[] args)
    {
        var exitCode = -1;
        await factory.WithDbAsync(async db => exitCode = await ResetAdminCommand.RunAsync(
            args, db, factory.Services.GetRequiredService<IPasswordHasher>(), factory.Clock, console));
        return exitCode;
    }

    [Fact]
    public async Task Resets_a_turned_off_Admin_turns_it_on_and_ends_every_session()
    {
        using var factory = new FacilityApiFactory();
        await AuthApi.LoggedInAdminAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Username == "admin")).IsActive = false;
            await db.SaveChangesAsync();
        });
        var console = new FakeConsole(NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, "ADMIN");

        Assert.Equal(0, exitCode);
        Assert.Equal(2, console.HiddenPrompts.Count);
        Assert.Empty(console.ShownPrompts);
        Assert.DoesNotContain(console.Written, line => line.Contains(NewPassword));
        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            Assert.True(admin.IsActive);
            var tokens = await db.RefreshTokens.Where(t => t.UserId == admin.Id).ToListAsync();
            Assert.NotEmpty(tokens);
            Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
            var entry = await db.AuditLog.SingleAsync();
            Assert.Equal(("ADMIN_RECOVER_RESET", (int?)null, (long?)admin.Id), (entry.Action, entry.ActorId, entry.EntityId));
            Assert.Contains("และเปิดใช้บัญชี", entry.Summary);
            Assert.DoesNotContain(NewPassword, entry.Summary);
        });
        var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.AdminLoginAsync(client, "admin", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Creates_a_new_Admin_for_a_name_nobody_has()
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole("แอดมินสำรอง", NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, "backup.admin");

        Assert.Equal(0, exitCode);
        Assert.Single(console.ShownPrompts);
        Assert.Equal(2, console.HiddenPrompts.Count);
        await factory.WithDbAsync(async db =>
        {
            var created = await db.Users.SingleAsync(u => u.Username == "backup.admin");
            Assert.Equal((UserRole.Admin, "แอดมินสำรอง", true), (created.Role, created.DisplayName, created.IsActive));
            var entry = await db.AuditLog.SingleAsync();
            Assert.Equal(("ADMIN_RECOVER_CREATE", (int?)null, (long?)created.Id), (entry.Action, entry.ActorId, entry.EntityId));
        });
    }

    [Fact]
    public async Task Refuses_a_Cleaners_employee_ID_and_changes_nothing()
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole(NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, "e1001");

        Assert.Equal(1, exitCode);
        Assert.Empty(console.HiddenPrompts);
        Assert.Contains(console.Written, line => line.Contains("รหัสพนักงานของแม่บ้าน"));
        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.Users.AnyAsync(u => u.Username == "e1001"));
            Assert.Empty(await db.AuditLog.ToListAsync());
        });
    }

    [Fact]
    public async Task Two_different_passwords_change_nothing()
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole(NewPassword, NewPassword + "x");

        var exitCode = await RunAsync(factory, console, "admin");

        Assert.Equal(1, exitCode);
        Assert.Contains(console.Written, line => line.Contains("รหัสผ่านสองครั้งไม่ตรงกัน"));
        await AssertUnchangedAsync(factory);
    }

    [Theory]
    [InlineData("short-pass")]
    [InlineData("1qaz2wsx3edc4rfv")]
    public async Task A_password_against_the_rule_changes_nothing(string password)
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole(password, password);

        var exitCode = await RunAsync(factory, console, "admin");

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(console.Written, line => line.Contains(password));
        await AssertUnchangedAsync(factory);
    }

    [Fact]
    public async Task A_new_Admin_needs_a_display_name()
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole("   ", NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, "backup.admin");

        Assert.Equal(1, exitCode);
        Assert.Empty(console.HiddenPrompts);
        await factory.WithDbAsync(async db => Assert.False(await db.Users.AnyAsync(u => u.Username == "backup.admin")));
    }

    public static TheoryData<int, string[]> WrongUsage => new()
    {
        { 2, [] },
        { 1, ["a b"] },
        { 2, ["admin", "extra"] },
    };

    [Theory]
    [MemberData(nameof(WrongUsage))]
    public async Task Wrong_usage_or_a_bad_username_changes_nothing(int expectedExitCode, string[] args)
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole(NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, args);

        Assert.Equal(expectedExitCode, exitCode);
        Assert.Empty(console.HiddenPrompts);
        await AssertUnchangedAsync(factory);
    }

    /// <summary>The seeded Admin keeps the seed's password and nothing is logged.</summary>
    private static Task AssertUnchangedAsync(FacilityApiFactory factory) =>
        factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            Assert.True(factory.Services.GetRequiredService<IPasswordHasher>().Verify("admin1234", admin.SecretHash));
            Assert.Empty(await db.AuditLog.ToListAsync());
        });
}
