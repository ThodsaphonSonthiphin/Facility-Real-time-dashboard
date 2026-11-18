using System.Net;
using System.Net.Http.Json;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Attendance;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §8 and §11: <c>POST /api/team-attendance/missing</c>. A Supervisor inside their dated scope fills a missing actual
/// time with a reason, an Admin keeps its wider correction rights, and nobody else may touch the person's slot — the refusal
/// never says anything about that person's schedule. A scan is never overwritten here, a stale revision is answered with the
/// current state, and a skip marker for the very action is resolved by the real time.
/// </summary>
public class MissingTeamAttendanceTests
{
    private static readonly DateOnly Day8 = EffectiveAttendanceApi.Day8;

    [Fact]
    public async Task Unauthenticated_missing_time_entry_is_rejected()
    {
        using var factory = new JanitorialApiFactory();
        var response = await factory.CreateApiClient().PostAsJsonAsync("/api/team-attendance/missing", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_cleaner_cannot_add_a_missing_time()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 12, 0));

        var response = await TeamAttendanceApi.AddAsync(cleaner, auth.User.Id, "ShiftIn", "07:00", expectedRevision: "x");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await EffectiveAttendanceApi.FactsAsync(factory));
    }

    [Fact]
    public async Task An_in_scope_supervisor_adds_a_missing_action_with_reason_and_audit()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 7, 0));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (supervisor, supervisorAuth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 12, 0));
        var revision = await TeamAttendanceApi.RevisionAsync(factory, person);

        var response = await TeamAttendanceApi.AddAsync(supervisor, person, "BreakOut", "10:05", expectedRevision: revision);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var state = (await response.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Equal("recorded", state.Action("BreakOut").State);
        Assert.Equal("SupervisorAdd", state.Action("BreakOut").Source);
        Assert.Equal(EffectiveAttendanceApi.Thai(8, 10, 5).UtcDateTime, state.Action("BreakOut").OccurredAt);

        var added = (await EffectiveAttendanceApi.FactsAsync(factory)).Single(fact => fact.EventType == AttendanceEvent.BreakOut);
        Assert.Equal(AttendanceSource.SupervisorAdd, added.Source);
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR01"), added.AreaId);
        Assert.Null(added.SignId);
        Assert.Null(added.WithinRadius);
        Assert.Equal(EffectiveAttendanceApi.Thai(8, 10, 5).UtcDateTime, added.OccurredAt);

        var audit = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ATTENDANCE_ADD", audit.Action);
        Assert.Equal(supervisorAuth.User.Id, audit.ActorId);
        Assert.Equal("shift_attendances", audit.EntityType);
        Assert.Equal(added.Id, audit.EntityId);
        Assert.Equal("ลืมสแกน หัวหน้ายืนยัน", audit.Reason);
        Assert.Null(audit.BeforeJson);
        Assert.Contains("SupervisorAdd", audit.AfterJson);
        Assert.Contains("10:05", audit.Summary);
    }

    [Fact]
    public async Task An_admin_may_add_for_any_building_and_a_supervisor_outside_their_scope_may_not()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var night = await AdminSetupApi.UserIdAsync(factory, "1000002");
        await TestData.AddAttendanceAsync(factory, "1000002", Day8, Shift.Night, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 19, 5));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(9, 9, 0));
        var revision = await TeamAttendanceApi.RevisionAsync(factory, night, Shift.Night);

        // 1000002 works building A's night shift; this Supervisor holds only its day shift.
        var refused = await TeamAttendanceApi.AddAsync(supervisor, night, "BreakOut", "22:10",
            expectedRevision: revision, shift: "Night");
        var allowed = await TeamAttendanceApi.AddAsync(admin, night, "BreakOut", "22:10",
            expectedRevision: revision, shift: "Night");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        var message = await refused.Content.ReadAsStringAsync();
        Assert.DoesNotContain("AR01", message);
        Assert.DoesNotContain("1000002", message);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(2, (await EffectiveAttendanceApi.FactsAsync(factory)).Count);
        // Only the Admin's entry is audited: the refused attempt wrote nothing at all.
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task An_existing_scan_is_never_overwritten()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 7, 0).AddSeconds(23));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 12, 0));
        var revision = await TeamAttendanceApi.RevisionAsync(factory, person);

        var response = await TeamAttendanceApi.AddAsync(supervisor, person, "ShiftIn", "06:58", expectedRevision: revision);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stored = Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Equal(EffectiveAttendanceApi.Thai(8, 7, 0).AddSeconds(23).UtcDateTime, stored.OccurredAt);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_stale_revision_is_refused_with_the_current_state()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 7, 0));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 12, 0));

        var response = await TeamAttendanceApi.AddAsync(supervisor, person, "BreakOut", "10:05",
            expectedRevision: new string('a', 64));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var conflict = (await response.Content.ReadFromJsonAsync<StateConflictAnswer>())!;
        Assert.Equal("refresh-required", conflict.Code);
        Assert.Equal(await TeamAttendanceApi.RevisionAsync(factory, person), conflict.State.StateRevision);
        Assert.Equal("recorded", conflict.State.Action("ShiftIn").State);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_previously_missing_action_cannot_be_filled_after_leave_makes_it_inapplicable()
    {
        using var factory = new JanitorialApiFactory();
        var start = new DateOnly(2026, 10, 1);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, start);
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, start);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", start, "AR01", breakGroup: 1);
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var departure = EffectiveAttendanceApi.Thai(8, 13, 0);
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 7, 0));
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftOut, departure);
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 18, 0));

        var read = await TeamAttendanceApi.StateAsync(supervisor, person);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var draft = (await read.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Contains("Break2Out", draft.FillableActions!);
        Assert.NotNull(draft.StateRevision);

        await EffectiveAttendanceApi.AddLeaveAsync(factory, "1000001", Day8, Shift.Day, departure,
            EffectiveAttendanceApi.Thai(8, 19, 0));

        // A submitted time can still be in actual event order, but the action is now inapplicable under the current plan.
        var response = await TeamAttendanceApi.AddAsync(supervisor, person, "Break2Out", "12:30",
            expectedRevision: draft.StateRevision);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<StateConflictAnswer>())!;
        Assert.Equal("refresh-required", result.Code);
        Assert.Equal(draft.StateRevision, result.State.StateRevision);
        Assert.Equal("not-applicable", result.State.Action("Break2Out").State);
        Assert.DoesNotContain("Break2Out", result.State.FillableActions!);
        Assert.DoesNotContain(await EffectiveAttendanceApi.FactsAsync(factory), fact => fact.EventType == AttendanceEvent.Break2Out);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Theory]
    [InlineData("BreakOut", "21:00", "ลืมสแกน")]        // outside the receiving window
    [InlineData("BreakOut", "20:00", "ลืมสแกน")]        // inside it, but still in the future
    [InlineData("BreakOut", "06:30", "ลืมสแกน")]        // before the recorded check-in
    [InlineData("BreakOut", "7:00", "ลืมสแกน")]         // not HH:mm
    [InlineData("BreakOut", "10:05", "   ")]            // no reason
    [InlineData("BreakOut", "10:05", null)]
    public async Task An_invalid_missing_time_is_refused_and_writes_nothing(string eventType, string time, string? reason)
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 7, 0));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 19, 0));
        var revision = await TeamAttendanceApi.RevisionAsync(factory, person);

        var response = await TeamAttendanceApi.AddAsync(supervisor, person, eventType, time, reason, revision);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_missing_reason_longer_than_the_existing_limit_is_refused()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 12, 0));
        var revision = await TeamAttendanceApi.RevisionAsync(factory, person);

        var response = await TeamAttendanceApi.AddAsync(
            supervisor, person, "ShiftIn", "07:00", new string('x', 501), revision);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await EffectiveAttendanceApi.FactsAsync(factory));
    }

    [Fact]
    public async Task Adding_the_action_resolves_a_stored_skip_marker()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 7, 0));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        // The Cleaner explicitly skipped the first break; the Supervisor now confirms the real time.
        await factory.WithDbAsync(async db =>
        {
            db.AttendanceGaps.Add(new AttendanceGap
            {
                UserId = person,
                ShiftDate = Day8,
                Shift = Shift.Day,
                EventType = AttendanceEvent.BreakOut,
                Status = AttendanceGapStatus.Skipped,
                AreaId = await db.Areas.Where(area => area.Code == "AR01").Select(area => area.Id).SingleAsync(),
                RecordedById = person,
                RecordedAt = EffectiveAttendanceApi.Thai(8, 9, 0).UtcDateTime,
                Revision = 1,
                CreatedAt = EffectiveAttendanceApi.Thai(8, 9, 0).UtcDateTime,
            });
            await db.SaveChangesAsync();
        });
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 12, 0));
        var revision = await TeamAttendanceApi.RevisionAsync(factory, person);

        var response = await TeamAttendanceApi.AddAsync(supervisor, person, "BreakOut", "10:05", expectedRevision: revision);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var gap = Assert.Single(await EffectiveAttendanceApi.GapsAsync(factory));
        Assert.Equal(AttendanceGapStatus.Resolved, gap.Status);
        Assert.Equal(2, gap.Revision);
        // Resolved by the recorded fact itself, so it names no actor of its own.
        Assert.Null(gap.RecordedById);
        Assert.Equal(2, (await EffectiveAttendanceApi.FactsAsync(factory)).Count);
    }

    [Fact]
    public async Task A_numbered_action_is_refused_on_a_legacy_slot_and_writes_nothing()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 7, 0));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 12, 0));
        var revision = await TeamAttendanceApi.RevisionAsync(factory, person);

        // The slot has no lifecycle row, so it is the legacy four: a numbered action is not one of its actions at all.
        var response = await TeamAttendanceApi.AddAsync(supervisor, person, "Break1Out", "10:05", expectedRevision: revision);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "เวลาต้องเรียงกัน: เข้างาน → ออกพัก → กลับจากพัก → เลิกงาน",
            (await response.Content.ReadFromJsonAsync<MessageModel>())!.Message);
        var stored = Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Equal(AttendanceEvent.ShiftIn, stored.EventType);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task The_scoped_read_answers_the_same_revision_inside_scope_and_nothing_outside_it()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 7, 0));
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 12, 0));

        var read = await TeamAttendanceApi.StateAsync(supervisor, person);
        var outside = await TeamAttendanceApi.StateAsync(supervisor, person, shift: "Night");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var state = (await read.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Equal(await TeamAttendanceApi.RevisionAsync(factory, person), state.StateRevision);
        Assert.Equal("recorded", state.Action("ShiftIn").State);
        Assert.DoesNotContain("ShiftIn", state.FillableActions!);
        Assert.Contains("BreakOut", state.FillableActions!);
        // Outside the Supervisor's dated shift, and for a Cleaner, the answer is the same refusal with no detail.
        Assert.Equal(HttpStatusCode.Forbidden, outside.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TeamAttendanceApi.StateAsync(cleaner, person)).StatusCode);
    }
}

