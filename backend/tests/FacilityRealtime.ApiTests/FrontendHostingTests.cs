using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests;

/// <summary>facility-0067: the API serves the built frontend from its own origin, and never in place of an API answer.</summary>
public sealed class FrontendHostingTests : IDisposable
{
    private const string IndexMarker = "<!-- facility frontend index -->";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("facility-wwwroot-").FullName;

    public FrontendHostingTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), $"<!doctype html>{IndexMarker}<div id=\"root\"></div>");
        File.WriteAllText(Path.Combine(_webRoot, "favicon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('app');");
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/scan/0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f")]
    [InlineData("/admin/accounts")]
    public async Task Page_paths_get_the_app(string path)
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(IndexMarker, await response.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Built_assets_are_served_and_cached_for_good()
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync("/assets/index-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task A_file_outside_assets_is_revalidated_not_cached_for_good()
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync("/favicon.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("/api/no-such-endpoint")]
    [InlineData("/api")]
    [InlineData("/hubs/no-such-hub")]
    [InlineData("/assets/missing.js")]
    public async Task Api_paths_and_missing_files_are_404_not_the_app(string path)
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(IndexMarker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_wrong_method_on_an_api_route_is_still_405()
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().DeleteAsync("/api/admin/audit-log");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task A_page_path_that_starts_with_api_letters_still_gets_the_app()
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync("/apiary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(IndexMarker, await response.Content.ReadAsStringAsync());
    }
}
