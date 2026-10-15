using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityRealtime.ApiTests.Inspections;

/// <summary>facility-0061: the record is saved before the push, so a failed push must not turn the answer into a 500.</summary>
public class LiveUpdateFailureTests
{
    private sealed class QuietProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Check-in pushes too, so every call is quiet until the test switches the fake to failing.</summary>
    private sealed class FakeClients : IHubClients
    {
        public bool Failing { get; set; }

        private IClientProxy Proxy() => Failing ? throw new InvalidOperationException("push failed") : new QuietProxy();

        public IClientProxy All => Proxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy();
        public IClientProxy Client(string connectionId) => Proxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy();
        public IClientProxy Group(string groupName) => Proxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy();
        public IClientProxy User(string userId) => Proxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy();
    }

    private sealed class FakeHubContext(FakeClients clients) : IHubContext<ScanHub>
    {
        public IHubClients Clients => clients;

        public IGroupManager Groups => throw new NotSupportedException();
    }

    [Fact]
    public async Task A_failed_push_still_answers_201_and_keeps_the_record()
    {
        var clients = new FakeClients();
        using var factory = new FacilityApiFactory
        {
            ConfigureTestServices = services => services.AddSingleton<IHubContext<ScanHub>>(new FakeHubContext(clients)),
        };
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));
        clients.Failing = true;
        var submit = new { qrToken = InspectionApi.MenRestroom, status = "Normal", latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 };

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", submit);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await factory.WithDbAsync(async db => Assert.Equal(1, await db.ScanRecords.CountAsync()));
    }
}
