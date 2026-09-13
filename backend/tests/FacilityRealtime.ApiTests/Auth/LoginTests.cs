using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests.Auth;

public class LoginTests
{
    [Fact]
    public async Task Seeded_cleaner_logs_in_against_a_pbkdf2_hash()
    {
        using var factory = new FacilityApiFactory();
        string storedHash = "";
        await factory.WithDbAsync(async db => storedHash = (await db.Users.FindAsync(1))!.PasswordHash);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "somchai", password = "password123" });

        Assert.StartsWith("pbkdf2-sha256$", storedHash);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "somchai", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
