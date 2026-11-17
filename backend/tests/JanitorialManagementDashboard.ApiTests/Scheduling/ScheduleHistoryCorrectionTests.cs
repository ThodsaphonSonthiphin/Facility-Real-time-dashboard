using System.Net;
using System.Text.Json;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §5 and §11 with ADRs facility-0187, 0189 and 0214 and acceptance A03 and A28: the ordinary baseline editor only
/// moves a plan forward, and a deliberate correction of a date that has already been planned is the separate
/// <c>correct-plan</c> command — it needs a reason, retains the original revision beside the corrected one, and never
/// rewrites recorded attendance, scans or inspections.
/// </summary>
public class ScheduleHistoryCorrectionTests
{
    private static readonly DateOnly Week = ScheduleWriteApi.Week;
    private static readonly DateOnly Past = new(2026, 11, 12);

    [Fact]
    public async Task A_past_regular_baseline_is_refused_while_a_reasoned_plan_correction_is_its_own_path()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var abandoned = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = ScheduleWriteApi.Iso(Past),
            days = new[] { new { weekday = "Thursday", areaId = area2 } },
        });
        var corrected = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2, reason = "แก้ Area ผิดคน" });

        Assert.Equal(HttpStatusCode.Conflict, abandoned.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(abandoned.Answer!.Problems).Code);
        Assert.Equal(HttpStatusCode.OK, corrected.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, corrected.Answer!.Outcome);
        Assert.Equal("1", corrected.Answer.Revisions[ScheduleWriteApi.DatedKey(person, Past, "Day", "area")]);
        await factory.WithDbAsync(async db =>
        {
            Assert.Single(await db.ScheduleBaselines.ToListAsync()); // no retroactive baseline version
            var row = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.Equal((Past, area2), (row.ShiftDate, row.AreaId!.Value));
            var change = Assert.Single(await db.ScheduleChanges.ToListAsync());
            Assert.Equal("แก้ Area ผิดคน", change.Reason);
        });
    }

    [Fact]
    public async Task A_past_date_cannot_be_edited_by_the_ordinary_dated_editor()
    {
        // ADR facility-0214: the two paths never overlap, so a passed date is only reachable through the reasoned correction.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var area = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2 });
        var breakGroup = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedBreak,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", breakGroup = 2 });

        Assert.All(new[] { area, breakGroup }, answer =>
        {
            Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
            Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Answer!.Problems).Code);
        });
        await factory.WithDbAsync(async db => Assert.Empty(await db.ScheduleExceptions.ToListAsync()));
    }

    [Fact]
    public async Task A_correction_needs_a_trimmed_reason()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var blank = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2, reason = "   " });
        var missing = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2 });
        var tooLong = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2, reason = new string('ก', 501) });

        Assert.All(new[] { blank, missing, tooLong }, answer =>
        {
            Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
            Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Answer!.Problems).Code);
        });
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.ScheduleExceptions.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_correction_records_the_original_and_the_corrected_revision()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var created = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2, reason = "ย้าย Area" });
        var revised = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area1, reason = "แก้กลับ" },
            created.Answer!.Revisions);

        Assert.Equal(HttpStatusCode.OK, revised.Response.StatusCode);
        Assert.Equal("2", revised.Answer!.Revisions[ScheduleWriteApi.DatedKey(person, Past, "Day", "area")]);
        await factory.WithDbAsync(async db =>
        {
            var row = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.Equal(2, row.Revision);
            Assert.Equal(area1, row.AreaId);
            var changes = await db.ScheduleChanges.OrderBy(change => change.Id).ToListAsync();
            Assert.Equal(2, changes.Count);
            using var first = JsonDocument.Parse(changes[0].AfterJson!);
            Assert.Equal("0", first.RootElement.GetProperty("originalRevision").GetString());
            Assert.Equal("1", first.RootElement.GetProperty("correctedRevision").GetString());
            using var second = JsonDocument.Parse(changes[1].AfterJson!);
            Assert.Equal("1", second.RootElement.GetProperty("originalRevision").GetString());
            Assert.Equal("2", second.RootElement.GetProperty("correctedRevision").GetString());
            // The before side keeps the plan as it stood, so the screen can show original and corrected apart (A28).
            using var before = JsonDocument.Parse(changes[1].BeforeJson!);
            Assert.Equal(area2, before.RootElement.GetProperty("areaId").GetInt32());
        });
    }

    [Fact]
    public async Task A_date_that_has_not_been_planned_yet_is_not_a_correction()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var future = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = "2026-12-10", shift = "Day", areaId = area2, reason = "ล่วงหน้า" });
        var nothing = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", reason = "ไม่มีอะไรแก้" });

        Assert.Equal(HttpStatusCode.Conflict, future.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(future.Answer!.Problems).Code);
        Assert.Equal(HttpStatusCode.Conflict, nothing.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(nothing.Answer!.Problems).Code);
        await factory.WithDbAsync(async db => Assert.Empty(await db.ScheduleExceptions.ToListAsync()));
    }

    [Fact]
    public async Task A_correction_leaves_the_recorded_attendance_and_scans_alone()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        await ScheduleWriteApi.AddCheckInAsync(factory, "1000001", Past, Shift.Day);
        var scan = await TestData.AddScanAsync(factory, "token-restroom-m1",
            new DateTimeOffset(2026, 11, 12, 8, 30, 0, TimeSpan.FromHours(7)));
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var corrected = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2, reason = "แก้ Area" });

        Assert.Equal(HttpStatusCode.OK, corrected.Response.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var attendance = Assert.Single(await db.ShiftAttendances.ToListAsync());
            Assert.Equal(area1, attendance.AreaId);          // the actual entry stays in the Area it happened in
            Assert.Equal(AttendanceEvent.ShiftIn, attendance.EventType);
            var record = Assert.Single(await db.ScanRecords.ToListAsync());
            Assert.Equal(scan, record.Id);
            Assert.Empty(await db.InspectionRecords.ToListAsync());
            Assert.Single(await db.ScheduleExceptions.ToListAsync());
        });
    }

    [Fact]
    public async Task A_dated_break_override_survives_a_later_regular_baseline_change()
    {
        // A03 and ADR facility-0189: keeping the explicit 15 December group is not the editor's silent deletion of it.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001", breakGroup: 1);
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var dated = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedBreak,
            new { userId = person, date = "2026-12-15", shift = "Day", breakGroup = 1 });
        var baseline = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = "2026-12-02",
            days = Enum.GetValues<DayOfWeek>()
                .Select(weekday => new { weekday = weekday.ToString(), areaId = area1, breakGroup = 2 })
                .ToArray(),
        });

        Assert.Equal(HttpStatusCode.OK, dated.Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, baseline.Response.StatusCode);
        var week = await ScheduleWriteApi.WeekAsync(admin, new DateOnly(2026, 12, 14));
        var row = week.Rows.Single(entry => entry.UserId == person);
        var overridden = row.Days.Single(day => day.Date == new DateOnly(2026, 12, 15));
        var inherited = row.Days.Single(day => day.Date == new DateOnly(2026, 12, 14));
        Assert.Equal(1, overridden.BreakGroup.Value);
        Assert.Equal("dated", overridden.BreakGroup.SourceKind);
        Assert.Equal(2, inherited.BreakGroup.Value);
        Assert.Equal("baseline", inherited.BreakGroup.SourceKind);
        await factory.WithDbAsync(async db =>
        {
            var exception = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.False(exception.IsCancelled);
            Assert.Equal(1, exception.BreakGroup);
        });
    }

    [Fact]
    public async Task A_week_that_has_already_started_needs_a_reason_for_its_day_off_edit()
    {
        // Spec §5: a retroactive plan edit needs a reason. The weekly day-off editor accepts one date inside the current week
        // that has already passed, so that edit is retroactive and is refused with nothing written until it carries one.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);
        var past = Week; // Monday the 16th; today's business date is Tuesday the 17th

        var missing = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(past) } } },
        });
        var blank = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(past) } } },
            reason = "   ",
        });

        Assert.All(new[] { missing, blank }, answer =>
        {
            Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
            Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Answer!.Problems).Code);
        });
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_reasoned_week_that_has_already_started_commits_and_the_audit_carries_the_reason()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);
        var past = Week; // Monday the 16th, already past on the 17th

        var answer = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(past) } } },
            reason = "  แจ้งลาย้อนหลัง  ",
        });

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer.Answer!.Outcome);
        Assert.Equal("1", answer.Answer.Revisions[ScheduleWriteApi.WeekKey(person, Week)]);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(past, Assert.Single(header.Choices).OffDate);
            var change = Assert.Single(await db.ScheduleChanges.ToListAsync());
            Assert.Equal("create", change.Operation);
            Assert.Equal("แจ้งลาย้อนหลัง", change.Reason);
        });
    }

    [Fact]
    public async Task A_no_op_resubmission_of_a_started_week_answers_committed_without_a_reason()
    {
        // Ruling (f) against the retroactive reason rule: a submission that changes nothing is not an edit, so it needs no
        // reason and leaves the header exactly as it was — while a real change inside the same started week still needs one.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);
        var pastWeek = Week.AddDays(-7); // Monday the 9th: every date in it has already passed
        var key = ScheduleWriteApi.WeekKey(person, pastWeek);

        var recorded = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(pastWeek),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(pastWeek) } } },
            reason = "แจ้งลาย้อนหลัง",
        });
        var again = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(pastWeek),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(pastWeek) } } },
        }, recorded.Answer!.Revisions);
        var changed = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(pastWeek),
            rows = new[]
            {
                new
                {
                    userId = person,
                    offDates = new[] { ScheduleWriteApi.Iso(pastWeek), ScheduleWriteApi.Iso(pastWeek.AddDays(2)) },
                },
            },
        }, again.Answer!.Revisions);

        Assert.Equal(HttpStatusCode.OK, recorded.Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, again.Answer!.Outcome);
        Assert.Equal("1", again.Answer.Revisions[key]);
        Assert.Equal(HttpStatusCode.Conflict, changed.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(changed.Answer!.Problems).Code);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(1, header.Revision);
            Assert.Equal(pastWeek, Assert.Single(header.Choices).OffDate);
            var change = Assert.Single(await db.ScheduleChanges.ToListAsync());
            Assert.Equal("create", change.Operation);
            Assert.Equal("แจ้งลาย้อนหลัง", change.Reason);
        });
    }

    [Fact]
    public async Task A_week_that_has_not_started_does_not_need_a_reason()
    {
        // The requirement is about retroactive edits only: a future week still commits without one.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);
        var week = Week.AddDays(7);

        var answer = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(week.AddDays(2)) } } },
        });

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(week, Assert.Single(await db.WeeklyDayOffs.ToListAsync()).WeekStart);
            Assert.Null(Assert.Single(await db.ScheduleChanges.ToListAsync()).Reason);
        });
    }

    [Fact]
    public async Task A_supervisor_corrects_only_the_history_of_a_building_they_still_hold()
    {
        // Spec §3: a correction needs the responsibility of the edited date and the one held today.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        await TestData.AddBuildingAsync(factory, "B");
        var areaB = await TestData.AddAreaAsync(factory, "AR03", "B");
        var other = await TestData.AddCleanerAsync(factory, "1000009", Shift.Day, areaCode: "AR03");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var own = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2, reason = "แก้ Area ผิด" });
        var foreign = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.CorrectPlan,
            new { userId = other, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = areaB, reason = "แก้ Area ผิด" });

        Assert.Equal(HttpStatusCode.OK, own.Response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, foreign.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(foreign.Answer!.Problems).Code);
        await factory.WithDbAsync(async db =>
        {
            var row = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.Equal(person, row.UserId);
        });
    }

    [Fact]
    public async Task A_supervisor_who_lost_the_building_cannot_correct_its_past()
    {
        // The other half of §3: the responsibility of the edited date is not enough once today's scope has moved on. This
        // Supervisor still holds a building today (so the command may be submitted at all) but not the one edited.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day,
            new DateOnly(2026, 10, 1), until: new DateOnly(2026, 11, 1));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 11, 1));
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2, reason = "แก้ Area ผิด" });

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Answer!.Problems).Code);
        await factory.WithDbAsync(async db => Assert.Empty(await db.ScheduleExceptions.ToListAsync()));
    }

    [Fact]
    public async Task A_supervisor_with_no_current_scope_cannot_submit_a_command_at_all()
    {
        // Spec §11 and controller ruling: the engine refuses the actor before any handler runs, so the answer carries no
        // row detail and is not a decision about one row.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day,
            new DateOnly(2026, 10, 1), until: new DateOnly(2026, 11, 1));
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.CorrectPlan,
            new { userId = person, date = ScheduleWriteApi.Iso(Past), shift = "Day", areaId = area2, reason = "แก้ Area ผิด" });

        Assert.Equal(HttpStatusCode.Forbidden, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.NotAuthorised, answer.Answer!.Outcome);
        Assert.Empty(answer.Answer.Problems);
        await factory.WithDbAsync(async db => Assert.Empty(await db.ScheduleExceptions.ToListAsync()));
    }
}
