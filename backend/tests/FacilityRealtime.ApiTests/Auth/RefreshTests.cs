using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FacilityRealtime.ApiTests.Auth;

public class RefreshTests
{
    [Fact]
    public async Task Refresh_rotates_the_cookie_and_returns_a_new_access_token()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);

        var response = await AuthApi.RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = AuthApi.RefreshCookieValue(response);
        Assert.False(string.IsNullOrEmpty(second));
        Assert.NotEqual(first, second);
        Assert.Equal("somchai", (await AuthApi.ReadAuthAsync(response)).User.Username);
    }

    [Fact]
    public async Task Rotated_token_replayed_within_30_seconds_still_refreshes()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);
        await AuthApi.RefreshAsync(client, first);

        factory.Clock.Advance(TimeSpan.FromSeconds(20)); // the Dashboard tab and the scan tab refreshed together
        var replay = await AuthApi.RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    }

    [Fact]
    public async Task Rotated_token_replayed_after_30_seconds_revokes_the_whole_login()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);
        var second = AuthApi.RefreshCookieValue(await AuthApi.RefreshAsync(client, first))!;

        factory.Clock.Advance(TimeSpan.FromSeconds(31));
        var replay = await AuthApi.RefreshAsync(client, first);
        var legitimate = await AuthApi.RefreshAsync(client, second);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.StatusCode);
    }

    [Fact]
    public async Task Reuse_detection_leaves_the_same_users_other_logins_alone()
    {
        using var factory = new FacilityApiFactory();
        var (phone, _, phoneToken) = await AuthApi.LoggedInAsync(factory);
        var (_, _, officeToken) = await AuthApi.LoggedInAsync(factory);
        await AuthApi.RefreshAsync(phone, phoneToken);

        factory.Clock.Advance(TimeSpan.FromSeconds(31));
        await AuthApi.RefreshAsync(phone, phoneToken);
        var office = await AuthApi.RefreshAsync(phone, officeToken);

        Assert.Equal(HttpStatusCode.OK, office.StatusCode);
    }

    [Fact]
    public async Task Token_unused_for_30_days_expires()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, token) = await AuthApi.LoggedInAsync(factory);

        factory.Clock.Advance(TimeSpan.FromDays(30));
        var response = await AuthApi.RefreshAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Each_refresh_gives_the_session_another_30_days()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, token) = await AuthApi.LoggedInAsync(factory);

        factory.Clock.Advance(TimeSpan.FromDays(29));
        var next = AuthApi.RefreshCookieValue(await AuthApi.RefreshAsync(client, token))!;
        factory.Clock.Advance(TimeSpan.FromDays(29));
        var response = await AuthApi.RefreshAsync(client, next);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_account_cannot_refresh_and_all_its_sessions_are_revoked()
    {
        using var factory = new FacilityApiFactory();
        var (client, auth, token) = await AuthApi.LoggedInAsync(factory);
        await AuthApi.LoggedInAsync(factory); // a second login on another device
        await SetActiveAsync(factory, auth.User.Id, false);

        var whileInactive = await AuthApi.RefreshAsync(client, token);
        await SetActiveAsync(factory, auth.User.Id, true);
        var afterReactivation = await AuthApi.RefreshAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, whileInactive.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReactivation.StatusCode);
        var liveTokens = -1;
        await factory.WithDbAsync(async db => liveTokens = await db.RefreshTokens.CountAsync(t => t.UserId == auth.User.Id && t.RevokedAt == null));
        Assert.Equal(0, liveTokens);
    }

    [Fact]
    public async Task Role_change_takes_effect_on_the_next_refresh()
    {
        using var factory = new FacilityApiFactory();
        var (client, auth, token) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == auth.User.Id)).Role = "admin";
            await db.SaveChangesAsync();
        });

        var refreshed = await AuthApi.ReadAuthAsync(await AuthApi.RefreshAsync(client, token));

        Assert.Equal("admin", refreshed.User.Role);
        Assert.Equal("admin", new JsonWebToken(refreshed.AccessToken).GetClaim("role").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token-this-server-issued")]
    public async Task Missing_or_unknown_cookie_is_401_and_clears_the_cookie(string? cookie)
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.RefreshAsync(factory.CreateApiClient(), cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("", AuthApi.RefreshCookieValue(response));
    }

    [Fact]
    public async Task Logout_revokes_that_login_and_deletes_the_cookie()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);
        var second = AuthApi.RefreshCookieValue(await AuthApi.RefreshAsync(client, first))!;

        var logout = await AuthApi.LogoutAsync(client, second);
        var afterLogout = await AuthApi.RefreshAsync(client, second);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal("", AuthApi.RefreshCookieValue(logout));
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_cookie_still_succeeds()
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LogoutAsync(factory.CreateApiClient(), null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static Task SetActiveAsync(FacilityApiFactory factory, int userId, bool isActive) =>
        factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == userId)).IsActive = isActive;
            await db.SaveChangesAsync();
        });
}
