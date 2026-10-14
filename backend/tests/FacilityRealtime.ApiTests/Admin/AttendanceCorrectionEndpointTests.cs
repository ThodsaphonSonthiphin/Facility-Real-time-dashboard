using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0051: the Admin adds or moves a Cleaner's attendance time with a reason, and each change is logged.</summary>
public class AttendanceCorrectionEndpointTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private static Task<List<ShiftAttendance>> EventsAsync(FacilityApiFactory factory, string employeeId) =>
        AdminSetupApi.ReadAsync(factory, db => db.ShiftAttendances.AsNoTracking()
            .Where(a => a.User!.EmployeeId == employeeId)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync());

    [Fact]
    public async Task Admin_adds_a_forgotten_check_in_with_a_reason()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "E1003");

        var response = await AdminAttendanceApi.CorrectAsync(admin, e1003, Oct8, "Day", "ShiftIn", "06:58", "มือถือแบตหมด หัวหน้ายืนยันว่ามาทำงาน");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var added = (await response.Content.ReadFromJsonAsync<BoardEventModel>())!;
        Assert.Equal("AdminAdd", added.Source);
        Assert.Equal(ThaiClock.At(8, 6, 58).UtcDateTime, added.OccurredAt);
        var stored = Assert.Single(await EventsAsync(factory, "E1003"));
        Assert.Null(stored.SignId);
        Assert.Null(stored.WithinRadius);
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR02"), stored.AreaId);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ATTENDANCE_ADD", entry.Action);
        Assert.Equal(adminAuth.User.Id, entry.ActorId);
        Assert.Equal("shift_attendances", entry.EntityType);
        Assert.Equal(stored.Id, entry.EntityId);
        Assert.Equal("มือถือแบตหมด หัวหน้ายืนยันว่ามาทำงาน", entry.Reason);
        Assert.Null(entry.BeforeJson);
        Assert.Contains("06:58", entry.Summary);
        Assert.Equal("Working", (await AdminAttendanceApi.BoardAsync(admin)).Rows.Single(r => r.AreaCode == "AR02").State);
    }

    [Fact]
    public async Task Admin_moves_a_scanned_time_and_the_old_time_stays_in_the_log()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await AdminAttendanceApi.CorrectAsync(admin, e1001, Oct8, "Day", "ShiftIn", "06:45", "เน็ตหลุด สแกนช้า");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = Assert.Single(await EventsAsync(factory, "E1001"));
        Assert.Equal(AttendanceSource.AdminEdit, stored.Source);
        Assert.Equal(ThaiClock.At(8, 6, 45).UtcDateTime, stored.OccurredAt);
        // A scanned event keeps its sign and GPS verdict
        Assert.NotNull(stored.SignId);
        Assert.True(stored.WithinRadius);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ATTENDANCE_EDIT", entry.Action);
        Assert.Contains("23:50:00Z", entry.BeforeJson);
        Assert.Contains("23:45:00Z", entry.AfterJson);
    }

    [Fact]
    public async Task A_missing_night_shift_out_is_filled_on_the_next_morning()
    {
        using var factory = new FacilityApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1002", "0810000002");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, night, ThaiClock.At(8, 18, 55));
        factory.Clock.SetUtcNow(ThaiClock.At(9, 9, 0));
        var e1002 = await AdminSetupApi.UserIdAsync(factory, "E1002");

        var response = await AdminAttendanceApi.CorrectAsync(admin, e1002, Oct8, "Night", "ShiftOut", "07:00", "ลืมสแกนเลิกงาน");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shiftOut = (await EventsAsync(factory, "E1002")).Single(a => a.EventType == AttendanceEvent.ShiftOut);
        Assert.Equal(ThaiClock.At(9, 7, 0).UtcDateTime, shiftOut.OccurredAt);
        Assert.Equal(AttendanceSource.AdminAdd, shiftOut.Source);
    }

    [Theory]
    [InlineData("Day", "ShiftOut", "05:00", "ลืมสแกน")]    // before the window opens at 06:00
    [InlineData("Day", "ShiftOut", "13:00", "ลืมสแกน")]    // still in the future at 12:00
    [InlineData("Night", "ShiftIn", "20:00", "ลืมสแกน")]   // not E1001's regular shift
    [InlineData("Day", "BreakIn", "10:00", "ลืมสแกน")]     // no Break-Out before it
    [InlineData("Day", "BreakOut", "06:30", "ลืมสแกน")]    // before the 06:50 Shift-In
    [InlineData("Day", "ShiftOut", "7:00", "ลืมสแกน")]     // not HH:mm
    [InlineData("Day", "ShiftOut", "11:00", "   ")]        // no reason
    [InlineData("Day", "ShiftOut", "11:00", null)]
    public async Task Invalid_corrections_are_refused_and_change_nothing(string shift, string eventType, string time, string? reason)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 12, 0));
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await AdminAttendanceApi.CorrectAsync(admin, e1001, Oct8, shift, eventType, time, reason);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(await EventsAsync(factory, "E1001"));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Only_active_cleaners_have_attendance()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var supervisor = await AdminSetupApi.UserIdAsync(factory, "S2001");

        var response = await AdminAttendanceApi.CorrectAsync(admin, supervisor, Oct8, "Day", "ShiftIn", "06:58", "ลืมสแกน");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Saving_the_same_time_again_logs_nothing()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "E1003");

        var first = await AdminAttendanceApi.CorrectAsync(admin, e1003, Oct8, "Day", "ShiftIn", "06:58", "ลืมสแกน");
        var again = await AdminAttendanceApi.CorrectAsync(admin, e1003, Oct8, "Day", "ShiftIn", "06:58", "ลืมสแกน");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Only_admins_correct_attendance()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);

        var response = await AdminAttendanceApi.CorrectAsync(cleaner, auth.User.Id, Oct8, "Day", "ShiftIn", "06:58", "แก้เอง");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
