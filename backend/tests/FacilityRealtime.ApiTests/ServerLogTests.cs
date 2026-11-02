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

    /// <summary>facility-0103: the category sits on the message's own line, so `grep FacilityRealtime.Security` finds it.</summary>
    [Fact]
    public async Task A_security_line_prints_its_category_on_the_same_line_as_the_message()
    {
        using var factory = new FacilityApiFactory();

        await AuthApi.LoginAsync(factory.CreateApiClient(), "1000001", "0899999999");

        Assert.Contains(
            "[WRN] FacilityRealtime.Security: Login failed: employee 1000001, wrong secret" + Environment.NewLine,
            factory.Logs.Lines.Select(line => line.ConsoleText));
    }
}
