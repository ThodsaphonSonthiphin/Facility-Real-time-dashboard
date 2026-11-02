using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests;

/// <summary>facility-0106: the app sends the browser's security headers itself, so they are kept by these tests.</summary>
public sealed class SecurityHeaderTests : IDisposable
{
    private const string Csp =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; " +
        "base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("facility-wwwroot-").FullName;

    public SecurityHeaderTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><div id=\"root\"></div>");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('app');");
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(", ", values) : null;

    [Theory]
    [InlineData("/")]
    [InlineData("/login/admin")]
    [InlineData("/assets/index-abc123.js")]
    [InlineData("/api/health")]
    public async Task Every_answer_carries_the_security_headers(string path)
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Csp, Header(response, "Content-Security-Policy"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal("geolocation=(self), camera=(), microphone=()", Header(response, "Permissions-Policy"));
    }

    /// <summary>facility-0106 decision 5 (F7): login and refresh answers carry an access token, so no API answer is kept.</summary>
    [Fact]
    public async Task Api_answers_are_never_stored_by_the_browser()
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };
        var client = factory.CreateApiClient();

        var health = await client.GetAsync("/api/health");
        var login = await AuthApi.LoginAsync(client);

        Assert.Equal("no-store", health.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("no-store", login.Headers.CacheControl?.ToString());
    }

    /// <summary>
    /// facility-0106 decisions 2 and 3: HSTS goes only on an answer that went out over https, which the app learns from
    /// X-Forwarded-Proto believed only from the proxy on this machine (Caddy). Off on a developer machine.
    /// </summary>
    [Theory]
    [InlineData("127.0.0.1", "https", "max-age=31536000")]
    [InlineData("127.0.0.1", "http", null)]
    [InlineData("192.0.2.50", "https", null)] // not the local proxy: the header is not believed
    public async Task Hsts_is_sent_only_over_https_through_the_local_proxy(string peer, string forwardedProto, string? expected)
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };
        var client = factory.CreateApiClientFrom(peer);
        client.DefaultRequestHeaders.Host = "facility.example.com";
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", forwardedProto);

        var response = await client.GetAsync("/login/admin");

        Assert.Equal(expected, Header(response, "Strict-Transport-Security"));
    }

    /// <summary>
    /// facility-0106 decision 4: only Development's Swagger page goes without the CSP. Anywhere else a /swagger path is
    /// just a page path and gets the app, which must keep its CSP, or the path would be a way around it.
    /// </summary>
    [Fact]
    public async Task Outside_development_a_swagger_path_keeps_the_content_security_policy()
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync("/swagger/anything");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // the app, through the page fallback
        Assert.Equal(Csp, Header(response, "Content-Security-Policy"));
    }
}
