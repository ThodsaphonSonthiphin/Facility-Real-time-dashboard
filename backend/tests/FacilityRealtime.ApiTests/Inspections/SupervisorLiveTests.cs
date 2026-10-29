using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Inspections;

/// <summary>facility-0057, 0058 rule 3: a point change reaches the Admins and that building's Supervisors, nobody else.</summary>
public class SupervisorLiveTests
{
    private static readonly object Submit = new
    {
        qrToken = InspectionApi.MenRestroom, status = "Normal", latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
    };

    private static TaskCompletionSource<JsonElement> Listen(HubConnection hub, string eventName)
    {
        var got = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<JsonElement>(eventName, point => got.TrySetResult(point));
        return got;
    }

    [Fact]
    public async Task The_buildings_supervisors_get_a_submission_live_and_others_do_not()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        await TestData.AddSupervisorAsync(factory, "S2003", "0820000003", "B", Shift.Day);
        var (_, dayA, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var (_, nightA, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2002", "0820000002");
        var (_, dayB, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2003", "0820000003");
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await using var dayAHub = await HubClient.ConnectAsync(factory, dayA.AccessToken);
        await using var nightAHub = await HubClient.ConnectAsync(factory, nightA.AccessToken);
        await using var dayBHub = await HubClient.ConnectAsync(factory, dayB.AccessToken);
        var dayAGot = Listen(dayAHub, "ScanRecorded");
        var nightAGot = Listen(nightAHub, "ScanRecorded");
        var dayBGot = Listen(dayBHub, "ScanRecorded");
        await Task.Delay(200);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Submit);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await dayAGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("PendingInspection", payload.GetProperty("status").GetString());
        await nightAGot.Task.WaitAsync(TimeSpan.FromSeconds(5)); // both shifts' Supervisors of building A
        await Task.Delay(500);
        Assert.False(dayBGot.Task.IsCompleted);
    }

    [Fact]
    public async Task A_supervisor_covering_the_building_gets_it_live()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddSupervisorAsync(factory, "S2003", "0820000003", "B", Shift.Day);
        await TestData.AddSupervisorCoverAsync(factory, "S2003", "A", new DateOnly(2026, 10, 8), Shift.Day);
        var (_, dayB, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2003", "0820000003");
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await using var hub = await HubClient.ConnectAsync(factory, dayB.AccessToken);
        var got = Listen(hub, "ScanRecorded");
        await Task.Delay(200);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        await cleaner.PostAsJsonAsync("/api/scan-records", Submit);

        await got.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>Each building-shift holds one Supervisor, so the isolated conditions are spread over two tests.</summary>
    [Fact]
    public async Task A_cancelled_cover_another_shifts_cover_and_a_deactivated_supervisor_get_nothing()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var day = new DateOnly(2026, 10, 8);
        await TestData.AddSupervisorAsync(factory, "S2010", "0820000010", "B", Shift.Day);
        await TestData.AddSupervisorAsync(factory, "S2011", "0820000011", "B", Shift.Night);
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        await TestData.AddSupervisorCoverAsync(factory, "S2010", "A", day, Shift.Day, cancelled: true);
        await TestData.AddSupervisorCoverAsync(factory, "S2011", "A", day, Shift.Night);

        await AssertOnlyTheControlGetsItAsync(factory, new[] { ("S2010", "0820000010"), ("S2011", "0820000011"), ("S2002", "0820000002") }, deactivate: "S2002");
    }

    [Fact]
    public async Task A_deactivated_supervisor_covering_the_building_gets_nothing()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddSupervisorAsync(factory, "S2012", "0820000012", "B", Shift.Day);
        await TestData.AddSupervisorCoverAsync(factory, "S2012", "A", new DateOnly(2026, 10, 8), Shift.Day);

        await AssertOnlyTheControlGetsItAsync(factory, new[] { ("S2012", "0820000012") }, deactivate: "S2012");
    }

    private static async Task AssertOnlyTheControlGetsItAsync(FacilityApiFactory factory, (string Id, string Phone)[] outsiders, string deactivate)
    {
        var (_, control, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var tokens = new List<(string Id, string Token)>();
        foreach (var (id, phone) in outsiders)
        {
            tokens.Add((id, (await AuthApi.LoggedInAsEmployeeAsync(factory, id, phone)).Item2.AccessToken));
        }

        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await using var controlHub = await HubClient.ConnectAsync(factory, control.AccessToken);
        var controlGot = Listen(controlHub, "ScanRecorded");
        var hubs = new List<HubConnection>();
        var gots = new Dictionary<string, TaskCompletionSource<JsonElement>>();
        try
        {
            foreach (var (id, token) in tokens)
            {
                var hub = await HubClient.ConnectAsync(factory, token);
                hubs.Add(hub);
                gots[id] = Listen(hub, "ScanRecorded");
            }

            await Task.Delay(200);
            await factory.WithDbAsync(async db =>
            {
                (await db.Users.SingleAsync(u => u.EmployeeId == deactivate)).IsActive = false;
                await db.SaveChangesAsync();
            });
            await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
            factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

            await cleaner.PostAsJsonAsync("/api/scan-records", Submit);

            await controlGot.Task.WaitAsync(TimeSpan.FromSeconds(5)); // the send works
            await Task.Delay(500);
            foreach (var (id, got) in gots)
            {
                Assert.False(got.Task.IsCompleted, $"{id} must receive nothing");
            }
        }
        finally
        {
            foreach (var hub in hubs)
            {
                await hub.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task The_buildings_supervisors_get_an_inspection_live()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (_, nightA, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2002", "0820000002");
        await using var hub = await HubClient.ConnectAsync(factory, nightA.AccessToken);
        var got = Listen(hub, "InspectionRecorded");
        await Task.Delay(200);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        Assert.Equal("Passed", (await got.Task.WaitAsync(TimeSpan.FromSeconds(5))).GetProperty("status").GetString());
    }
}
