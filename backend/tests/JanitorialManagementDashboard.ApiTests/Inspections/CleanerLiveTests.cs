using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Inspections;

/// <summary>
/// Owner 2026-10-26, amending facility-0052 and 0058: a point change also reaches the Cleaners of the point's Area (both
/// shifts) and any Cleaner covering that Area in the clock's shift, so My Work shows an inspection without a press.
/// </summary>
public class CleanerLiveTests
{
    private static TaskCompletionSource<JsonElement> Listen(HubConnection hub, string eventName)
    {
        var got = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<JsonElement>(eventName, point => got.TrySetResult(point));
        return got;
    }

    [Fact]
    public async Task An_inspection_reaches_both_shifts_Cleaners_of_the_area_and_not_another_areas_Cleaner()
    {
        using var factory = new JanitorialApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (_, day, _) = await AuthApi.LoggedInAsync(factory); // 1000001, AR01 day
        var (_, night, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000002", "0810000002"); // AR01 night
        var (_, other, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000003", "0810000003"); // AR02 day
        await using var dayHub = await HubClient.ConnectAsync(factory, day.AccessToken);
        await using var nightHub = await HubClient.ConnectAsync(factory, night.AccessToken);
        await using var otherHub = await HubClient.ConnectAsync(factory, other.AccessToken);
        var dayGot = Listen(dayHub, "InspectionRecorded");
        var nightGot = Listen(nightHub, "InspectionRecorded");
        var otherGot = Listen(otherHub, "InspectionRecorded");
        await Task.Delay(200);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        Assert.Equal("Passed", (await dayGot.Task.WaitAsync(TimeSpan.FromSeconds(5))).GetProperty("status").GetString());
        await nightGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(500);
        Assert.False(otherGot.Task.IsCompleted, "AR02's Cleaner must receive nothing about AR01's point");
    }

    [Fact]
    public async Task A_Cleaner_covering_the_area_gets_it_but_a_cancelled_cover_and_a_deactivated_Cleaner_do_not()
    {
        using var factory = new JanitorialApiFactory();
        var shiftDate = new DateOnly(2026, 10, 8);
        foreach (var id in new[] { "E3001", "E3002", "E3003" })
        {
            await TestData.AddCleanerAsync(factory, id, Shift.Day, phone: "0830000000");
        }

        await TestData.AddCoverAsync(factory, "E3001", "AR01", shiftDate, Shift.Day);
        await TestData.AddCoverAsync(factory, "E3002", "AR01", shiftDate, Shift.Day, cancelled: true);
        await TestData.AddCoverAsync(factory, "E3003", "AR01", shiftDate, Shift.Day);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (_, control, _) = await AuthApi.LoggedInAsync(factory);
        await using var controlHub = await HubClient.ConnectAsync(factory, control.AccessToken);
        var controlGot = Listen(controlHub, "InspectionRecorded");
        var covering = (await AuthApi.LoggedInAsEmployeeAsync(factory, "E3001", "0830000000")).Auth;
        var cancelled = (await AuthApi.LoggedInAsEmployeeAsync(factory, "E3002", "0830000000")).Auth;
        var deactivated = (await AuthApi.LoggedInAsEmployeeAsync(factory, "E3003", "0830000000")).Auth;
        await using var coveringHub = await HubClient.ConnectAsync(factory, covering.AccessToken);
        await using var cancelledHub = await HubClient.ConnectAsync(factory, cancelled.AccessToken);
        await using var deactivatedHub = await HubClient.ConnectAsync(factory, deactivated.AccessToken);
        var coveringGot = Listen(coveringHub, "InspectionRecorded");
        var cancelledGot = Listen(cancelledHub, "InspectionRecorded");
        var deactivatedGot = Listen(deactivatedHub, "InspectionRecorded");
        await Task.Delay(200);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "E3003")).IsActive = false;
            await db.SaveChangesAsync();
        });
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        await controlGot.Task.WaitAsync(TimeSpan.FromSeconds(5)); // the send works
        await coveringGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(500);
        Assert.False(cancelledGot.Task.IsCompleted, "a cancelled cover must receive nothing");
        Assert.False(deactivatedGot.Task.IsCompleted, "a deactivated Cleaner must receive nothing");
    }

    [Fact]
    public async Task An_inspection_reaches_active_relief_and_confirmed_transferred_cleaners_of_the_area()
    {
        using var factory = new JanitorialApiFactory();
        var (_, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var shiftDate = new DateOnly(2026, 10, 8);
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var adminId = adminAuth.User.Id;
        var reliefId = await TestData.AddCleanerAsync(factory, "E3010", Shift.Day, phone: "0830000010");
        var transferredId = await AdminSetupApi.UserIdAsync(factory, "1000003"); // regular AR02

        await factory.WithDbAsync(async db =>
        {
            var ar01SignId = await db.Signs.Where(s => s.Code == "AR01-IN").Select(s => s.Id).SingleAsync();
            db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
            {
                UserId = reliefId, Kind = CleanerKind.Relief, HomeBuildingId = buildingA, Shift = Shift.Day,
                EffectiveFrom = shiftDate.AddDays(-1), Revision = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            db.ReliefAssignments.Add(new ReliefAssignment
            {
                UserId = reliefId, ShiftDate = shiftDate, Shift = Shift.Day, AreaId = ar01,
                SourceBuildingId = buildingA, BreakGroup = 1,
                CreatedById = adminId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            var transfer = new AreaTransfer
            {
                PersonId = transferredId, ShiftDate = shiftDate, Shift = Shift.Day,
                FromAreaId = ar02, ToAreaId = ar01, EffectiveAt = ThaiClock.At(8, 8, 0).UtcDateTime,
                CreatedById = adminId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.AreaTransfers.Add(transfer);
            await db.SaveChangesAsync();
            db.AreaTransferConfirmations.Add(new AreaTransferConfirmation
            {
                TransferId = transfer.Id, ConfirmedById = transferredId, AreaId = ar01, SignId = ar01SignId,
                OccurredAt = ThaiClock.At(8, 8, 5).UtcDateTime, Source = AttendanceSource.Scan, CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var reliefAuth = (await AuthApi.LoggedInAsEmployeeAsync(factory, "E3010", "0830000010")).Auth;
        var transferredAuth = (await AuthApi.LoggedInAsEmployeeAsync(factory, "1000003", "0810000003")).Auth;
        await using var reliefHub = await HubClient.ConnectAsync(factory, reliefAuth.AccessToken);
        await using var transferredHub = await HubClient.ConnectAsync(factory, transferredAuth.AccessToken);
        var reliefGot = Listen(reliefHub, "InspectionRecorded");
        var transferredGot = Listen(transferredHub, "InspectionRecorded");
        await Task.Delay(200);

        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        Assert.Equal("Passed", (await reliefGot.Task.WaitAsync(TimeSpan.FromSeconds(5))).GetProperty("status").GetString());
        Assert.Equal("Passed", (await transferredGot.Task.WaitAsync(TimeSpan.FromSeconds(5))).GetProperty("status").GetString());
    }
}
