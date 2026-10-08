using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FacilityRealtime.ApiTests.Auth;

public class LoginTests
{
    [Fact]
    public async Task Cleaner_logs_in_with_employee_id_and_phone()
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await AuthApi.ReadAuthAsync(response);
        Assert.Equal("E1001", body.User.Username);
        Assert.Equal("สมชาย ใจดี", body.User.FullName);
        Assert.Equal("cleaner", body.User.Role);
        var expected = factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(5);
        Assert.InRange(body.ExpiresAt.ToUniversalTime(), expected.AddSeconds(-1), expected.AddSeconds(1));
    }

    [Theory]
    [InlineData("081-000-0001")]
    [InlineData("+66 81 000 0001")]
    [InlineData(" 0810000001 ")]
    public async Task Phone_may_be_typed_with_spaces_dashes_or_country_code(string phone)
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LoginAsync(factory.CreateApiClient(), "E1001", phone);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Supervisor_logs_in_the_same_way()
    {
        using var factory = new FacilityApiFactory();

        var body = await AuthApi.ReadAuthAsync(await AuthApi.LoginAsync(factory.CreateApiClient(), "S2001", "0820000001"));

        Assert.Equal("S2001", body.User.Username);
        Assert.Equal("supervisor", body.User.Role);
    }

    [Fact]
    public async Task Access_token_carries_user_id_login_name_display_name_and_role()
    {
        using var factory = new FacilityApiFactory();

        var body = await AuthApi.ReadAuthAsync(await AuthApi.LoginAsync(factory.CreateApiClient()));

        var jwt = new JsonWebToken(body.AccessToken);
        Assert.Equal(body.User.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("E1001", jwt.GetClaim("preferred_username").Value);
        Assert.Equal("สมชาย ใจดี", jwt.GetClaim("name").Value);
        Assert.Equal("cleaner", jwt.GetClaim("role").Value);
        Assert.Equal(TimeSpan.FromMinutes(5), jwt.ValidTo - jwt.IssuedAt);
    }

    [Fact]
    public async Task Admin_logs_in_with_username_and_password()
    {
        using var factory = new FacilityApiFactory();

        var body = await AuthApi.ReadAuthAsync(await AuthApi.AdminLoginAsync(factory.CreateApiClient()));

        Assert.Equal("admin", body.User.Username);
        Assert.Equal("admin", body.User.Role);
    }

    [Fact]
    public async Task Login_sets_an_http_only_strict_refresh_cookie_scoped_to_auth_routes()
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LoginAsync(factory.CreateApiClient());

        var header = AuthApi.RefreshSetCookieHeader(response)!.ToLowerInvariant();
        Assert.Contains("httponly", header);
        Assert.Contains("samesite=strict", header);
        Assert.Contains("path=/api/auth", header);
        Assert.Contains("expires=", header);
        Assert.DoesNotContain("secure", header); // ADR facility-0013: Jwt:RefreshCookieSecure is false in development
    }

    [Fact]
    public async Task Only_the_hash_of_the_refresh_token_is_stored()
    {
        using var factory = new FacilityApiFactory();
        var cookie = AuthApi.RefreshCookieValue(await AuthApi.LoginAsync(factory.CreateApiClient()))!;
        var stored = new List<string>();

        await factory.WithDbAsync(async db => stored = await db.RefreshTokens.Select(t => t.TokenHash).ToListAsync());

        Assert.Equal(43, cookie.Length);
        Assert.Equal(new[] { RefreshTokenRules.Hash(cookie) }, stored);
    }

    [Theory]
    [InlineData("E1001", "0899999999")]
    [InlineData("E9999", "0810000001")]
    public async Task Wrong_employee_id_or_phone_is_401_and_sets_no_cookie(string employeeId, string phone)
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LoginAsync(factory.CreateApiClient(), employeeId, phone);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(AuthApi.RefreshSetCookieHeader(response));
    }

    [Fact]
    public async Task Wrong_admin_password_is_401()
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin", "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cleaner_cannot_use_the_admin_form()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "E1001")).Username = "e1001-web";
            await db.SaveChangesAsync();
        });

        // The password equals the phone whose hash is stored, so only the Admin-role guard stops this.
        var response = await factory.CreateApiClient().PostAsJsonAsync("/api/auth/login", new { username = "e1001-web", password = "0810000001" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_use_the_employee_form()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            admin.EmployeeId = "A9000";
            admin.SecretHash = new Pbkdf2PasswordHasher(iterations: 1_000).Hash("0830000001");
            await db.SaveChangesAsync();
        });

        var response = await AuthApi.LoginAsync(factory.CreateApiClient(), "A9000", "0830000001");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_account_cannot_log_in()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "E1001")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void App_refuses_to_start_without_a_signing_key()
    {
        using var factory = new FacilityApiFactory(signingKey: "");

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("Jwt:SigningKey", error.ToString());
    }
}
