using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Inspections;
using JanitorialManagementDashboard.ApiTests.Scheduling;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Admin;

public class OverviewLiveTests
{
    private const string TargetEvent = "OverviewChanged";

    [Fact]
    public async Task A_committed_target_change_reaches_only_active_Admins_with_the_new_revision_id()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var other = await AccountApi.ReadAsync(await AccountApi.CreateAdminAsync(admin, "admin2", "temporary-pass-2"));
        var signIn = await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", "temporary-pass-2");
        signIn.EnsureSuccessStatusCode();
        var otherAuth = await AuthApi.ReadAuthAsync(signIn);
        var (_, cleanerAuth, _) = await AuthApi.LoggedInAsync(factory);
        var (_, supervisorAuth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        await using var adminHub = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        await using var otherHub = await HubClient.ConnectAsync(factory, otherAuth.AccessToken);
        await using var cleanerHub = await HubClient.ConnectAsync(factory, cleanerAuth.AccessToken);
        await using var supervisorHub = await HubClient.ConnectAsync(factory, supervisorAuth.AccessToken);
        var adminEvents = Listen(adminHub, TargetEvent);
        var otherEvents = Listen(otherHub, TargetEvent);
        var cleanerEvents = Listen(cleanerHub, TargetEvent);
        var supervisorEvents = Listen(supervisorHub, TargetEvent);
        var before = await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 6, 0));

        var building = await AdminSetupApi.BuildingIdAsync(factory);
        var area = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var point = await AdminSetupApi.PointIdAsync(factory, "AR01-01");
        using var renamed = await admin.PutAsJsonAsync($"/api/admin/buildings/{building}", new { name = "Renamed building" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var buildingRevision = await AssertCurrentRevisionAsync(factory, adminEvents, before);
        Assert.Equal(buildingRevision, ReadRevisionId(await NextAsync(otherEvents)));

        using var deactivated = await admin.PostAsync($"{AccountApi.Path}/{other.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        using var savedArea = await admin.PutAsJsonAsync($"/api/admin/areas/{area}", new
        {
            name = "Renamed area", buildingId = building, shiftPattern = "DayAndNight",
        });
        Assert.Equal(HttpStatusCode.OK, savedArea.StatusCode);
        var areaRevision = await AssertCurrentRevisionAsync(factory, adminEvents, buildingRevision);

        Task<HttpResponseMessage> SavePoint() => admin.PutAsJsonAsync($"/api/admin/points/{point}", new
        {
            name = "Renamed point", roundWindows = new[] { new { shift = "Day", start = "10:00", end = "11:00" } },
        });
        using var savedPoint = await SavePoint();
        Assert.Equal(HttpStatusCode.OK, savedPoint.StatusCode);
        var pointRevision = await AssertCurrentRevisionAsync(factory, adminEvents, areaRevision);

        using var samePoint = await SavePoint();
        Assert.Equal(HttpStatusCode.OK, samePoint.StatusCode);
        var cleaner = await AdminSetupApi.UserIdAsync(factory, "1000001");
        using var assigned = await admin.PutAsJsonAsync($"/api/admin/areas/{area}", new
        {
            name = "Renamed area", buildingId = building, shiftPattern = "DayAndNight", dayCleanerId = cleaner,
        });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.Equal(pointRevision, await HeadAsync(factory));
        await Task.WhenAll(AssertQuietAsync(adminEvents), AssertQuietAsync(otherEvents),
            AssertQuietAsync(cleanerEvents), AssertQuietAsync(supervisorEvents));
    }

    [Fact]
    public async Task A_failed_target_capture_rolls_back_and_sends_nothing()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, auth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await using var connection = await HubClient.ConnectAsync(factory, auth.AccessToken);
        var events = Listen(connection, TargetEvent);
        var now = ThaiClock.At(8, 9, 0);
        var original = await OverviewTestData.StartHistoryAsync(factory, now);
        var building = await AdminSetupApi.BuildingIdAsync(factory);
        Task<HttpResponseMessage> Rename() => admin.PutAsJsonAsync(
            $"/api/admin/buildings/{building}", new { name = "Renamed building" });

        factory.Clock.SetUtcNow(now.AddSeconds(-1));
        using var failed = await Rename();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(original, await HeadAsync(factory));
        await AssertQuietAsync(events);

        factory.Clock.SetUtcNow(now);
        using var retry = await Rename();
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await AssertCurrentRevisionAsync(factory, events, original);
    }

    [Fact]
    public async Task A_failed_target_push_keeps_the_committed_setup_and_revision()
    {
        var clients = new LiveUpdateFailureTests.FakeClients { Failing = true };
        using var factory = new JanitorialApiFactory
        {
            ConfigureTestServices = services =>
                services.AddSingleton<IHubContext<ScanHub>>(new LiveUpdateFailureTests.FakeHubContext(clients)),
        };
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var before = await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 6, 0));
        var building = await AdminSetupApi.BuildingIdAsync(factory);

        using var saved = await admin.PutAsJsonAsync($"/api/admin/buildings/{building}", new { name = "Renamed building" });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("Renamed building", await AdminSetupApi.ReadAsync(factory,
            db => db.Buildings.Where(row => row.Id == building).Select(row => row.Name).SingleAsync()));
        var revision = await HeadAsync(factory);
        Assert.True(revision > before);
        Assert.Contains(factory.Logs.Lines, line =>
            line.Message.Contains($"Overview revision {revision} is saved; notification failed"));
    }

    [Fact]
    public async Task A_transfer_instruction_does_not_invalidate_entry_but_an_accepted_arrival_does()
    {
        using var factory = TransferApi.NewFactory();
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, cleanerAuth, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await using var connection = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        var attendanceEvents = Listen(connection, "AttendanceRecorded");
        var scheduleEvents = Listen(connection, ScheduleNotifications.EventName);
        var targetEvents = Listen(connection, TargetEvent);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        await NextAsync(attendanceEvents);

        var instruction = await TransferApi.InstructForIdAsync(factory, admin, cleanerAuth.User.Id, origin,
            destination, TransferApi.Thai(13, 0));
        await NextAsync(scheduleEvents);
        await Task.WhenAll(AssertQuietAsync(attendanceEvents), AssertQuietAsync(targetEvents));

        using var confirmed = await TransferApi.ConfirmAsync(factory, cleaner, instruction, "1", at: TransferApi.Thai(13, 5));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var arrival = await NextAsync(attendanceEvents);
        Assert.Equal(destination, arrival.GetProperty("areaId").GetInt32());
        Assert.Equal("Day", arrival.GetProperty("shift").GetString());
        await Task.WhenAll(NextAsync(scheduleEvents), AssertQuietAsync(targetEvents));
    }

    private static ChannelReader<JsonElement> Listen(HubConnection hub, string name)
    {
        var events = Channel.CreateUnbounded<JsonElement>();
        hub.On<JsonElement>(name, payload => { events.Writer.TryWrite(payload); });
        return events.Reader;
    }

    private static async Task<JsonElement> NextAsync(ChannelReader<JsonElement> events)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await events.ReadAsync(timeout.Token);
    }

    private static async Task AssertQuietAsync(ChannelReader<JsonElement> events)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => events.ReadAsync(timeout.Token).AsTask());
    }

    private static Task<long?> HeadAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory, db => db.OverviewTargetStates.Select(state => state.LatestRevisionId).SingleAsync());

    private static long ReadRevisionId(JsonElement payload)
    {
        var field = Assert.Single(payload.EnumerateObject());
        Assert.Equal("revisionId", field.Name);
        return field.Value.GetInt64();
    }

    private static async Task<long> AssertCurrentRevisionAsync(JanitorialApiFactory factory,
        ChannelReader<JsonElement> events, long previous)
    {
        var revision = ReadRevisionId(await NextAsync(events));
        Assert.True(revision > previous);
        Assert.Equal(revision, await HeadAsync(factory));
        return revision;
    }
}