/// <summary>The scoped missing-time calls and the revision a screen sends back.</summary>
internal static class TeamAttendanceApi
{
    public static Task<HttpResponseMessage> AddAsync(
        HttpClient client,
        int userId,
        string eventType,
        string time,
        string? reason = "ลืมสแกน หัวหน้ายืนยัน",
        string expectedRevision = "",
        string shiftDate = "2026-10-08",
        string shift = "Day") =>
        client.PostAsJsonAsync("/api/team-attendance/missing", new
        {
            userId,
            shiftDate,
            shift,
            eventType,
            time,
            reason,
            expectedRevision,
        });

    /// <summary>
    /// The token the page would have read: the same public rule the attendance read returns, derived from the slot's stored
    /// facts, so a test states the revision without a route that only the Cleaner themselves may call.
    /// </summary>
    public static async Task<string> RevisionAsync(
        JanitorialApiFactory factory, int person, Shift shift = Shift.Day)
    {
        var facts = await AdminSetupApi.ReadAsync(factory, db => db.ShiftAttendances.AsNoTracking()
            .Where(row => row.UserId == person && row.ShiftDate == EffectiveAttendanceApi.Day8 && row.Shift == shift)
            .ToListAsync());
        return AttendanceStateRevision.Of(person, new ShiftSlot(EffectiveAttendanceApi.Day8, shift), facts);
    }

    /// <summary>The scoped attendance read a manager's missing-time screen opens from (spec §4, §8).</summary>
    public static Task<HttpResponseMessage> StateAsync(
        HttpClient client, int userId, string shiftDate = "2026-10-08", string shift = "Day") =>
        client.GetAsync($"/api/team-attendance/{userId}?shiftDate={shiftDate}&shift={shift}");
}
