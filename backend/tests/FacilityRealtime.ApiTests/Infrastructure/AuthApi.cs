using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FacilityRealtime.ApiTests.Infrastructure;

public record AuthUserModel(int Id, string Username, string FullName, string Role);

public record AuthResponseModel(string AccessToken, DateTime ExpiresAt, AuthUserModel User);

public static class AuthApi
{
    public const string CookieName = "facility_refresh";

    /// <summary>Cookies are handled by hand so a test can replay an old refresh token on purpose.</summary>
    public static HttpClient CreateApiClient(this FacilityApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username = "somchai", string password = "password123") =>
        client.PostAsJsonAsync("/api/auth/login", new { username, password });

    public static async Task<AuthResponseModel> ReadAuthAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AuthResponseModel>())!;

    /// <summary>The refresh cookie's Set-Cookie header, or null when the response did not touch the cookie.</summary>
    public static string? RefreshSetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.LastOrDefault(v => v.StartsWith(CookieName + "="))
            : null;

    /// <summary>The refresh token; "" when the response deleted the cookie; null when it did not touch it.</summary>
    public static string? RefreshCookieValue(HttpResponseMessage response) =>
        RefreshSetCookieHeader(response)?.Split(';')[0][(CookieName.Length + 1)..];

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string? refreshToken) =>
        PostWithCookieAsync(client, "/api/auth/refresh", refreshToken);

    public static Task<HttpResponseMessage> LogoutAsync(HttpClient client, string? refreshToken) =>
        PostWithCookieAsync(client, "/api/auth/logout", refreshToken);

    /// <summary>Logs in and returns a client that already sends the access token.</summary>
    public static async Task<(HttpClient Client, AuthResponseModel Auth, string RefreshToken)> LoggedInAsync(
        FacilityApiFactory factory, string username = "somchai", string password = "password123")
    {
        var client = factory.CreateApiClient();
        var response = await LoginAsync(client, username, password);
        response.EnsureSuccessStatusCode();
        var auth = await ReadAuthAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth, RefreshCookieValue(response)!);
    }

    private static Task<HttpResponseMessage> PostWithCookieAsync(HttpClient client, string path, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refreshToken is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={refreshToken}");
        }

        return client.SendAsync(request);
    }
}
