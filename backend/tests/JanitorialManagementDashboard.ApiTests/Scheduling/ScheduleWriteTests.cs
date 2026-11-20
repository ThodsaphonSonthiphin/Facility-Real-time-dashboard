using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §3, §5, §11 and acceptance A01, A03, A04, A07 and A18: the five schedule routes are the only way a screen changes a
/// plan, who may call them is decided before the body is read, a submitted batch is one atomic command for the submitted
/// rows only, and a repeated no-op edit leaves the history it did not change alone.
/// <para>
/// The scenarios here always go through HTTP, because the two properties this task owns — "an unauthorised caller never
/// reaches a handler" and "a disabled server answers 404" — are properties of the route table, not of a service.
/// </para>
/// </summary>
public class ScheduleWriteTests
{
    private static readonly DateOnly Week = ScheduleWriteApi.Week;

    [Fact]
    public async Task An_unauthenticated_caller_cannot_reach_the_schedule_routes()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        ScheduleWriteApi.Today(factory);
        var client = factory.CreateApiClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/schedules/me?weekStart={ScheduleWriteApi.Iso(Week)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/schedules?weekStart={ScheduleWriteApi.Iso(Week)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/schedules/command-tokens", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/schedules/commands", new { kind = "set-weekly-days-off" })).StatusCode);
    }

    [Fact]
    public async Task A_cleaner_cannot_write_management_commands()
    {
        // Spec §3 and §11: the role gate runs before request-body binding, so an invalid body still cannot reach a handler.
        using var factory = ScheduleWriteApi.NewFactory();
        var (client, auth, _) = await AuthApi.LoggedInAsync(factory);
        ScheduleWriteApi.Today(factory);

        var response = await client.PostAsJsonAsync("/api/schedules/commands", new { kind = "set-weekly-days-off" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/schedules/command-tokens", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/schedules?weekStart={ScheduleWriteApi.Iso(Week)}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/schedules/me?weekStart={ScheduleWriteApi.Iso(Week)}")).StatusCode);
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.ScheduleCommandReceipts.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());
        });
        Assert.NotEqual(0, auth.User.Id);
    }

    [Fact]
    public async Task A_cleaner_own_read_ignores_a_forged_building_and_shift()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var (client, auth, _) = await AuthApi.LoggedInAsync(factory);
        ScheduleWriteApi.Today(factory);

        var forged = await client.GetAsync(
            $"/api/schedules/me?weekStart={ScheduleWriteApi.Iso(Week)}&buildingId={buildingB}&shift=Night");
        var week = await ScheduleWriteApi.ReadWeekAsync(forged);

        Assert.Equal(auth.User.Id, Assert.Single(week.Rows).UserId);
    }

    [Fact]
    public async Task An_admin_reads_the_week_and_gets_a_command_identity()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var (client, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var token = await ScheduleWriteApi.IssueAsync(client);
        var week = await ScheduleWriteApi.WeekAsync(client, Week);

        Assert.NotNull(token);
        Assert.NotEqual(Guid.Empty, token!.CommandId);
        Assert.False(string.IsNullOrEmpty(token.Token));
        Assert.Equal(Week, week.WeekStart);
        Assert.Equal(3, week.Rows.Count);
        Assert.Equal(ScheduleWriteApi.Iso(Week), ScheduleWriteApi.Iso(week.WeekStart));
    }

    [Fact]
    public async Task A_supervisor_cannot_write_another_buildings_people_across_command_kinds()
    {
        // A01: the dated scope decides every kind, not only the read.
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "AR03", "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var other = await TestData.AddCleanerAsync(factory, "1000009", Shift.Day, areaCode: "AR03", phone: "0810000009");
        var areaB = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var weekly = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff,
            new { weekStart = ScheduleWriteApi.Iso(Week), rows = new[] { new { userId = other, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(2)) } } } });
        var dated = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetDatedArea,
            new { userId = other, date = ScheduleWriteApi.Iso(Week.AddDays(2)), shift = "Day", areaId = areaB });
        var leave = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.RecordLeave,
            new { userId = other, date = ScheduleWriteApi.Iso(Week.AddDays(2)), shift = "Day", startsAt = "2026-11-18T00:00:00Z", endsAt = "2026-11-18T12:00:00Z" });
        var baseline = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline,
            new { userId = other, effectiveFrom = "2026-12-02", days = new[] { new { weekday = "Monday", areaId = areaB } } });
        var relief = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetReliefBaseline,
            new { userId = other, effectiveFrom = "2026-12-02", days = new[] { new { weekday = "Monday", areaId = areaB } } });
        var breakGroup = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetDatedBreak,
            new { userId = other, date = ScheduleWriteApi.Iso(Week.AddDays(2)), shift = "Day", breakGroup = 1 });
        var correction = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.CorrectPlan,
            new { userId = other, date = ScheduleWriteApi.Iso(Week.AddDays(2)), shift = "Day", areaId = areaB, reason = "แก้ไข" });

        Assert.All(new[] { weekly, dated, leave, baseline, relief, breakGroup, correction }, answer =>
        {
            Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
            Assert.Equal(ScheduleCommandOutcome.Rejected, answer.Answer!.Outcome);
            Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Answer.Problems).Code);
        });
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());
            Assert.Empty(await db.ScheduleExceptions.ToListAsync());
            Assert.Empty(await db.ScheduleBaselines.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_supervisor_writes_the_buildings_and_shift_the_dated_scope_gives_them()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var area = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var weekly = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff,
            new { weekStart = ScheduleWriteApi.Iso(Week), rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(2)) } } } });
        var dated = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = ScheduleWriteApi.Iso(Week.AddDays(3)), shift = "Day", areaId = area });

        Assert.Equal(HttpStatusCode.OK, weekly.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, weekly.Answer!.Outcome);
        Assert.Equal("1", weekly.Answer.Revisions[ScheduleWriteApi.WeekKey(person, Week)]);
        Assert.Equal(HttpStatusCode.OK, dated.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, dated.Answer!.Outcome);
        Assert.Equal("1", dated.Answer.Revisions[ScheduleWriteApi.DatedKey(person, Week.AddDays(3), "Day", "area")]);
    }

    [Fact]
    public async Task A_submitted_part_of_the_roster_succeeds_and_stays_visibly_incomplete()
    {
        // A07: only the submitted people change, the batch is atomic, and an unsent day stays unset rather than guessed.
        using var factory = ScheduleWriteApi.NewFactory();
        var first = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var second = await AdminSetupApi.UserIdAsync(factory, "1000003");
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000001", Week);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", Week, "AR01");
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000003", Week);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000003", Week, "AR02");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = first, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(2)) } } },
        });

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        var week = await ScheduleWriteApi.WeekAsync(admin, Week);
        var chosen = week.Rows.Single(row => row.UserId == first);
        var untouched = week.Rows.Single(row => row.UserId == second);
        Assert.True(chosen.HasWeeklyChoice);
        Assert.True(chosen.Days.Single(day => day.Date == Week.AddDays(2)).IsDayOff);
        Assert.False(untouched.HasWeeklyChoice);
        Assert.All(untouched.Days, day => Assert.False(day.IsDayOff));

        // Saturday and Sunday were never chosen: the roster stays visibly incomplete instead of being filled in.
        Assert.All(chosen.Days.Where(day => day.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday),
            day => Assert.True(day.IsUnset));
        Assert.All(untouched.Days.Where(day => day.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday),
            day => Assert.True(day.IsUnset));
    }

    [Fact]
    public async Task One_stale_row_in_a_batch_rejects_the_whole_batch()
    {
        // A18: a stale expected revision writes nothing at all — not the rows that were current, not a receipt.
        using var factory = ScheduleWriteApi.NewFactory();
        var first = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var second = await AdminSetupApi.UserIdAsync(factory, "1000003");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);
        var committed = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = first, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(2)) } } },
        });
        Assert.Equal(HttpStatusCode.OK, committed.Response.StatusCode);

        var stale = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[]
            {
                new { userId = first, offDates = Array.Empty<string>() },
                new { userId = second, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(3)) } },
            },
        }, new Dictionary<string, string>
        {
            [ScheduleWriteApi.WeekKey(first, Week)] = "1",
            [ScheduleWriteApi.WeekKey(second, Week)] = "1", // stale: that person has no week header at all
        });

        Assert.Equal(HttpStatusCode.Conflict, stale.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Rejected, stale.Answer!.Outcome);
        var problem = Assert.Single(stale.Answer.Problems);
        Assert.Equal(ScheduleCommandErrorCode.StaleRevision, problem.Code);
        Assert.Equal(ScheduleWriteApi.WeekKey(second, Week), problem.RowId);
        Assert.Equal("0", stale.Answer.Revisions[ScheduleWriteApi.WeekKey(second, Week)]);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(first, header.UserId);
            Assert.Equal(new DateOnly(2026, 11, 18), Assert.Single(header.Choices).OffDate);
            Assert.Equal(1, header.Revision);
            Assert.Single(await db.ScheduleChanges.ToListAsync());
            Assert.Single(await db.ScheduleCommandReceipts.ToListAsync());
        });
    }

    [Fact]
    public async Task A_no_op_edit_commits_without_changing_the_history()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);
        var payload = new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(2)) } } },
        };

        var first = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, payload);
        var again = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, payload,
            first.Answer!.Revisions);

        Assert.Equal(HttpStatusCode.OK, first.Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, again.Answer!.Outcome);
        Assert.Equal("1", again.Answer.Revisions[ScheduleWriteApi.WeekKey(person, Week)]);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.ToListAsync());
            Assert.Equal(1, header.Revision);
            var change = Assert.Single(await db.ScheduleChanges.ToListAsync());
            Assert.Equal("create", change.Operation);
            Assert.Equal(2, await db.ScheduleCommandReceipts.CountAsync());
        });
    }

    [Fact]
    public async Task A_regular_baseline_start_must_be_in_the_future_and_keeps_the_earlier_version()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000001", new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var today = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = ScheduleWriteApi.Iso(new DateOnly(2026, 11, 17)),
            days = new[] { new { weekday = "Monday", areaId = area2, breakGroup = 2 } },
        });
        var future = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = "2026-12-02",
            days = new[] { new { weekday = "Tuesday", areaId = area2, breakGroup = 2 } },
        });

        // ADR facility-0187: today is not a future start, so the normal editor refuses it and writes nothing.
        Assert.Equal(HttpStatusCode.Conflict, today.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(today.Answer!.Problems).Code);
        Assert.Equal(HttpStatusCode.OK, future.Response.StatusCode);
        Assert.Equal("1", future.Answer!.Revisions[ScheduleWriteApi.BaselineKey(person, "regular")]);

        await factory.WithDbAsync(async db =>
        {
            var versions = await db.ScheduleBaselines.Include(baseline => baseline.Days)
                .Where(baseline => baseline.UserId == person && baseline.Kind == BaselineKind.Regular)
                .OrderBy(baseline => baseline.EffectiveFrom).ToListAsync();
            Assert.Equal(2, versions.Count);
            // ADR facility-0173: the earlier version keeps its own dates and only its end moves, with its revision bumped.
            Assert.Equal((new DateOnly(2026, 10, 1), (DateOnly?)new DateOnly(2026, 12, 2)), (versions[0].EffectiveFrom, versions[0].EffectiveUntil));
            Assert.Equal(2, versions[0].Revision);
            Assert.Equal((new DateOnly(2026, 12, 2), (DateOnly?)null), (versions[1].EffectiveFrom, versions[1].EffectiveUntil));
            Assert.Equal(1, versions[1].Revision);
            var tuesday = versions[1].Days.Single(day => day.Weekday == DayOfWeek.Tuesday);
            Assert.Equal((area2, (int?)2), (tuesday.AreaId, tuesday.BreakGroup));
            // The weekdays that were not submitted carry the previous version's cells forward.
            Assert.All(versions[1].Days.Where(day => day.Weekday != DayOfWeek.Tuesday),
                day => Assert.Equal((area1, (int?)1), (day.AreaId, day.BreakGroup)));
        });

        var week = await ScheduleWriteApi.WeekAsync(admin, new DateOnly(2026, 12, 7));
        var row = week.Rows.Single(entry => entry.UserId == person);
        Assert.Equal(area2, row.Days.Single(day => day.Date == new DateOnly(2026, 12, 8)).Area.Value);
        Assert.Equal(area1, row.Days.Single(day => day.Date == new DateOnly(2026, 12, 7)).Area.Value);
    }

    [Fact]
    public async Task A_relief_week_pattern_carries_areas_and_no_break_group()
    {
        // Spec §5 and ADR facility-0220: a relief pattern is weekday Area/day-off/unset cells, and the break group is
        // inherited from the replaced person (0152) rather than stored on the pattern.
        using var factory = ScheduleWriteApi.NewFactory();
        var relief = await TestData.AddCleanerAsync(factory, "1000005", Shift.Day);
        await ScheduleWriteApi.AddProfileAsync(factory, "1000005", CleanerKind.Relief, Shift.Day, new DateOnly(2026, 10, 1), "A");
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var committed = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetReliefBaseline, new
        {
            userId = relief,
            effectiveFrom = "2026-12-02",
            days = new object[]
            {
                new { weekday = "Monday", areaId = area1 },
                new { weekday = "Tuesday", isDayOff = true },
            },
        });
        var grouped = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetReliefBaseline, new
        {
            userId = relief,
            effectiveFrom = "2026-12-09",
            days = new[] { new { weekday = "Monday", areaId = area1, breakGroup = 1 } },
        });

        Assert.Equal(HttpStatusCode.OK, committed.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, committed.Answer!.Outcome);
        Assert.Equal(HttpStatusCode.Conflict, grouped.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(grouped.Answer!.Problems).Code);
        await factory.WithDbAsync(async db =>
        {
            var version = Assert.Single(await db.ScheduleBaselines.Include(baseline => baseline.Days).ToListAsync());
            Assert.Equal(BaselineKind.Relief, version.Kind);
            Assert.Equal(2, version.Days.Count);
            Assert.All(version.Days, day => Assert.Null(day.BreakGroup));
            Assert.Equal(area1, version.Days.Single(day => day.Weekday == DayOfWeek.Monday).AreaId);
            Assert.True(version.Days.Single(day => day.Weekday == DayOfWeek.Tuesday).IsDayOff);
        });
    }

    [Fact]
    public async Task A_dated_Area_change_moves_one_date_and_keeps_the_rest_of_the_week()
    {
        // A03: the changed Wednesday moves, the days before it stay, and a second edit of the same value writes nothing.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000001", new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var moved = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = "2026-11-18", shift = "Day", areaId = area2 });
        var again = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = "2026-11-18", shift = "Day", areaId = area2 }, moved.Answer!.Revisions);
        var stale = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = "2026-11-18", shift = "Day", areaId = area1 },
            new Dictionary<string, string> { [ScheduleWriteApi.DatedKey(person, new DateOnly(2026, 11, 18), "Day", "area")] = "0" });

        Assert.Equal(HttpStatusCode.OK, moved.Response.StatusCode);
        Assert.Equal("1", moved.Answer!.Revisions[ScheduleWriteApi.DatedKey(person, new DateOnly(2026, 11, 18), "Day", "area")]);
        Assert.Equal(HttpStatusCode.OK, again.Response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.StaleRevision, Assert.Single(stale.Answer!.Problems).Code);

        var week = await ScheduleWriteApi.WeekAsync(admin, Week);
        var row = week.Rows.Single(entry => entry.UserId == person);
        var wednesday = row.Days.Single(day => day.Date == new DateOnly(2026, 11, 18));
        var tuesday = row.Days.Single(day => day.Date == new DateOnly(2026, 11, 17));
        Assert.Equal(area2, wednesday.Area.Value);
        Assert.Equal("dated", wednesday.Area.SourceKind);
        Assert.Equal(area1, tuesday.Area.Value);
        Assert.Equal("baseline", tuesday.Area.SourceKind);
        await factory.WithDbAsync(async db =>
        {
            var row = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.Equal(1, row.Revision);
            Assert.Single(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task Recording_and_cancelling_leave_keeps_the_row_and_never_writes_attendance()
    {
        // Spec §7 and A04: leave is its own record with a retained history; it is not an attendance fact and it never
        // cancels itself.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000001", new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);
        var (admin, auth, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var recorded = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.RecordLeave, new
        {
            userId = person, date = "2026-11-18", shift = "Day",
            startsAt = "2026-11-18T00:00:00Z", endsAt = "2026-11-18T12:00:00Z", reason = "ลาพักร้อนครึ่งวัน",
        });
        var duplicate = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.RecordLeave, new
        {
            userId = person, date = "2026-11-18", shift = "Day",
            startsAt = "2026-11-18T00:00:00Z", endsAt = "2026-11-18T12:00:00Z",
        });

        Assert.Equal(HttpStatusCode.OK, recorded.Response.StatusCode);
        Assert.Equal("1", recorded.Answer!.Revisions[ScheduleWriteApi.LeaveKey(person, new DateOnly(2026, 11, 18), "Day")]);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(duplicate.Answer!.Problems).Code);
        var onLeave = (await ScheduleWriteApi.WeekAsync(admin, Week)).Rows.Single(row => row.UserId == person)
            .Days.Single(day => day.Date == new DateOnly(2026, 11, 18));
        Assert.True(onLeave.IsLeave);
        Assert.Equal("leave", onLeave.Availability);
        Assert.NotNull(onLeave.Area.Value); // the eligible Area stays visible for the record-first check-in

        var cancelled = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CancelLeave,
            new { userId = person, date = "2026-11-18", shift = "Day", reason = "กลับมาทำงาน" },
            recorded.Answer.Revisions);

        Assert.Equal(HttpStatusCode.OK, cancelled.Response.StatusCode);
        Assert.Equal("2", cancelled.Answer!.Revisions[ScheduleWriteApi.LeaveKey(person, new DateOnly(2026, 11, 18), "Day")]);
        var after = (await ScheduleWriteApi.WeekAsync(admin, Week)).Rows.Single(row => row.UserId == person)
            .Days.Single(day => day.Date == new DateOnly(2026, 11, 18));
        Assert.False(after.IsLeave);
        Assert.Equal("available", after.Availability);
        await factory.WithDbAsync(async db =>
        {
            var leave = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.Equal(ScheduleExceptionKind.Leave, leave.Kind);
            Assert.True(leave.IsCancelled);
            Assert.NotNull(leave.CancelledAt);
            Assert.Equal(auth.User.Id, leave.CancelledById);
            Assert.Equal(2, leave.Revision);
            Assert.Equal(2, await db.ScheduleChanges.CountAsync());
            Assert.Empty(await db.ShiftAttendances.ToListAsync());
        });
    }

    [Fact]
    public async Task A_command_result_is_readable_only_by_the_actor_that_committed_it()
    {
        // Spec §11: the status of an uncertain write is scoped, and it must never enumerate another actor's command.
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var committed = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff,
            new { weekStart = ScheduleWriteApi.Iso(Week), rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(2)) } } } });
        var own = await admin.GetAsync($"/api/schedules/commands/{committed.Answer!.CommandId}");
        var foreign = await supervisor.GetAsync($"/api/schedules/commands/{committed.Answer.CommandId}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var stored = await ScheduleWriteApi.ReadAsync<ScheduleWriteApi.CommandAnswer>(own);
        Assert.Equal(ScheduleCommandOutcome.Committed, stored!.Outcome);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.NotEqual(Guid.Empty, committed.Answer.CommandId);
    }

    [Fact]
    public async Task An_unknown_command_kind_is_refused_without_writing()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(admin, "set-nothing", new { userId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.UnknownKind, answer.Answer!.Outcome);
        await factory.WithDbAsync(async db => Assert.Empty(await db.ScheduleChanges.ToListAsync()));
    }

    [Fact]
    public async Task A_week_start_that_is_not_a_monday_is_normalised_for_the_whole_batch()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff,
            new { weekStart = "2026-11-18", rows = new[] { new { userId = person, offDates = new[] { "2026-11-18" } } } });

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(Week, header.WeekStart);
            Assert.Equal(new DateOnly(2026, 11, 18), Assert.Single(header.Choices).OffDate);
        });
    }

    [Fact]
    public async Task A_day_off_choice_outside_the_submitted_week_is_refused()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(9)) } } },
        });

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Answer!.Problems).Code);
        await factory.WithDbAsync(async db => Assert.Empty(await db.WeeklyDayOffs.ToListAsync()));
    }

    [Fact]
    public async Task A_day_off_is_authorised_by_the_date_it_is_written_not_by_the_week_start()
    {
        // Spec §3: a person whose dated period moves building inside the week is authorised on the identity of each date,
        // so the Supervisor of their old building cannot record Wednesday's day off on Monday's authority.
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddBuildingAsync(factory, "B");
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day,
            new DateOnly(2026, 10, 1), "A", until: Week.AddDays(2));
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day,
            Week.AddDays(2), "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var across = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(1)), ScheduleWriteApi.Iso(Week.AddDays(2)) } } },
        });
        var beforeTheMove = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(1)) } } },
        });

        Assert.Equal(HttpStatusCode.Conflict, across.Response.StatusCode);
        var refused = Assert.Single(across.Answer!.Problems);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, refused.Code);
        Assert.Contains(ScheduleWriteApi.Iso(Week.AddDays(2)), refused.Message);
        Assert.Equal(HttpStatusCode.OK, beforeTheMove.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, beforeTheMove.Answer!.Outcome);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(Week.AddDays(1), Assert.Single(header.Choices).OffDate);
        });
    }

    [Fact]
    public async Task A_dated_identity_that_starts_mid_week_can_still_have_that_weeks_days_off_recorded()
    {
        // The other half of §3: the week header is one fact for the week, so a period that only starts on Wednesday must not
        // make that week's choices impossible to record.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000001", Week.AddDays(2));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(2)) } } },
        });

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer.Answer!.Outcome);
        Assert.Equal("1", answer.Answer.Revisions[ScheduleWriteApi.WeekKey(person, Week)]);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(Week, header.WeekStart);
            Assert.Equal(Week.AddDays(2), Assert.Single(header.Choices).OffDate);
        });
    }

    [Fact]
    public async Task A_malformed_dated_value_is_refused_instead_of_cancelling_the_override()
    {
        // Controller ruling: only an explicit JSON null removes a dated override. A value no removal means ("2", 2.5, true)
        // is an unreadable payload, never a silent cancellation that answers committed.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);
        var live = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = "2026-11-18", shift = "Day", areaId = area2 });
        Assert.Equal(HttpStatusCode.OK, live.Response.StatusCode);

        var asString = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = "2026-11-18", shift = "Day", areaId = "2" });
        var fractional = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = "2026-11-18", shift = "Day", areaId = 2.5 });
        var boolean = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = "2026-11-18", shift = "Day", areaId = true });
        var group = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedBreak,
            new { userId = person, date = "2026-11-18", shift = "Day", breakGroup = "1" });

        Assert.All(new[] { asString, fractional, boolean, group }, answer =>
        {
            Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
            Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Answer!.Problems).Code);
        });
        await factory.WithDbAsync(async db =>
        {
            var row = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.False(row.IsCancelled);
            Assert.Equal(area2, row.AreaId);
            Assert.Equal(1, row.Revision);
            Assert.Equal("create", Assert.Single(await db.ScheduleChanges.ToListAsync()).Operation);
        });
    }

    [Fact]
    public async Task A_malformed_baseline_cell_is_refused_instead_of_being_stored_as_unset()
    {
        // The same three-state rule on a baseline cell: "2" is not areaId 2 and is not "no Area" either.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var areaAsString = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = "2026-12-02",
            days = new[] { new { weekday = "Tuesday", areaId = "2" } },
        });
        var dayOffAsString = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = "2026-12-02",
            days = new[] { new { weekday = "Tuesday", isDayOff = "true" } },
        });
        var spokenNull = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = "2026-12-02",
            days = new object[] { new { weekday = "Tuesday", areaId = (int?)null } },
        });

        Assert.All(new[] { areaAsString, dayOffAsString }, answer =>
        {
            Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
            Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Answer!.Problems).Code);
        });
        // An explicit null is a real value for a cell ("this weekday carries no Area"), so it still stores.
        Assert.Equal(HttpStatusCode.OK, spokenNull.Response.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var versions = await db.ScheduleBaselines.Include(baseline => baseline.Days).ToListAsync();
            Assert.Equal(2, versions.Count);
            var added = versions.Single(version => version.EffectiveFrom == new DateOnly(2026, 12, 2));
            Assert.Null(added.Days.Single(day => day.Weekday == DayOfWeek.Tuesday).AreaId);
        });
    }

    [Fact]
    public async Task A_relief_baseline_batch_commits_both_rows_and_rolls_back_all_when_one_is_stale()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var first = await TestData.AddCleanerAsync(factory, "1000005", Shift.Day);
        var second = await TestData.AddCleanerAsync(factory, "1000006", Shift.Day);
        await ScheduleWriteApi.AddProfileAsync(factory, "1000005", CleanerKind.Relief, Shift.Day, new DateOnly(2026, 10, 1), "A");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000006", CleanerKind.Relief, Shift.Day, new DateOnly(2026, 10, 1), "A");
        var area = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);
        var firstKey = ScheduleWriteApi.BaselineKey(first, "relief");
        var secondKey = ScheduleWriteApi.BaselineKey(second, "relief");
        Task<(HttpResponseMessage Response, ScheduleWriteApi.CommandAnswer? Answer)> Write(string date, string firstRevision, string secondRevision) =>
            ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetReliefBaseline, new
            {
                rows = new[]
                {
                    new { userId = first, effectiveFrom = date, days = new[] { new { weekday = "Monday", areaId = area } } },
                    new { userId = second, effectiveFrom = date, days = new[] { new { weekday = "Tuesday", areaId = area } } },
                },
            }, new Dictionary<string, string> { [firstKey] = firstRevision, [secondKey] = secondRevision });

        var written = await Write("2026-12-01", "0", "0");
        Assert.Equal(HttpStatusCode.OK, written.Response.StatusCode);
        Assert.Equal("1", written.Answer!.Revisions[firstKey]);
        Assert.Equal("1", written.Answer.Revisions[secondKey]);

        var stale = await Write("2026-12-08", "1", "0");
        Assert.Equal(HttpStatusCode.Conflict, stale.Response.StatusCode);
        var problem = Assert.Single(stale.Answer!.Problems);
        Assert.Equal(secondKey, problem.RowId);
        Assert.Equal(ScheduleCommandErrorCode.StaleRevision, problem.Code);
        await factory.WithDbAsync(async db =>
        {
            var patterns = await db.ScheduleBaselines.Where(entry => entry.UserId == first || entry.UserId == second).ToListAsync();
            Assert.Equal(2, patterns.Count);
            Assert.All(patterns, entry => Assert.Null(entry.EffectiveUntil));
            Assert.Equal(2, await db.ScheduleChanges.CountAsync(entry => entry.EntityType == "ScheduleBaseline"));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Baseline_timeline_rejects_stale_clients_after_appending_and_inserting(bool relief)
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = relief
            ? await TestData.AddCleanerAsync(factory, "1000005", Shift.Day)
            : await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        if (relief)
        {
            await ScheduleWriteApi.AddProfileAsync(
                factory, "1000005", CleanerKind.Relief, Shift.Day, new DateOnly(2026, 10, 1), "A");
        }

        var area = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (first, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (second, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var kind = relief ? ScheduleWriteApi.SetReliefBaseline : ScheduleWriteApi.SetRegularBaseline;
        var key = ScheduleWriteApi.BaselineKey(person, relief ? "relief" : "regular");
        async Task<string> ReadRevision(HttpClient client)
        {
            var week = await ScheduleWriteApi.WeekAsync(client, new DateOnly(2026, 12, 14));
            var row = Assert.Single(week.Rows, entry => entry.UserId == person);
            Assert.Equal(row.RegularBaselineRevision, row.ReliefBaselineRevision);
            return relief ? row.ReliefBaselineRevision : row.RegularBaselineRevision;
        }

        Task<(HttpResponseMessage Response, ScheduleWriteApi.CommandAnswer? Answer)> Write(
            HttpClient client, string expected, string from) =>
            ScheduleWriteApi.ExecuteAsync(client, kind, new
            {
                userId = person,
                effectiveFrom = from,
                days = new[] { new { weekday = "Monday", areaId = area } },
            }, new Dictionary<string, string> { [key] = expected });

        async Task<string> Commit(HttpClient client, string expected, string from)
        {
            var written = await Write(client, expected, from);
            Assert.Equal(HttpStatusCode.OK, written.Response.StatusCode);
            Assert.Equal(await ReadRevision(client), written.Answer!.Revisions[key]);
            return written.Answer.Revisions[key];
        }

        async Task AssertStale(string expected, string current, string from)
        {
            var refused = await Write(second, expected, from);
            Assert.Equal(HttpStatusCode.Conflict, refused.Response.StatusCode);
            Assert.Equal(ScheduleCommandErrorCode.StaleRevision, Assert.Single(refused.Answer!.Problems).Code);
            Assert.Equal(current, refused.Answer.Revisions[key]);
        }

        var initial = await ReadRevision(second);
        var firstVersion = await Commit(first, initial, "2026-12-02");
        Assert.NotEqual(initial, firstVersion);
        Assert.Equal(firstVersion, await ReadRevision(second));

        var appended = await Commit(first, firstVersion, "2026-12-23");
        Assert.NotEqual(firstVersion, appended);
        await AssertStale(firstVersion, appended, "2026-12-30");
        Assert.Equal(appended, await ReadRevision(second));

        var inserted = await Commit(first, appended, "2026-12-09");
        Assert.NotEqual(appended, inserted);
        await AssertStale(appended, inserted, "2026-12-16");
        Assert.Equal(inserted, await Commit(first, inserted, "2026-12-09")); // exact no-op

        await factory.WithDbAsync(async db =>
        {
            var starts = await db.ScheduleBaselines.Where(version => version.UserId == person)
                .OrderBy(version => version.EffectiveFrom).Select(version => version.EffectiveFrom).ToListAsync();
            Assert.Contains(new DateOnly(2026, 12, 9), starts);
            Assert.DoesNotContain(new DateOnly(2026, 12, 16), starts);
            Assert.DoesNotContain(new DateOnly(2026, 12, 30), starts);
        });
    }

    [Fact]
    public async Task The_week_read_names_each_days_shift_so_a_write_can_identify_the_row()
    {
        // Spec §4 and §11: an Admin reads every building and clock shift, and a write has to name the shift of the date it
        // edits. A person's shift can change inside one week, so the read carries it per day.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day,
            new DateOnly(2026, 10, 1), "A", until: Week.AddDays(2));
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Night, Week.AddDays(2), "A");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var week = await ScheduleWriteApi.WeekAsync(admin, Week);
        var days = week.Rows.Single(entry => entry.UserId == person).Days;
        Assert.Equal("Day", days.Single(day => day.Date == Week.AddDays(1)).Shift);
        Assert.Equal("Night", days.Single(day => day.Date == Week.AddDays(2)).Shift);

        var guessed = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = ScheduleWriteApi.Iso(Week.AddDays(2)), shift = "Day", areaId = area2 });
        var named = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = ScheduleWriteApi.Iso(Week.AddDays(2)), shift = "Night", areaId = area2 });

        Assert.Equal(HttpStatusCode.Conflict, guessed.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(guessed.Answer!.Problems).Code);
        Assert.Equal(HttpStatusCode.OK, named.Response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_dated_Area_keeps_the_row_and_falls_back_to_the_baseline()
    {
        // Spec §5 and ADR facility-0202/0189: an explicit null cancels the override instead of deleting it, and the week
        // then resolves from the baseline again.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (admin, auth, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var created = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = "2026-11-18", shift = "Day", areaId = area2 });
        var cancelled = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = "2026-11-18", shift = "Day", areaId = (int?)null },
            created.Answer!.Revisions);

        Assert.Equal(HttpStatusCode.OK, cancelled.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, cancelled.Answer!.Outcome);
        Assert.Equal("2", cancelled.Answer.Revisions[ScheduleWriteApi.DatedKey(person, new DateOnly(2026, 11, 18), "Day", "area")]);
        await factory.WithDbAsync(async db =>
        {
            var row = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.True(row.IsCancelled);
            Assert.NotNull(row.CancelledAt);
            Assert.Equal(auth.User.Id, row.CancelledById);
            Assert.Equal(area2, row.AreaId);
            Assert.Equal(2, row.Revision);
            var changes = await db.ScheduleChanges.OrderBy(change => change.Id).ToListAsync();
            Assert.Equal(2, changes.Count);
            Assert.Equal("cancel", changes[1].Operation);
        });

        var day = (await ScheduleWriteApi.WeekAsync(admin, Week)).Rows.Single(row => row.UserId == person)
            .Days.Single(entry => entry.Date == new DateOnly(2026, 11, 18));
        Assert.Equal(area1, day.Area.Value);
        Assert.Equal("baseline", day.Area.SourceKind);
    }

    [Fact]
    public async Task Cancelling_a_leave_that_does_not_exist_is_refused_and_writes_nothing()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CancelLeave,
            new { userId = person, date = "2026-11-18", shift = "Day", reason = "ยกเลิกการลา" });

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Answer!.Problems).Code);
        Assert.Equal("0", answer.Answer.Revisions[ScheduleWriteApi.LeaveKey(person, new DateOnly(2026, 11, 18), "Day")]);
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.ScheduleExceptions.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
            Assert.Empty(await db.ScheduleCommandReceipts.ToListAsync());
        });
    }

    [Fact]
    public async Task A_baseline_that_would_strand_a_dated_override_is_refused_with_the_affected_date()
    {
        // Plan Task 6: "incompatible dated settings return affected-date conflicts". A version that would leave a live dated
        // override on a day-off cell, or a break group on a cell with no Area, is refused for each affected date instead of
        // being stored and stranding the override; a compatible version still keeps both overrides.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001", breakGroup: 1);
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        ScheduleWriteApi.Today(factory);
        var areaDate = new DateOnly(2026, 12, 15);   // a Tuesday
        var groupDate = new DateOnly(2026, 12, 16);  // a Wednesday

        var datedArea = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedArea,
            new { userId = person, date = ScheduleWriteApi.Iso(areaDate), shift = "Day", areaId = area2 });
        var datedGroup = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetDatedBreak,
            new { userId = person, date = ScheduleWriteApi.Iso(groupDate), shift = "Day", breakGroup = 2 });
        Assert.Equal(HttpStatusCode.OK, datedArea.Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, datedGroup.Response.StatusCode);

        var dayOffTuesday = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = "2026-12-02",
            days = new[] { new { weekday = "Tuesday", isDayOff = true } },
        });
        var areaLessWednesday = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = "2026-12-02",
            days = new object[]
            {
                new { weekday = "Tuesday", areaId = area1 },
                new { weekday = "Wednesday" },
            },
        });

        Assert.Equal(HttpStatusCode.Conflict, dayOffTuesday.Response.StatusCode);
        Assert.Contains(dayOffTuesday.Answer!.Problems,
            problem => problem.Code == ScheduleCommandErrorCode.RowInvalid
                && problem.RowId.Contains(ScheduleWriteApi.Iso(areaDate), StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Conflict, areaLessWednesday.Response.StatusCode);
        Assert.Contains(areaLessWednesday.Answer!.Problems,
            problem => problem.Code == ScheduleCommandErrorCode.RowInvalid
                && problem.RowId.Contains(ScheduleWriteApi.Iso(groupDate), StringComparison.Ordinal));
        await factory.WithDbAsync(async db => Assert.Single(await db.ScheduleBaselines.ToListAsync()));

        var compatible = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = person,
            effectiveFrom = "2026-12-02",
            days = new[] { new { weekday = "Tuesday", areaId = area1, breakGroup = 2 } },
        });

        Assert.Equal(HttpStatusCode.OK, compatible.Response.StatusCode);
        var week = await ScheduleWriteApi.WeekAsync(admin, new DateOnly(2026, 12, 14));
        var days = week.Rows.Single(row => row.UserId == person).Days;
        var overriddenArea = days.Single(day => day.Date == areaDate);
        var overriddenGroup = days.Single(day => day.Date == groupDate);
        Assert.Equal((int?)area2, overriddenArea.Area.Value);
        Assert.Equal("dated", overriddenArea.Area.SourceKind);
        Assert.Equal((int?)2, overriddenGroup.BreakGroup.Value);
        Assert.Equal("dated", overriddenGroup.BreakGroup.SourceKind);
    }

    [Fact]
    public async Task A_clear_is_authorised_by_every_date_it_removes_not_by_one_date_of_the_week()
    {
        // Controller ruling: an empty submission deletes choices the client never named, so each date it removes must pass the
        // same per-date scope check a submitted date passes. This Supervisor holds building A on Monday and Tuesday, which is
        // authority to edit those two days — not authority to delete a Wednesday the person now works in building B.
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddBuildingAsync(factory, "B");
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day,
            new DateOnly(2026, 10, 1), "A", until: Week.AddDays(2));
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, Week.AddDays(2), "B");
        await ScheduleWriteApi.AddStoredWeekAsync(factory, "1000001", Week, Week.AddDays(2));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = Array.Empty<string>() } },
        });

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Rejected, answer.Answer!.Outcome);
        var problem = Assert.Single(answer.Answer.Problems);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, problem.Code);
        Assert.Contains(ScheduleWriteApi.Iso(Week.AddDays(2)), problem.Message);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(1, header.Revision);
            Assert.Equal(Week.AddDays(2), Assert.Single(header.Choices).OffDate);
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task An_actor_with_week_wide_authority_can_clear_the_stored_week()
    {
        // The other half of the ruling: the dates the clear removes are all inside this Supervisor's scope, so the week is
        // emptied as one write — the header keeps its row, its revision moves and one audit row records the before/after.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);
        var key = ScheduleWriteApi.WeekKey(person, Week);

        var chosen = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[]
            {
                new
                {
                    userId = person,
                    offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(2)), ScheduleWriteApi.Iso(Week.AddDays(4)) },
                },
            },
        });
        var cleared = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = Array.Empty<string>() } },
        }, chosen.Answer!.Revisions);

        Assert.Equal(HttpStatusCode.OK, chosen.Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cleared.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, cleared.Answer!.Outcome);
        Assert.Equal("2", cleared.Answer.Revisions[key]);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(2, header.Revision);
            Assert.Empty(header.Choices);
            var changes = await db.ScheduleChanges.OrderBy(change => change.Id).ToListAsync();
            Assert.Equal(2, changes.Count);
            Assert.Equal("update", changes[1].Operation);
            using var before = JsonDocument.Parse(changes[1].BeforeJson!);
            Assert.Equal(2, before.RootElement.GetProperty("offDates").GetArrayLength());
            using var after = JsonDocument.Parse(changes[1].AfterJson!);
            Assert.Empty(after.RootElement.GetProperty("offDates").EnumerateArray());
            Assert.Equal("2", after.RootElement.GetProperty("revision").GetString());
        });

        var week = await ScheduleWriteApi.WeekAsync(supervisor, Week);
        Assert.All(week.Rows.Single(row => row.UserId == person).Days, day => Assert.False(day.IsDayOff));
    }

    [Fact]
    public async Task An_existing_stored_week_can_be_edited_so_one_choice_replaces_another()
    {
        // The update half of the same command: a header that already exists keeps its row and takes the new set, so the drop
        // and the insert are stored as one revision and the audit keeps the before side.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);
        var key = ScheduleWriteApi.WeekKey(person, Week);

        var chosen = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(2)) } } },
        });
        var moved = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(4)) } } },
        }, chosen.Answer!.Revisions);

        Assert.Equal(HttpStatusCode.OK, moved.Response.StatusCode);
        Assert.Equal("2", moved.Answer!.Revisions[key]);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(2, header.Revision);
            Assert.Equal(Week.AddDays(4), Assert.Single(header.Choices).OffDate);
            var changes = await db.ScheduleChanges.OrderBy(change => change.Id).ToListAsync();
            Assert.Equal(2, changes.Count);
            using var before = JsonDocument.Parse(changes[1].BeforeJson!);
            Assert.Equal(ScheduleWriteApi.Iso(Week.AddDays(2)),
                Assert.Single(before.RootElement.GetProperty("offDates").EnumerateArray()).GetString());
        });

        var week = await ScheduleWriteApi.WeekAsync(supervisor, Week);
        var days = week.Rows.Single(row => row.UserId == person).Days;
        Assert.False(days.Single(day => day.Date == Week.AddDays(2)).IsDayOff);
        Assert.True(days.Single(day => day.Date == Week.AddDays(4)).IsDayOff);
    }

    [Fact]
    public async Task An_actor_with_part_of_the_weeks_authority_cannot_confirm_an_empty_week()
    {
        // With nothing stored, "no days off this week" is an assertion about all seven dates — one authorised Monday is not
        // enough to write it.
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddBuildingAsync(factory, "B");
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day,
            new DateOnly(2026, 10, 1), "A", until: Week.AddDays(2));
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, Week.AddDays(2), "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = Array.Empty<string>() } },
        });

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        var problem = Assert.Single(answer.Answer!.Problems);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, problem.Code);
        Assert.Contains(ScheduleWriteApi.Iso(Week.AddDays(2)), problem.Message);
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_partly_scoped_actor_cannot_drop_a_stored_choice_outside_their_scope_by_omitting_it()
    {
        // Controller ruling: the payload replaces the stored selection rather than merging into it, so a non-empty
        // submission deletes every stored choice it omits. The stored Wednesday is a date this person moved to building B
        // on, so it is not this Supervisor's to delete — even though the Monday they did submit is. Old code authorised the
        // omission by the dates it submitted and deleted the Wednesday without a lock on it.
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddBuildingAsync(factory, "B");
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day,
            new DateOnly(2026, 10, 1), "A", until: Week.AddDays(2));
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, Week.AddDays(2), "B");
        await ScheduleWriteApi.AddStoredWeekAsync(factory, "1000001", Week, Week.AddDays(2));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var scopedRead = await ScheduleWriteApi.WeekAsync(supervisor, Week);
        var scopedPerson = Assert.Single(scopedRead.Rows, row => row.UserId == person);
        Assert.False(scopedPerson.WeeklyReplaceAllowed);
        Assert.DoesNotContain(scopedPerson.Days, day => day.Date == Week.AddDays(2));

        // The submitted Monday is a past date, so a reason travels with the command: the refusal below has to be the
        // Wednesday's missing authority, never a missing reason.
        var answer = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week) } } },
            reason = "เลื่อนวันหยุด",
        });

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Rejected, answer.Answer!.Outcome);
        var problem = Assert.Single(answer.Answer.Problems);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, problem.Code);
        Assert.Contains(ScheduleWriteApi.Iso(Week.AddDays(2)), problem.Message);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(1, header.Revision);
            Assert.Equal(Week.AddDays(2), Assert.Single(header.Choices).OffDate);
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task An_actor_whose_scope_holds_the_whole_week_can_drop_stored_choices_by_omitting_them()
    {
        // The other half of the ruling: every stored date this submission drops is inside the Supervisor's own building, so
        // the replacement is one ordinary edit — revision 2, one audit row keeping the before side, no reason because the
        // date submitted (Tuesday, today) and the dates dropped are not before today's business date.
        using var factory = ScheduleWriteApi.NewFactory();
        var person = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001");
        await ScheduleWriteApi.AddStoredWeekAsync(factory, "1000001", Week, Week.AddDays(2), Week.AddDays(4));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);
        var key = ScheduleWriteApi.WeekKey(person, Week);

        var answer = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = new[] { ScheduleWriteApi.Iso(Week.AddDays(1)) } } },
        });

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer.Answer!.Outcome);
        Assert.Equal("2", answer.Answer.Revisions[key]);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(2, header.Revision);
            Assert.Equal(Week.AddDays(1), Assert.Single(header.Choices).OffDate);
            var change = Assert.Single(await db.ScheduleChanges.ToListAsync());
            Assert.Equal("update", change.Operation);
            Assert.Null(change.Reason);
            using var before = JsonDocument.Parse(change.BeforeJson!);
            Assert.Equal(2, before.RootElement.GetProperty("offDates").GetArrayLength());
        });

        var week = await ScheduleWriteApi.WeekAsync(supervisor, Week);
        var days = week.Rows.Single(row => row.UserId == person).Days;
        Assert.True(days.Single(day => day.Date == Week.AddDays(1)).IsDayOff);
        Assert.False(days.Single(day => day.Date == Week.AddDays(2)).IsDayOff);
    }

    [Fact]
    public async Task A_stored_week_with_no_choices_resubmitted_empty_is_a_no_op()
    {
        // Finding E: "nothing would change" is decided for every shape, so a saved header that already asserts "no days off"
        // is never judged by the all-seven-dates check — it answers committed with no write and names the revision that was
        // already there. Old code answered 409 for this partly scoped Supervisor before it compared anything.
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddBuildingAsync(factory, "B");
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day,
            new DateOnly(2026, 10, 1), "A", until: Week.AddDays(2));
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, Week.AddDays(2), "B");
        await ScheduleWriteApi.AddStoredWeekAsync(factory, "1000001", Week);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory);

        var answer = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetWeeklyDaysOff, new
        {
            weekStart = ScheduleWriteApi.Iso(Week),
            rows = new[] { new { userId = person, offDates = Array.Empty<string>() } },
        });

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer.Answer!.Outcome);
        Assert.Equal("1", answer.Answer.Revisions[ScheduleWriteApi.WeekKey(person, Week)]);
        await factory.WithDbAsync(async db =>
        {
            var header = Assert.Single(await db.WeeklyDayOffs.Include(week => week.Choices).ToListAsync());
            Assert.Equal(1, header.Revision);
            Assert.Empty(header.Choices);
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }
}

