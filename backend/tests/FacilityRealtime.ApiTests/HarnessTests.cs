using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests;

public class HarnessTests
{
    [Fact]
    public async Task Api_boots_on_sqlite_and_answers_the_healthcheck()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Seed_runs_against_the_test_database()
    {
        using var factory = new FacilityApiFactory();
        var pointCount = 0;
        var userCount = 0;

        await factory.WithDbAsync(async db =>
        {
            pointCount = await db.ServicePoints.CountAsync();
            userCount = await db.Users.CountAsync();
        });

        Assert.Equal(3, pointCount);
        Assert.Equal(2, userCount);
    }
}
