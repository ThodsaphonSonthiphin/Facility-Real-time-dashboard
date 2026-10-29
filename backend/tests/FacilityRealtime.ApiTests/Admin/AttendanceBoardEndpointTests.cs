using System.Net;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// Wireframe D2, facility-0027/0050. Seed: AR01 (Day and Night; 1000001 Day, 1000002 Night) and AR02 (Day only; 1000003).
/// Log in before moving the clock.
/// </summary>
public class AttendanceBoardEndpointTests
{
    [Fact]
    public async Task The_board_needs_an_admin()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateApiClient().GetAsync(AdminAttendanceApi.BoardPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(AdminAttendanceApi.BoardPath)).StatusCode);
    }

    [Fact]
    public async Task Before_anyone_checks_in_every_area_is_empty()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));

        var board = await AdminAttendanceApi.BoardAsync(admin);

        Assert.Equal(new DateOnly(2026, 10, 8), board.ShiftDate);
        Assert.Equal("Day", board.Shift);
        Assert.False(board.ShiftEnded);
        Assert.Equal(new[] { "AR01", "AR02" }, board.Rows.Select(r => r.AreaCode));
        Assert.All(board.Rows, r => Assert.Equal("Empty", r.State));
        Assert.Equal("1000001", board.Rows[0].Cleaner!.EmployeeId);
        Assert.Equal("A", board.Rows[0].BuildingCode);
        Assert.Equal(new BoardSummaryModel(2, 0, 2, 0), board.Summary);
    }

    [Fact]
    public async Task An_area_follows_its_cleaners_scans_and_empty_areas_come_first()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var working = await AdminAttendanceApi.BoardAsync(admin);
        Assert.Equal(new[] { "AR02", "AR01" }, working.Rows.Select(r => r.AreaCode));
        var ar01 = working.Rows[1];
        Assert.Equal("Working", ar01.State);
        var shiftIn = Assert.Single(ar01.Events);
        Assert.Equal("ShiftIn", shiftIn.EventType);
        Assert.Equal("Scan", shiftIn.Source);
        Assert.Equal(ThaiClock.At(8, 6, 50).UtcDateTime, shiftIn.OccurredAt);
        Assert.True(shiftIn.WithinRadius);
        Assert.Equal(new BoardSummaryModel(2, 1, 1, 0), working.Summary);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 11, 0));
        Assert.Equal(HttpStatusCode.OK, (await AttendanceApi.RecordAsync(cleaner, "BreakOut")).StatusCode);
        Assert.Equal("OnBreak", (await AdminAttendanceApi.BoardAsync(admin)).Rows.Single(r => r.AreaCode == "AR01").State);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 19, 5));
        Assert.Equal(HttpStatusCode.OK, (await AttendanceApi.RecordAsync(cleaner, "ShiftOut")).StatusCode);
        // 19:05 is the Night shift on the clock, so ask for the Day shift
        var done = await AdminAttendanceApi.BoardAsync(admin, "?shiftDate=2026-10-08&shift=Day");
        Assert.Equal("Done", done.Rows.Single(r => r.AreaCode == "AR01").State);
        Assert.True(done.ShiftEnded);
        Assert.Equal(1, done.Summary.Present);
    }

    [Fact]
    public async Task The_night_board_leaves_out_day_only_areas()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 21, 0));

        var board = await AdminAttendanceApi.BoardAsync(admin);

        Assert.Equal("Night", board.Shift);
        var row = Assert.Single(board.Rows);
        Assert.Equal("AR01", row.AreaCode);
        Assert.Equal("1000002", row.Cleaner!.EmployeeId);
    }

    [Fact]
    public async Task A_cover_fills_an_area_whose_cleaner_did_not_come()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        await TestData.AddCoverAsync(factory, "1000001", "AR02", new DateOnly(2026, 10, 8), Shift.Day);
        await TestData.AddCoverAsync(factory, "1000001", "AR02", new DateOnly(2026, 10, 8), Shift.Day, cancelled: true);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var board = await AdminAttendanceApi.BoardAsync(admin);

        var ar02 = board.Rows.Single(r => r.AreaCode == "AR02");
        Assert.Equal("Covered", ar02.State);
        Assert.Equal("1000003", ar02.Cleaner!.EmployeeId);
        Assert.Empty(ar02.Events);
        var cover = Assert.Single(ar02.Covers);
        Assert.Equal("1000001", cover.EmployeeId);
        Assert.Equal("AR01", cover.OwnAreaCode);
        Assert.Equal(ThaiClock.At(8, 6, 50).UtcDateTime, cover.ShiftInAt);
        Assert.Equal(new BoardSummaryModel(2, 1, 0, 1), board.Summary);
    }

    [Fact]
    public async Task A_check_in_far_from_the_sign_carries_its_distance()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 6, 50));
        var far = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);
        Assert.Equal(HttpStatusCode.OK, far.StatusCode);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var shiftIn = Assert.Single((await AdminAttendanceApi.BoardAsync(admin)).Rows.Single(r => r.AreaCode == "AR01").Events);

        Assert.False(shiftIn.WithinRadius);
        Assert.True(shiftIn.DistanceM > 50);
    }

    [Fact]
    public async Task The_board_filters_by_building_and_leaves_out_deactivated_areas()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);

        Assert.Equal(2, (await AdminAttendanceApi.BoardAsync(admin, $"?buildingId={buildingA}")).Rows.Count);
        Assert.Empty((await AdminAttendanceApi.BoardAsync(admin, $"?buildingId={buildingA + 100}")).Rows);

        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Code == "AR02")).IsActive = false;
            await db.SaveChangesAsync();
        });
        Assert.Equal(new[] { "AR01" }, (await AdminAttendanceApi.BoardAsync(admin)).Rows.Select(r => r.AreaCode));
    }

    [Theory]
    [InlineData("?shiftDate=2026-10-08")]
    [InlineData("?shift=Day")]
    [InlineData("?shiftDate=2026-10-08&shift=Evening")]
    [InlineData("?shiftDate=2026-10-08&shift=1")]
    [InlineData("?shiftDate=08/10/2026&shift=Day")]
    public async Task A_shift_needs_both_a_date_and_day_or_night(string query)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync(AdminAttendanceApi.BoardPath + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_check_in_reaches_the_admins_live_and_only_the_admins()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, cleanerAuth, _) = await AuthApi.LoggedInAsync(factory);
        var (_, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await using var adminHub = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        await using var cleanerHub = await HubClient.ConnectAsync(factory, cleanerAuth.AccessToken);
        var got = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanerGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminHub.On<JsonElement>("AttendanceRecorded", payload => got.TrySetResult(payload));
        cleanerHub.On<JsonElement>("AttendanceRecorded", payload => cleanerGot.TrySetResult(payload));
        await Task.Delay(200);

        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));

        var payload = await got.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR01"), payload.GetProperty("areaId").GetInt32());
        Assert.Equal("2026-10-08", payload.GetProperty("shiftDate").GetString());
        Assert.Equal("Day", payload.GetProperty("shift").GetString());
        await Task.Delay(500);
        Assert.False(cleanerGot.Task.IsCompleted); // facility-0058
    }
}
