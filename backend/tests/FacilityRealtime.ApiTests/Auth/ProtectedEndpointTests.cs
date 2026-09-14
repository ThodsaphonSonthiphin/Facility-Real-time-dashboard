using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FacilityRealtime.ApiTests.Auth;

public class ProtectedEndpointTests
{
    private const string QrToken = "token-restroom-m1";

    // A throwaway literal for the "signed with a different key" test below; not a real secret.
    private const string DifferentSigningKey = "different-throwaway-signing-key-0123456789-abcdefghijklmnop";

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

    [Fact]
    public async Task Token_signed_with_a_different_key_is_rejected()
    {
        using var factory = new FacilityApiFactory();
        var settings = factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var user = await SeededUserAsync(factory);
        var now = DateTime.UtcNow;
        var forgedSettings = new JwtSettings
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            SigningKey = DifferentSigningKey,
        };
        var token = BuildAccessToken(forgedSettings, now, now.AddMinutes(settings.AccessTokenMinutes), user);
        var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/service-points");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(20, HttpStatusCode.OK)] // inside the 30-second ClockSkew (ADR facility-0012)
    [InlineData(40, HttpStatusCode.Unauthorized)] // outside it
    public async Task Expired_token_is_accepted_only_within_the_clock_skew(int secondsPastExpiry, HttpStatusCode expected)
    {
        using var factory = new FacilityApiFactory();
        var settings = factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var user = await SeededUserAsync(factory);
        var expires = DateTime.UtcNow.AddSeconds(-secondsPastExpiry);
        var token = BuildAccessToken(settings, expires.AddMinutes(-5), expires, user);
        var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/service-points");

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Mirrors JwtAccessTokenIssuer exactly, so a crafted token differs from a real one only in what the test varies.</summary>
    private static string BuildAccessToken(JwtSettings settings, DateTime issuedAt, DateTime expires, User user) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(AuthClaims.UserId, user.Id.ToString()),
                new Claim(AuthClaims.Username, user.Username),
                new Claim(AuthClaims.Name, user.FullName),
                new Claim(AuthClaims.Role, user.Role),
            }),
            SigningCredentials = new SigningCredentials(JwtKeys.SigningKey(settings), SecurityAlgorithms.HmacSha256),
        });

    private static async Task<User> SeededUserAsync(FacilityApiFactory factory, string username = "somchai")
    {
        User? user = null;
        await factory.WithDbAsync(async db => user = await db.Users.SingleAsync(u => u.Username == username));
        return user!;
    }
}
