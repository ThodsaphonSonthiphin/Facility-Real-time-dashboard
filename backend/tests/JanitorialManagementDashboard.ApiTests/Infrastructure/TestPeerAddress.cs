using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace JanitorialManagementDashboard.ApiTests.Infrastructure;

/// <summary>
/// The in-memory test server gives a request no peer address. This filter runs before Program's pipeline and sets the
/// peer from a test-only header, as a real connection would, so the Login Lock (facility-0104) can be tested per place.
/// </summary>
public sealed class TestPeerAddress : IStartupFilter
{
    public const string Header = "X-Test-Peer-Address";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(Header, out var value))
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(value.ToString());
                context.Request.Headers.Remove(Header);
            }

            return nextMiddleware(context);
        });
        next(app);
    };
}
