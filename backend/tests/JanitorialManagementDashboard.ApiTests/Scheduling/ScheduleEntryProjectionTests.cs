using System.Net;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Attendance;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §13 and ADR facility-0216 (Task 11): the attendance board read answers one Area's actual entry evidence and its
/// current responsibility separately. An instruction alone assigns the destination without entry; a confirmed arrival adds
/// the destination as entered while the origin keeps its historical entry; a later Shift-Out hides neither; a cancelled
/// instruction adds nothing; and a leave or day-off record never erases what somebody actually did.
/// </summary>
public class ScheduleEntryProjectionTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    [Fact]
    public async Task A_pending_instruction_assigns_the_destination_without_entry_or_a_cleaner()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 1));
        var board = await BoardAsync(admin);

        // The target denominator is untouched: every Area that works the shift is still a row.
        Assert.Equal(new[] { "AR01", "AR02", "AR03" }, board.Rows.Select(row => row.AreaCode).OrderBy(code => code));

        // The origin keeps its historical entry, but responsibility has left it.
        var ar01 = board.Rows.Single(row => row.AreaCode == "AR01");
        Assert.Equal("entered", ar01.Entry);
        Assert.Equal(new[] { "shift-in" }, ar01.EntryEvidence!.Select(row => row.Kind));
        Assert.Null(ar01.Cleaner);
        Assert.Null(ar01.Responsibility);

        // The destination is assigned by the effective instruction, has no entry evidence and nobody standing in it yet.
        var ar03 = board.Rows.Single(row => row.AreaCode == "AR03");
        Assert.Equal("covered", ar03.Entry);
        Assert.Empty(ar03.EntryEvidence!);
        Assert.Null(ar03.Cleaner);
        Assert.Equal("instructed", ar03.Responsibility!.State);
        Assert.Null(ar03.Responsibility.ConfirmedAt);
    }

    [Fact]
    public async Task A_confirmed_arrival_enters_the_destination_and_keeps_the_origin_entry()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 6));
        var board = await BoardAsync(admin);

        var ar01 = board.Rows.Single(row => row.AreaCode == "AR01");
        Assert.Equal("entered", ar01.Entry);
        var shiftIn = Assert.Single(ar01.EntryEvidence!);
        Assert.Equal("shift-in", shiftIn.Kind);
        Assert.Null(shiftIn.TransferId);
        Assert.Null(ar01.Cleaner);

        var ar03 = board.Rows.Single(row => row.AreaCode == "AR03");
        Assert.Equal("entered", ar03.Entry);
        var arrival = Assert.Single(ar03.EntryEvidence!);
        Assert.Equal("transfer-arrival", arrival.Kind);
        Assert.Equal(transferId, arrival.TransferId);
        // The evidence carries the actual arrival time, never the instruction's plan time.
        Assert.Equal(TransferApi.Thai(13, 5).UtcDateTime, arrival.RecordedAt);
        Assert.Equal("1000001", ar03.Cleaner!.EmployeeId);
        Assert.Equal("confirmed", ar03.Responsibility!.State);
        Assert.Equal(TransferApi.Thai(13, 5).UtcDateTime, ar03.Responsibility.ConfirmedAt);
        Assert.Equal(destination, ar03.Responsibility.LastConfirmedAreaId);
    }

    [Fact]
    public async Task A_final_shift_out_keeps_both_areas_entered_and_ends_the_destination()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        factory.Clock.SetUtcNow(TransferApi.Thai(19, 5));
        Assert.Equal(HttpStatusCode.OK,
            (await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftOut", TransferApi.Destination)).StatusCode);

        factory.Clock.SetUtcNow(TransferApi.Thai(19, 6));
        var board = await BoardAsync(admin);

        // ADR facility-0216: A's entry is history and Shift-Out does not remove it; B remains entered and is now done.
        Assert.Equal("entered", board.Rows.Single(row => row.AreaCode == "AR01").Entry);
        var ar03 = board.Rows.Single(row => row.AreaCode == "AR03");
        Assert.Equal("entered", ar03.Entry);
        Assert.Equal("Done", ar03.State);
        Assert.Equal("transfer-arrival", Assert.Single(ar03.EntryEvidence!).Kind);
    }

    [Fact]
    public async Task A_leave_and_a_day_off_record_do_not_hide_actual_entry()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        // An agreed leave and a saved day-off choice are absence records, not evidence erasers (spec §13).
        await EffectiveAttendanceApi.AddLeaveAsync(factory, "1000001", Day, Shift.Day,
            TransferApi.Thai(12, 0), TransferApi.Thai(18, 0));
        await factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(user => user.EmployeeId == "1000001");
            var actor = await db.Users.SingleAsync(user => user.Username == "admin");
            db.WeeklyDayOffs.Add(new WeeklyDayOff
            {
                UserId = person.Id,
                WeekStart = new DateOnly(2026, 10, 5),
                Revision = 1,
                CreatedById = actor.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Choices = [new WeeklyDayOffChoice { OffDate = Day }],
            });
            await db.SaveChangesAsync();
        });

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 6));
        var board = await BoardAsync(admin);

        Assert.Equal("entered", board.Rows.Single(row => row.AreaCode == "AR01").Entry);
        Assert.Equal("entered", board.Rows.Single(row => row.AreaCode == "AR03").Entry);
    }

    [Fact]
    public async Task A_cancelled_instruction_adds_no_entry_and_restores_the_origin()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        factory.Clock.SetUtcNow(TransferApi.Thai(12, 58));
        var cancelled = await TransferApi.CancelAsync(admin, transferId,
            expected: new() { [TransferApi.Key(auth.User.Id)] = "1" });
        Assert.Equal(HttpStatusCode.OK, cancelled.Response.StatusCode);

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 1));
        var board = await BoardAsync(admin);

        var ar01 = board.Rows.Single(row => row.AreaCode == "AR01");
        Assert.Equal("entered", ar01.Entry);
        Assert.Equal("1000001", ar01.Cleaner!.EmployeeId);
        Assert.Null(ar01.Responsibility);

        var ar03 = board.Rows.Single(row => row.AreaCode == "AR03");
        Assert.Equal("unassigned", ar03.Entry);
        Assert.Empty(ar03.EntryEvidence!);
        Assert.Null(ar03.Responsibility);
    }

    /// <summary>The Day board of the transfer tests' synthetic week, read exactly as the Admin page asks for it.</summary>
    private static Task<AttendanceBoardModel> BoardAsync(HttpClient admin) =>
        AdminAttendanceApi.BoardAsync(admin, "?shiftDate=2026-10-08&shift=Day");
}
