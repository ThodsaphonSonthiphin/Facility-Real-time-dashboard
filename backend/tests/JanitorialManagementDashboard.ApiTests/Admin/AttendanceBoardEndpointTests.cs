using System.Net;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Admin;

/// <summary>
/// Wireframe D2, facility-0027/0050. Seed: AR01 (Day and Night; 1000001 Day, 1000002 Night) and AR02 (Day only; 1000003).
/// Log in before moving the clock.
/// </summary>
public class AttendanceBoardEndpointTests
{
    [Fact]
    public async Task A_standalone_loan_is_assigned_before_entry_and_entered_only_after_the_destination_scan()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var source = await TestData.AddBuildingAsync(factory, "B");
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var (cleaner, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E3001", ReliefApi.Phone("E3001"));
        var date = DateOnly.FromDateTime(factory.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(1);
        var dateString = ReliefApi.Iso(date);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, date.AddDays(-2));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, date.AddDays(-2));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var assignment = await ReliefApi.AssignAsync(supervisor, ReliefApi.Assignment(relief, destination, date, source, breakGroup: 1));
        Assert.Equal(ReliefApi.Committed, assignment.Answer!.Outcome);

        factory.Clock.SetUtcNow(new DateTimeOffset(date.ToDateTime(new TimeOnly(7, 5)), TimeSpan.FromHours(7)));
        var query = $"?shiftDate={dateString}&shift=Day";
        var before = (await AdminAttendanceApi.BoardAsync(admin, query)).Rows.Single(row => row.AreaId == destination);
        Assert.Equal("E3001", before.Cleaner!.EmployeeId);
        Assert.Equal("covered", before.Entry);
        Assert.Empty(before.EntryEvidence!);
        Assert.Equal(HttpStatusCode.OK, (await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn", TransferApi.Destination)).StatusCode);
        var after = (await AdminAttendanceApi.BoardAsync(admin, query)).Rows.Single(row => row.AreaId == destination);
        Assert.Equal("entered", after.Entry);
        Assert.Single(after.EntryEvidence!);
        Assert.Equal("ShiftIn", Assert.Single(after.Events).EventType);
    }

    [Fact]
    public async Task The_board_needs_an_admin()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateApiClient().GetAsync(AdminAttendanceApi.BoardPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(AdminAttendanceApi.BoardPath)).StatusCode);
    }

    [Fact]
    public async Task Before_anyone_checks_in_every_area_is_empty()
    {
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
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
        Assert.Equal(HttpStatusCode.OK, (await AttendanceApi.RecordAsync(cleaner, "Break1Out")).StatusCode);
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
    public async Task A_numbered_break_leaves_the_area_on_break()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var day = new DateOnly(2026, 10, 8);

        await TestData.AddAttendanceAsync(factory, "1000001", day, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 50));
        await TestData.AddAttendanceAsync(factory, "1000001", day, Shift.Day, AttendanceEvent.Break1Out, ThaiClock.At(8, 10, 5));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 30));

        // Spec §8: a numbered break out without its return is the same "in the Area" answer the legacy Break-Out gives.
        var board = await AdminAttendanceApi.BoardAsync(admin, "?shiftDate=2026-10-08&shift=Day");

        Assert.Equal("OnBreak", board.Rows.Single(r => r.AreaCode == "AR01").State);
    }

    [Fact]
    public async Task The_night_board_leaves_out_day_only_areas()
    {
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync(AdminAttendanceApi.BoardPath + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_check_in_reaches_the_admins_live_and_only_the_admins()
    {
        using var factory = new JanitorialApiFactory();
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

    [Fact]
    public async Task Weekly_day_off_or_whole_shift_leave_regular_cleaner_does_not_displace_relief_or_mark_area_covered()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var date = DateOnly.FromDateTime(factory.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(1);
        var weekStart = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var dateString = ReliefApi.Iso(date);
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, date.AddDays(-7));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", date.AddDays(-7), "AR01", breakGroup: 1);
        await ScheduleWriteApi.AddProfileAsync(factory, "1000003", CleanerKind.Regular, Shift.Day, date.AddDays(-7));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000003", date.AddDays(-7), "AR02", breakGroup: 1);

        // 1000001 takes WeeklyDayOff on date; 1000003 takes whole-shift Leave on date.
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "1000003");
        var adminId = adminAuth.User.Id;
        var shiftStart = new DateTimeOffset(date.ToDateTime(new TimeOnly(7, 0)), TimeSpan.FromHours(7)).UtcDateTime;
        var shiftEnd = new DateTimeOffset(date.ToDateTime(new TimeOnly(19, 0)), TimeSpan.FromHours(7)).UtcDateTime;
        await factory.WithDbAsync(async db =>
        {
            var off = new WeeklyDayOff
            {
                UserId = e1001, WeekStart = weekStart, CreatedById = adminId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            off.Choices.Add(new WeeklyDayOffChoice { OffDate = date });
            db.WeeklyDayOffs.Add(off);
            db.ScheduleExceptions.Add(new ScheduleException
            {
                UserId = e1003, Kind = ScheduleExceptionKind.Leave, ShiftDate = date, Shift = Shift.Day,
                StartsAt = shiftStart, EndsAt = shiftEnd, CreatedById = adminId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        // Assign Relief E3001 to AR01 and have them check in; leave AR02 without a replacement.
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "A", Shift.Day);
        var (reliefClient, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E3001", ReliefApi.Phone("E3001"));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, date.AddDays(-7));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var assigned = await ReliefApi.AssignAsync(supervisor, ReliefApi.Assignment(relief, ar01, date, buildingA, breakGroup: 1));
        Assert.Equal(ReliefApi.Committed, assigned.Answer!.Outcome);

        factory.Clock.SetUtcNow(new DateTimeOffset(date.ToDateTime(new TimeOnly(7, 5)), TimeSpan.FromHours(7)));
        Assert.Equal(HttpStatusCode.OK, (await EffectiveAttendanceApi.RecordAsync(reliefClient, "ShiftIn", "token-checkin-ar01")).StatusCode);

        var board = await AdminAttendanceApi.BoardAsync(admin, $"?shiftDate={dateString}&shift=Day");
        var rowAr01 = board.Rows.Single(r => r.AreaId == ar01);
        var rowAr02 = board.Rows.Single(r => r.AreaId == ar02);

        // AR01 must show the working Relief Cleaner as primary with their Shift-In, not the off-duty 1000001.
        Assert.Equal("E3001", rowAr01.Cleaner!.EmployeeId);
        Assert.Equal("Working", rowAr01.State);
        Assert.Single(rowAr01.Events);

        // AR02 has only an on-leave Regular Cleaner and no replacement, so it must be unassigned and empty.
        Assert.Null(rowAr02.Cleaner);
        Assert.Equal("Empty", rowAr02.State);
        Assert.Equal("unassigned", rowAr02.Entry);
    }
}
