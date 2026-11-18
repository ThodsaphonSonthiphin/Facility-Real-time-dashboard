using System.Net;
using System.Text.Json;
using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §12: live scheduling views are refreshed by a scoped invalidation, not by data. The push goes only to accounts that
/// are active and authorised at send time, it carries the command identity alone, a read is always the source of truth
/// when a push is missed, and a push that fails cannot undo a committed command.
/// </summary>
public class ScheduleLiveTests
{
    /// <summary>The command payload every scenario uses: one person's day off, edited by the responsible Supervisor.</summary>
    private static object Payload(int personId) => new
    {
        weekStart = ScheduleWriteApi.Iso(ScheduleWriteApi.Week),
        rows = new[] { new { userId = personId, offDates = new[] { "2026-11-18" } } },
    };

    private static TaskCompletionSource<JsonElement> Listen(HubConnection connection)
    {
        var got = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(ScheduleNotifications.EventName, payload => got.TrySetResult(payload));
        return got;
    }

    [Fact]
    public async Task A_committed_command_invalidates_only_active_authorised_accounts()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddSupervisorAsync(factory, "2000002", "0820000002", "B", Shift.Day);
        await TestData.AddSupervisorScopeAsync(factory, "2000002", "B", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddSupervisorAsync(factory, "2000003", "0820000003", "A", Shift.Night);
        await TestData.AddSupervisorScopeAsync(factory, "2000003", "A", Shift.Night, new DateOnly(2026, 10, 1));
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");

        // Log in before moving the business clock: JwtBearer validates against the real clock.
        var (owner, ownerAuth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var (_, otherAuth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000002", "0820000002");
        var (_, removedAuth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000003", "0820000003");
        var (_, cleanerAuth, _) = await AuthApi.LoggedInAsync(factory);
        ScheduleWriteApi.Today(factory);

        await using var ownerHub = await HubClient.ConnectAsync(factory, ownerAuth.AccessToken);
        await using var otherHub = await HubClient.ConnectAsync(factory, otherAuth.AccessToken);
        await using var removedHub = await HubClient.ConnectAsync(factory, removedAuth.AccessToken);
        await using var cleanerHub = await HubClient.ConnectAsync(factory, cleanerAuth.AccessToken);

        var ownerGot = Listen(ownerHub);
        var otherGot = Listen(otherHub);
        var removedGot = Listen(removedHub);
        var cleanerGot = Listen(cleanerHub);

        // The third Supervisor loses the account after the page opened: the push resolves recipients again, so the
        // still-open connection must receive nothing.
        await factory.WithDbAsync(db => db.Users
            .Where(user => user.EmployeeId == "2000003")
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsActive, false)));
        await Task.Delay(200);

        var (response, answer) = await ScheduleWriteApi.ExecuteAsync(
            owner, ScheduleWriteApi.SetWeeklyDaysOff, Payload(person));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer!.Outcome);
        var ownerPayload = await ownerGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cleanerGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(500);
        Assert.False(otherGot.Task.IsCompleted);
        Assert.False(removedGot.Task.IsCompleted);

        // Identity only: no person, building or schedule value can be read out of the broadcast.
        var field = Assert.Single(ownerPayload.EnumerateObject());
        Assert.Equal("commandid", field.Name.ToLowerInvariant());
        Assert.Equal(answer.CommandId, field.Value.GetGuid());
    }

    [Fact]
    public async Task A_missed_push_is_recovered_by_the_next_authorised_read()
    {
        using var factory = NewFactory(services =>
            services.AddSingleton<IScheduleCommandNotifier, NoOpScheduleCommandNotifier>());
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, auth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        // The page's live connection drops before the save; the save still commits, and opening the week again answers
        // from the stored facts rather than from anything the dropped push might have carried.
        var dropped = await HubClient.ConnectAsync(factory, auth.AccessToken);
        await dropped.StopAsync();
        await dropped.DisposeAsync();

        var (response, answer) = await ScheduleWriteApi.ExecuteAsync(
            supervisor, ScheduleWriteApi.SetWeeklyDaysOff, Payload(person));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer!.Outcome);

        var week = await ScheduleWriteApi.WeekAsync(supervisor, ScheduleWriteApi.Week);
        var row = Assert.Single(week.Rows, entry => entry.UserId == person);
        var day = Assert.Single(row.Days, entry => entry.Date == new DateOnly(2026, 11, 18));
        Assert.True(day.IsDayOff);
        Assert.False(day.IsUnset);
    }

    [Fact]
    public async Task A_push_that_fails_leaves_the_committed_command_and_its_facts_intact()
    {
        using var factory = NewFactory(services =>
            services.AddSingleton<IHubContext<ScanHub>>(new UnreachableHub()));
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var (response, answer) = await ScheduleWriteApi.ExecuteAsync(
            supervisor, ScheduleWriteApi.SetWeeklyDaysOff, Payload(person));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer!.Outcome);
        await factory.WithDbAsync(async db =>
        {
            var week = await db.WeeklyDayOffs.SingleAsync(header => header.UserId == person);
            Assert.Equal(new DateOnly(2026, 11, 16), week.WeekStart);
            Assert.Single(await db.ScheduleCommandReceipts.ToListAsync());
        });
    }

    private static JanitorialApiFactory NewFactory(Action<IServiceCollection>? extra = null) => new()
    {
        ConfigureTestServices = services =>
        {
            services.AddSingleton(new SchedulingSettings(ScheduleWriteApi.TestKey)
            {
                Enabled = true,
                HistoryStartsAt = new DateOnly(2026, 10, 1),
            });
            extra?.Invoke(services);
        },
    };

    /// <summary>A hub whose every send fails, standing in for a dropped or broken live connection.</summary>
    private sealed class UnreachableHub : IHubContext<ScanHub>
    {
        public IHubClients Clients { get; } = new UnreachableClients();

        public IGroupManager Groups { get; } = new UnreachableGroups();
    }

    private sealed class UnreachableClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new UnreachableProxy();

        public IClientProxy All => Proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Client(string connectionId) => Proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;

        public IClientProxy Group(string groupName) => Proxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;

        public IClientProxy User(string userId) => Proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class UnreachableProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("the live connection is not reachable"));
    }

    private sealed class UnreachableGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