/// <summary>
/// The HTTP contract and the test rows the two write test files share. Every request here is what a screen would send: a
/// command identity from <c>POST /api/schedules/command-tokens</c>, then the envelope with its expected revisions.
/// </summary>
internal static class ScheduleWriteApi
{
    /// <summary>A synthetic key: the real one is configuration, never a default in product code.</summary>
    public const string TestKey = "synthetic-scheduling-command-key-32-bytes-minimum";

    public const string SetWeeklyDaysOff = "set-weekly-days-off";
    public const string SetRegularBaseline = "set-regular-baseline";
    public const string SetReliefBaseline = "set-relief-baseline";
    public const string SetDatedArea = "set-dated-area";
    public const string SetDatedBreak = "set-dated-break";
    public const string RecordLeave = "record-leave";
    public const string CancelLeave = "cancel-leave";
    public const string CorrectPlan = "correct-plan";

    public static readonly DateOnly Week = new(2026, 11, 16);

    public static JanitorialApiFactory NewFactory() => new()
    {
        ConfigureTestServices = services => services.AddSingleton(new SchedulingSettings(TestKey)
        {
            HistoryStartsAt = new DateOnly(2026, 10, 1),
        }),
    };

    /// <summary>Asia/Bangkok on 17 November 2026, so the business date of every write test is Tuesday the 17th.</summary>
    public static void Today(JanitorialApiFactory factory) =>
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.FromHours(7)));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string WeekKey(int userId, DateOnly weekStart) => $"week:{userId}:{Iso(weekStart)}";

    public static string BaselineKey(int userId, string kind) => $"baseline:{userId}:{kind}";

    public static string DatedKey(int userId, DateOnly date, string shift, string kind) =>
        $"exception:{userId}:{Iso(date)}:{shift}:{kind}";

    public static string LeaveKey(int userId, DateOnly date, string shift) => $"leave:{userId}:{Iso(date)}:{shift}";

    public static async Task<TokenAnswer?> IssueAsync(HttpClient client) =>
        await ReadAsync<TokenAnswer>(await client.PostAsync("/api/schedules/command-tokens", null));

    public static async Task<(HttpResponseMessage Response, CommandAnswer? Answer)> ExecuteAsync(
        HttpClient client, string kind, object payload, Dictionary<string, string>? expected = null)
    {
        var issued = await IssueAsync(client);
        Assert.NotNull(issued);
        var response = await client.PostAsJsonAsync("/api/schedules/commands", new
        {
            token = issued!.Token,
            commandId = issued.CommandId,
            expectedRevisions = expected ?? new Dictionary<string, string>(),
            kind,
            payload,
        });
        return (response, await ReadAsync<CommandAnswer>(response));
    }

    /// <summary>
    /// The answer's body whatever its status: a refused command is a 409 carrying the row problems, and a test that only
    /// parsed successful answers could not tell a refusal from an empty one.
    /// </summary>
    public static async Task<T?> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(body) ? default : JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<WeekAnswer> ReadWeekAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<WeekAnswer>())!;

    public static async Task<WeekAnswer> WeekAsync(HttpClient client, DateOnly weekStart)
    {
        var response = await client.GetAsync($"/api/schedules?weekStart={Iso(weekStart)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadWeekAsync(response);
    }

    /// <summary>A person's dated identity from <paramref name="from"/> in a building, as the migration backfill writes it.</summary>
    public static Task AddProfileAsync(
        JanitorialApiFactory factory, string employeeId, CleanerKind kind, Shift shift, DateOnly from,
        string? buildingCode = "A", DateOnly? until = null) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(user => user.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            var building = buildingCode is null ? null : await db.Buildings.SingleAsync(entry => entry.Code == buildingCode);
            db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
            {
                UserId = person.Id, Kind = kind, HomeBuildingId = building?.Id, Shift = shift,
                EffectiveFrom = from, EffectiveUntil = until, Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    /// <summary>A regular identity in building A, which is what every scenario's roster row starts from.</summary>
    public static Task AddWeekdayProfileAsync(JanitorialApiFactory factory, string employeeId, DateOnly from) =>
        AddProfileAsync(factory, employeeId, CleanerKind.Regular, Shift.Day, from);

    /// <summary>
    /// A person with a dated regular identity and a Monday-to-Friday baseline from <paramref name="from"/>, in the Area
    /// their account already names — the shape the migration backfill produces (spec §14).
    /// </summary>
    public static async Task<int> SetUpWeekAsync(
        JanitorialApiFactory factory, string employeeId, DateOnly? from = null, int? breakGroup = null)
    {
        var start = from ?? new DateOnly(2026, 10, 1);
        var areaCode = await AdminSetupApi.ReadAsync(factory, db => db.Users
            .Where(user => user.EmployeeId == employeeId).Select(user => user.Area!.Code).SingleAsync());
        await AddWeekdayProfileAsync(factory, employeeId, start);
        await AddWeekdayBaselineAsync(factory, employeeId, start, areaCode, breakGroup);
        return await AdminSetupApi.UserIdAsync(factory, employeeId);
    }

    /// <summary>A baseline version whose Monday-to-Friday cells work in <paramref name="areaCode"/>; the weekend stays unset.</summary>
    public static Task AddWeekdayBaselineAsync(
        JanitorialApiFactory factory, string employeeId, DateOnly from, string areaCode, int? breakGroup = null) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(user => user.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            var area = await db.Areas.SingleAsync(entry => entry.Code == areaCode);
            var baseline = new ScheduleBaseline
            {
                UserId = person.Id, Kind = BaselineKind.Regular, EffectiveFrom = from, Revision = 1,
                CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            foreach (var weekday in new[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
            })
            {
                baseline.Days.Add(new ScheduleBaselineDay { Weekday = weekday, AreaId = area.Id, BreakGroup = breakGroup });
            }

            db.ScheduleBaselines.Add(baseline);
            await db.SaveChangesAsync();
        });

    /// <summary>A week choice somebody already saved: the stored fact a later empty submission would delete.</summary>
    public static Task AddStoredWeekAsync(
        JanitorialApiFactory factory, string employeeId, DateOnly weekStart, params DateOnly[] offDates) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(user => user.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            var header = new WeeklyDayOff
            {
                UserId = person.Id, WeekStart = weekStart, Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            foreach (var date in offDates)
            {
                header.Choices.Add(new WeeklyDayOffChoice { OffDate = date });
            }

            db.WeeklyDayOffs.Add(header);
            await db.SaveChangesAsync();
        });

    /// <summary>A stored dated break-group override, as the selected-date editor writes it.</summary>
    public static Task AddDatedBreakAsync(
        JanitorialApiFactory factory, string employeeId, DateOnly date, int breakGroup) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(user => user.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            db.ScheduleExceptions.Add(new ScheduleException
            {
                UserId = person.Id, Kind = ScheduleExceptionKind.BreakGroup, ShiftDate = date, Shift = Shift.Day,
                BreakGroup = breakGroup, Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    /// <summary>A recorded ShiftIn, so a correction test can prove the actual facts are not rewritten.</summary>
    public static Task AddCheckInAsync(JanitorialApiFactory factory, string employeeId, DateOnly date, Shift shift) =>
        TestData.AddAttendanceAsync(factory, employeeId, date, shift, AttendanceEvent.ShiftIn,
            new DateTimeOffset(date.ToDateTime(new TimeOnly(7, 5)), TimeSpan.FromHours(7)));

    public sealed record CommandAnswer(
        Guid CommandId, string Outcome, Dictionary<string, string> Revisions, List<ProblemAnswer> Problems);

    public sealed record ProblemAnswer(string RowId, string Code, string Message);

    public sealed record TokenAnswer(Guid CommandId, string Token, DateTimeOffset ExpiresAt);

    public sealed record WeekAnswer(
        DateOnly WeekStart, string Revision, DateTimeOffset ReadAt, string Availability, List<PersonAnswer> Rows);

    public sealed record PersonAnswer(
        int UserId, string DisplayName, string CleanerKind, int? HomeBuildingId, bool HasWeeklyChoice,
        string WeeklyRevision, string RegularBaselineRevision, string ReliefBaselineRevision, List<DayAnswer> Days, bool? WeeklyReplaceAllowed = null);

    public sealed record DayAnswer(
        DateOnly Date, ValueAnswer Area, ValueAnswer BreakGroup, bool IsDayOff, bool IsLeave, bool IsUnset,
        string Availability, List<ProblemAnswer> Problems, string? Shift = null, ScheduleEditFacts? Edit = null);

    public sealed record ValueAnswer(int? Value, int? SourceId, string SourceKind, string Revision);
}
