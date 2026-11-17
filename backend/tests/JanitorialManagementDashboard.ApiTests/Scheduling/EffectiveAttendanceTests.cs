using System.Net;
using System.Net.Http.Json;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Attendance;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §7 with ruling A: the Area a Cleaner may check in at is the slot's effective duty — a live relief assignment, the
/// dated plan, or — only when no dated identity exists at all — the account's compatibility Area. An absence record keeps
/// the underlying eligible Area visible and is shown as a conflict marker beside it, never as a reason to hide the Area.
/// </summary>
public class EffectiveAttendanceTests
{
    private static readonly DateOnly Day8 = EffectiveAttendanceApi.Day8;

    [Fact]
    public async Task A_dedicated_relief_scans_at_the_sign_of_their_live_assignment()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddAreaAsync(factory, "AR03", "A");
        await TestData.AddBuildingAsync(factory, "B");
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        await ReliefApi.AddStoredReliefAsync(factory, relief, destination, Day8, Shift.Day,
            await AdminSetupApi.BuildingIdAsync(factory, "B"));
        var (client, auth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E3001", ReliefApi.Phone("E3001"));

        var state = await EffectiveAttendanceApi.RecordAndReadAsync(factory, client, EffectiveAttendanceApi.Thai(8, 7, 2),
            "ShiftIn", "token-checkin-ar03");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(destination, state.DutyAreaId);
        Assert.Equal("relief", state.DutySource);
        var stored = Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Equal(destination, stored.AreaId);
        Assert.Equal(auth.User.Id, stored.UserId);
    }

    [Fact]
    public async Task A_relief_with_only_a_home_building_is_refused_and_writes_nothing()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddAreaAsync(factory, "AR03", "A");
        await TestData.AddBuildingAsync(factory, "B");
        // The person exists as a relief of building B with a dated identity, but nobody assigned them this slot: a home
        // building alone grants no duty Area (spec §7).
        await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var (client, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E3001", ReliefApi.Phone("E3001"));
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 2));

        var response = await EffectiveAttendanceApi.RecordAsync(client, "ShiftIn", "token-checkin-ar03");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("AR03", (await response.Content.ReadFromJsonAsync<BlockedScanModel>())!.AreaCode);
        Assert.Empty(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
        Assert.Single(await EffectiveAttendanceApi.BlockedAsync(factory));
    }

    [Fact]
    public async Task A_dated_plan_area_wins_over_a_stale_account_area()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddAreaAsync(factory, "AR03", "A");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR03");
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        // 1000001's account still names AR01; the plan for 8 October says AR03.
        var planned = EffectiveAttendanceApi.Thai(8, 7, 1);
        factory.Clock.SetUtcNow(planned);
        var accepted = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn", "token-checkin-ar03");
        var blocked = await EffectiveAttendanceApi.RecordAsync(cleaner, "BreakOut", "token-checkin-ar01");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var state = (await accepted.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR03"), state.DutyAreaId);
        Assert.Equal("baseline", state.DutySource);
        var stored = Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR03"), stored.AreaId);
    }

    [Fact]
    public async Task An_eligible_check_in_on_a_leave_day_is_accepted_with_a_visible_conflict()
    {
        using var factory = new JanitorialApiFactory();
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01");
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await EffectiveAttendanceApi.AddLeaveAsync(factory, "1000001", Day8, Shift.Day,
            new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var state = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 5), "ShiftIn");

        Assert.Equal("Recorded", state.Outcome);
        Assert.True(state.IsLeave);
        Assert.False(state.IsDayOff);
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR01"), state.DutyAreaId);
        var stored = Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Equal(cleanerId, stored.UserId);
        // Nothing about the plan moved: the leave row is still one agreed interval and no schedule row was rewritten.
        await factory.WithDbAsync(async db =>
        {
            var leave = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.False(leave.IsCancelled);
            Assert.Equal(cleanerId, leave.UserId);
            Assert.Single(await db.CleanerProfilePeriods.ToListAsync());
        });
    }

    [Fact]
    public async Task An_eligible_check_in_on_a_day_off_is_accepted_with_a_visible_conflict()
    {
        using var factory = new JanitorialApiFactory();
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01");
        await ScheduleWriteApi.AddStoredWeekAsync(factory, "1000001", new DateOnly(2026, 10, 5), Day8);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var state = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 5), "ShiftIn");

        Assert.Equal("Recorded", state.Outcome);
        Assert.True(state.IsDayOff);
        Assert.False(state.IsLeave);
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR01"), state.DutyAreaId);
        // The day off is still recorded exactly as the Supervisor saved it: the check-in adds an actual fact, not a plan edit.
        await factory.WithDbAsync(async db =>
        {
            var week = Assert.Single(await db.WeeklyDayOffs.Include(row => row.Choices).ToListAsync());
            Assert.Equal(Day8, Assert.Single(week.Choices).OffDate);
        });
    }

    [Fact]
    public async Task An_account_with_no_dated_identity_keeps_its_own_area_as_the_compatibility_projection()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var state = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 5), "ShiftIn");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR01"), state.DutyAreaId);
        Assert.Equal("compatibility", state.DutySource);
        Assert.Null(state.DutyBreakGroup);
    }
}

