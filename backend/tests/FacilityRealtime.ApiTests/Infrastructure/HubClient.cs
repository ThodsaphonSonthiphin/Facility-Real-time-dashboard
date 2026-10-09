using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace FacilityRealtime.ApiTests.Infrastructure;

public static class HubClient
{
    /// <summary>Connects to /hubs/scan inside the test server. Long polling, because the in-memory server has no WebSockets.</summary>
    public static async Task<HubConnection> ConnectAsync(FacilityApiFactory factory, string accessToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/scan"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }
}
