using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FacilityRealtime.ApiTests.Auth;

public class LoginTests
{
    [Fact]
    public async Task Valid_login_returns_a_five_minute_access_token_and_the_user()
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await AuthApi.ReadAuthAsync(response);
        Assert.Equal("somchai", body.User.Username);
        Assert.Equal("สมชาย ใจดี", body.User.FullName);
        Assert.Equal("cleaner", body.User.Role);
        var expected = factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(5);
        Assert.InRange(body.ExpiresAt.ToUniversalTime(), expected.AddSeconds(-1), expected.AddSeconds(1));
    }

    [Fact]
    public async Task Access_token_carries_user_id_username_name_and_role()
    {
        using var factory = new FacilityApiFactory();

        var body = await AuthApi.ReadAuthAsync(await AuthApi.LoginAsync(factory.CreateApiClient()));

        var jwt = new JsonWebToken(body.AccessToken);
        Assert.Equal(body.User.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("somchai", jwt.GetClaim("preferred_username").Value);
        Assert.Equal("สมชาย ใจดี", jwt.GetClaim("name").Value);
        Assert.Equal("cleaner", jwt.GetClaim("role").Value);
        Assert.Equal(TimeSpan.FromMinutes(5), jwt.ValidTo - jwt.IssuedAt);
    }

    [Fact]
    public async Task Admin_login_reports_the_admin_role()
    {
        using var factory = new FacilityApiFactory();

        var body = await AuthApi.ReadAuthAsync(await AuthApi.LoginAsync(factory.CreateApiClient(), "admin", "admin1234"));

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
        Assert.DoesNotContain("secure", header); // ADR facility-0013: phones reach the POC over plain HTTP
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
    [InlineData("somchai", "wrong-password")]
    [InlineData("nobody", "password123")]
    public async Task Bad_credentials_are_401_and_set_no_cookie(string username, string password)
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LoginAsync(factory.CreateApiClient(), username, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(AuthApi.RefreshSetCookieHeader(response));
    }

    [Fact]
    public async Task Deactivated_account_cannot_log_in()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Username == "somchai")).IsActive = false;
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
