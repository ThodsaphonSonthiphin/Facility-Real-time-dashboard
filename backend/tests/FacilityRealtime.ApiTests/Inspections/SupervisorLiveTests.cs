using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;

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
        var (_, dayA, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2001", "0820000001");
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
