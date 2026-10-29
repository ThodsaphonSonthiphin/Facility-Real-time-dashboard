using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests;

/// <summary>facility-0095: the Server Log holds security events and server errors, not every SQL statement.</summary>
public class ServerLogTests
{
    [Fact]
    public async Task A_login_writes_no_SQL_statement_to_the_log()
    {
        using var factory = new FacilityApiFactory();

        await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.DoesNotContain(factory.Logs.Lines, line => line.Category == "Microsoft.EntityFrameworkCore.Database.Command");
    }
}
