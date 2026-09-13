using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Auth;

public class ProtectedEndpointTests
{
    private const string QrToken = "token-restroom-m1";

    [Theory]
    [InlineData("/api/service-points")]
    [InlineData("/api/service-points/by-token/" + QrToken)]
    public async Task Dashboard_and_point_lookup_require_login(string path)
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Any_logged_in_account_can_read_the_dashboard_and_a_point()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = await AuthApi.LoggedInAsync(factory);

        var list = await client.GetAsync("/api/service-points");
        var point = await client.GetAsync("/api/service-points/by-token/" + QrToken);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, point.StatusCode);
    }

    [Fact]
    public async Task Scanning_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().PostAsJsonAsync("/api/scan-records", new { qrToken = QrToken, status = "Normal" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scan_is_recorded_as_the_logged_in_user_even_if_the_body_names_someone_else()
    {
        using var factory = new FacilityApiFactory();
        var (client, auth, _) = await AuthApi.LoggedInAsync(factory);
        var adminId = 0;
        await factory.WithDbAsync(async db => adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id);

        // Master's contract accepted userId in the body; an old client or curl may still send it
        var response = await client.PostAsJsonAsync("/api/scan-records", new { qrToken = QrToken, userId = adminId, status = "Normal" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var recordedUserId = 0;
        await factory.WithDbAsync(async db => recordedUserId = (await db.ScanRecords.OrderByDescending(r => r.Id).FirstAsync()).UserId);
        Assert.Equal(auth.User.Id, recordedUserId);
    }

    [Fact]
    public async Task Admin_accounts_can_scan_too()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = await AuthApi.LoggedInAsync(factory, "admin", "admin1234");

        var response = await client.PostAsJsonAsync("/api/scan-records", new { qrToken = QrToken, status = "Normal" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Hub_negotiation_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().PostAsync("/hubs/scan/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hub_accepts_the_access_token_from_the_query_string()
    {
        using var factory = new FacilityApiFactory();
        var (_, auth, _) = await AuthApi.LoggedInAsync(factory);

        var response = await factory.CreateApiClient().PostAsync($"/hubs/scan/negotiate?negotiateVersion=1&access_token={auth.AccessToken}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Query_string_token_is_ignored_outside_hubs()
    {
        using var factory = new FacilityApiFactory();
        var (_, auth, _) = await AuthApi.LoggedInAsync(factory);

        var response = await factory.CreateApiClient().GetAsync($"/api/service-points?access_token={auth.AccessToken}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