/// <summary>The attendance calls and fixtures the Task 9 test files share: a slot lifecycle, a leave interval, and the state answer.</summary>
internal static class EffectiveAttendanceApi
{
    public static readonly DateOnly Day8 = new(2026, 10, 8);

    /// <summary>Asia/Bangkok wall-clock on 8 October 2026, when every Task 9 attendance test happens.</summary>
    public static DateTimeOffset Thai(int day, int hour, int minute) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(7));

    public static Task<HttpResponseMessage> RecordAsync(
        HttpClient client,
        string eventType,
        string qrToken = AttendanceApi.Area1CheckIn,
        string? lifecycleVersion = null,
        string? stateRevision = null,
        IReadOnlyList<string>? confirmGaps = null) =>
        client.PostAsJsonAsync("/api/attendance", new
        {
            qrToken,
            eventType,
            latitude = TestGps.NearLatitude,
            longitude = TestGps.NearLongitude,
            accuracyM = 10,
            lifecycleVersion,
            stateRevision,
            confirmGaps,
        });

    public static async Task<AttendanceStateAnswer> StateAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<AttendanceStateAnswer>("/api/attendance/me");
        Assert.NotNull(response);
        return response!;
    }

    /// <summary>Moves the clock to <paramref name="at"/> and records the action, asserting it was accepted.</summary>
    public static async Task<AttendanceStateAnswer> RecordAndReadAsync(
        JanitorialApiFactory factory,
        HttpClient client,
        DateTimeOffset at,
        string eventType,
        string qrToken = AttendanceApi.Area1CheckIn,
        string? lifecycleVersion = null,
        string? stateRevision = null,
        IReadOnlyList<string>? confirmGaps = null)
    {
        factory.Clock.SetUtcNow(at);
        var response = await RecordAsync(client, eventType, qrToken, lifecycleVersion, stateRevision, confirmGaps);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return (await response.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
    }

    /// <summary>An explicit lifecycle row for one dated slot: the slot the operator chose for the numbered contract.</summary>
    public static Task AddLifecycleAsync(
        JanitorialApiFactory factory, DateOnly shiftDate, Shift shift, AttendanceLifecycleVersion version) =>
        factory.WithDbAsync(async db =>
        {
            db.AttendanceSlotLifecycles.Add(new AttendanceSlotLifecycle
            {
                ShiftDate = shiftDate,
                Shift = shift,
                Version = version,
                ActivatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    /// <summary>An agreed leave interval of one slot, as the leave command stores it: UTC, half-open.</summary>
    public static Task AddLeaveAsync(
        JanitorialApiFactory factory, string employeeId, DateOnly shiftDate, Shift shift, DateTimeOffset from,
        DateTimeOffset to) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(user => user.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            db.ScheduleExceptions.Add(new ScheduleException
            {
                UserId = person.Id,
                Kind = ScheduleExceptionKind.Leave,
                ShiftDate = shiftDate,
                Shift = shift,
                StartsAt = from.UtcDateTime,
                EndsAt = to.UtcDateTime,
                Revision = 1,
                CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    public static Task<List<Domain.Entities.ShiftAttendance>> FactsAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory, db => db.ShiftAttendances.AsNoTracking().OrderBy(row => row.Id).ToListAsync());

    public static Task<List<AttendanceGap>> GapsAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory, db => db.AttendanceGaps.AsNoTracking().OrderBy(row => row.Id).ToListAsync());

    public static Task<List<Domain.Entities.BlockedScan>> BlockedAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.AsNoTracking().ToListAsync());
}

/// <summary>One action of the read, as the page receives it.</summary>
public record ActionAnswer(string EventType, string State, DateTime? OccurredAt, string? Source, DateTime? ExpectedAt);

public record GapAnswer(string EventType, string Status, string? Reason, DateTime? RecordedAt);

public record ChoiceAnswer(string Action, List<string> RequiredGaps);

/// <summary>The whole attendance answer, including the members Task 9 added (all absent from an older answer).</summary>
public record AttendanceStateAnswer(
    DateOnly? ShiftDate,
    string? Shift,
    List<AttendanceEntryModel> Events,
    List<string> NextEvents,
    string? Outcome,
    string? LifecycleVersion = null,
    string? StateRevision = null,
    List<ActionAnswer>? Actions = null,
    List<GapAnswer>? Gaps = null,
    List<ChoiceAnswer>? NextActions = null,
    int? DutyAreaId = null,
    int? DutyBreakGroup = null,
    string? DutySource = null,
    bool IsDayOff = false,
    bool IsLeave = false)
{
    public ActionAnswer Action(string eventType) =>
        Actions!.Single(action => action.EventType == eventType);
}

/// <summary>A refused write: the machine word plus the old list of actions that are allowed now.</summary>
public record RefusedAnswer(string Message, List<string> NextEvents, string? Code = null, List<string>? RequiredGaps = null);

/// <summary>A stale-revision refusal, which carries the state the caller has to re-read.</summary>
public record StateConflictAnswer(string Message, string Code, AttendanceStateAnswer State);
