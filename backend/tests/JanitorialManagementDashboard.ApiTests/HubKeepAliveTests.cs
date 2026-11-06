using JanitorialManagementDashboard.ApiTests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JanitorialManagementDashboard.ApiTests;

/// <summary>
/// facility-0107: an Admin page counts the link lost after 10 s without a word from the server, so the server must
/// speak at least twice in that time. The server's own wait for a silent client is left at SignalR's 30 s default.
/// </summary>
public sealed class HubKeepAliveTests
{
    [Fact]
    public void The_hub_pings_every_5_s_and_leaves_its_wait_for_a_silent_client_alone()
    {
        using var factory = new JanitorialApiFactory();

        var options = factory.Services.GetRequiredService<IOptions<HubOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(5), options.KeepAliveInterval);
        Assert.Null(options.ClientTimeoutInterval); // not set: SignalR's own 30 s
    }
}
