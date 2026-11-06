using System.Net;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests;

public class HarnessTests
{
    [Fact]
    public async Task Api_boots_on_sqlite_and_answers_the_healthcheck()
    {
        using var factory = new JanitorialApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    public async Task Api_documentation_is_not_served_outside_development(string path)
    {
        using var factory = new JanitorialApiFactory();

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // facility-0049: the server is on the internet
    }

    [Fact]
    public async Task Seed_runs_against_the_test_database()
    {
        using var factory = new JanitorialApiFactory();
        var counts = Array.Empty<int>();

        await factory.WithDbAsync(async db => counts = new[]
        {
            await db.Buildings.CountAsync(),
            await db.Areas.CountAsync(),
            await db.ServicePoints.CountAsync(),
            await db.Signs.CountAsync(),
            await db.PointRoundWindows.CountAsync(),
            await db.Users.CountAsync(),
        });

        // buildings, areas, points, signs (3 point + 2 check-in), round windows, users
        Assert.Equal(new[] { 1, 2, 3, 5, 9, 5 }, counts);
    }
}
