using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FacilityRealtime.ApiTests.Auth;

/// <summary>
/// facility-0095..0097: one Warning line per security event, in category FacilityRealtime.Security. An account is
/// named by employee ID (an Admin by username); what was typed for an account that does not exist never appears.
/// </summary>
public class SecurityLogTests
{
    private static List<string> SecurityLines(FacilityApiFactory factory) =>
        factory.Logs.Lines
            .Where(line => line.Category == "FacilityRealtime.Security")
            .Select(line => { Assert.Equal(LogLevel.Warning, line.Level); return line.Message; })
            .ToList();

    private static void NowhereInTheLog(FacilityApiFactory factory, string text) =>
        Assert.DoesNotContain(factory.Logs.Lines, line => line.Message.Contains(text, StringComparison.Ordinal));

    [Fact]
    public async Task A_wrong_phone_names_the_employee_and_never_the_phone()
    {
        using var factory = new FacilityApiFactory();

        await AuthApi.LoginAsync(factory.CreateApiClient(), "1000001", "0899999999");

        Assert.Equal(new[] { "Login failed: employee 1000001, wrong secret" }, SecurityLines(factory));
        NowhereInTheLog(factory, "0899999999");
    }

    [Fact]
    public async Task A_turned_off_account_with_the_right_phone_says_so()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "1000001")).IsActive = false;
            await db.SaveChangesAsync();
        });

        await AuthApi.LoginAsync(factory.CreateApiClient(), "1000001", "0810000001");

        Assert.Equal(new[] { "Login failed: employee 1000001, account turned off" }, SecurityLines(factory));
        NowhereInTheLog(factory, "0810000001");
    }

    [Fact]
    public async Task A_turned_off_account_with_a_wrong_phone_is_a_wrong_secret()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "1000001")).IsActive = false;
            await db.SaveChangesAsync();
        });

        // "account turned off" is written only once the secret matched, so it never tells a guesser more
        await AuthApi.LoginAsync(factory.CreateApiClient(), "1000001", "0899999999");

        Assert.Equal(new[] { "Login failed: employee 1000001, wrong secret" }, SecurityLines(factory));
        NowhereInTheLog(factory, "0899999999");
    }

    [Fact]
    public async Task A_phone_typed_into_the_employee_id_field_is_never_written()
    {
        using var factory = new FacilityApiFactory();

        // both fields take digits only (facility-0054), so the fields get swapped
        await AuthApi.LoginAsync(factory.CreateApiClient(), "0810000001", "1000001");

        Assert.Equal(new[] { "Login failed: unknown account (employee login)" }, SecurityLines(factory));
        NowhereInTheLog(factory, "0810000001");
    }

    [Fact]
    public async Task A_login_name_longer_than_any_account_is_an_unknown_account()
    {
        using var factory = new FacilityApiFactory();

        await AuthApi.LoginAsync(factory.CreateApiClient(), new string('1', 101), "0810000001");

        Assert.Equal(new[] { "Login failed: unknown account (employee login)" }, SecurityLines(factory));
    }

    [Fact]
    public async Task An_admins_wrong_password_names_the_username_and_never_the_password()
    {
        using var factory = new FacilityApiFactory();
        var typed = $"wrong-{Guid.NewGuid():N}"; // built at run time: no password literal in the repo

        await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin", typed);

        Assert.Equal(new[] { "Login failed: admin admin, wrong secret" }, SecurityLines(factory));
        NowhereInTheLog(factory, typed);
    }

    [Fact]
    public async Task Each_attempt_refused_while_locked_is_written()
    {
        using var factory = new FacilityApiFactory();
        var client = factory.CreateApiClient();
        for (var i = 0; i < 5; i++)
        {
            await AuthApi.LoginAsync(client, "1000001", "0899999999");
        }

        var refused = await AuthApi.LoginAsync(client, "1000001", "0810000001");
        await AuthApi.LoginAsync(client, "1000001", "0810000001");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        var lines = SecurityLines(factory);
        Assert.Equal(Enumerable.Repeat("Login failed: employee 1000001, wrong secret", 5), lines.Take(5));
        Assert.Equal(new[] { "Login refused while locked: employee 1000001", "Login refused while locked: employee 1000001" }, lines.Skip(5));
    }

    [Fact]
    public async Task A_locked_unknown_account_is_written_without_what_was_typed()
    {
        using var factory = new FacilityApiFactory();
        var client = factory.CreateApiClient();
        for (var i = 0; i < 6; i++)
        {
            await AuthApi.LoginAsync(client, "9999999", "0812345678");
        }

        Assert.Equal("Login refused while locked: unknown account (employee login)", SecurityLines(factory).Last());
        NowhereInTheLog(factory, "9999999");
        NowhereInTheLog(factory, "0812345678");
    }

    [Fact]
    public async Task A_reused_refresh_token_names_the_account_whose_session_ended()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);
        await AuthApi.RefreshAsync(client, first);

        factory.Clock.Advance(TimeSpan.FromSeconds(31)); // past the 30 s grace of facility-0015
        var replay = await AuthApi.RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(new[] { "Refresh token reused, session ended: employee 1000001" }, SecurityLines(factory));
        NowhereInTheLog(factory, first);
    }

    [Fact]
    public async Task An_unknown_admin_username_is_written_without_what_was_typed()
    {
        using var factory = new FacilityApiFactory();
        var typed = $"nobody-{Guid.NewGuid():N}"; // built at run time: no login literal in the repo

        await AuthApi.AdminLoginAsync(factory.CreateApiClient(), typed, $"wrong-{Guid.NewGuid():N}");

        Assert.Equal(new[] { "Login failed: unknown account (admin login)" }, SecurityLines(factory));
        NowhereInTheLog(factory, typed);
    }

    [Fact]
    public async Task A_locked_admin_is_written_with_the_username_and_never_the_password()
    {
        using var factory = new FacilityApiFactory();
        var client = factory.CreateApiClient();
        var typed = $"wrong-{Guid.NewGuid():N}"; // built at run time: no password literal in the repo
        for (var i = 0; i < 5; i++)
        {
            await AuthApi.AdminLoginAsync(client, "admin", typed);
        }

        var refused = await AuthApi.AdminLoginAsync(client, "admin", typed);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("Login refused while locked: admin admin", SecurityLines(factory).Last());
        NowhereInTheLog(factory, typed);
    }

    [Fact]
    public async Task A_locked_unknown_admin_username_is_written_without_what_was_typed()
    {
        using var factory = new FacilityApiFactory();
        var client = factory.CreateApiClient();
        var typed = $"nobody-{Guid.NewGuid():N}"; // built at run time: no login literal in the repo
        for (var i = 0; i < 6; i++)
        {
            await AuthApi.AdminLoginAsync(client, typed, $"wrong-{Guid.NewGuid():N}");
        }

        Assert.Equal("Login refused while locked: unknown account (admin login)", SecurityLines(factory).Last());
        NowhereInTheLog(factory, typed);
    }

    [Fact]
    public async Task An_admins_reused_refresh_token_names_the_admin_username()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAdminAsync(factory);
        await AuthApi.RefreshAsync(client, first);

        factory.Clock.Advance(TimeSpan.FromSeconds(31)); // past the 30 s grace of facility-0015
        var replay = await AuthApi.RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(new[] { "Refresh token reused, session ended: admin admin" }, SecurityLines(factory));
        NowhereInTheLog(factory, first);
    }

    [Fact]
    public async Task A_successful_login_writes_no_security_line()
    {
        using var factory = new FacilityApiFactory();

        await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.Empty(SecurityLines(factory));
    }
}
