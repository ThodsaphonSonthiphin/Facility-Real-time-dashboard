using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JanitorialManagementDashboard.ApiTests.Attendance;

public record AttendanceEntryModel(string EventType, DateTime OccurredAt, string Source, bool? WithinRadius);

public record AttendanceStateModel(
    DateOnly? ShiftDate,
    string? Shift,
    List<AttendanceEntryModel> Events,
    List<string> NextEvents,
    string? Outcome,
    int? DutyAreaId = null,
    string? DutySource = null);

public record AttendanceRefusedModel(string Message, List<string> NextEvents);

public record BlockedScanModel(string Message, string AreaCode, string AreaName);

/// <summary>1000001 is AR01's Day cleaner, 1000002 its Night cleaner; AR01's check-in sign is at TestGps.Near. Log in before moving the clock.</summary>
public class AttendanceEndpointTests
{
    private static async Task<AttendanceStateModel> RecordAtAsync(JanitorialApiFactory factory, HttpClient client, DateTimeOffset at, string eventType)
    {
        factory.Clock.SetUtcNow(at);
        var response = await AttendanceApi.RecordAsync(client, eventType);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AttendanceStateModel>())!;
    }

    private static async Task<List<T>> AllAsync<T>(JanitorialApiFactory factory, Func<AppDbContext, IQueryable<T>> query)
    {
        var rows = new List<T>();
        await factory.WithDbAsync(async db => rows = await query(db).ToListAsync());
        return rows;
    }

    [Fact]
    public async Task Recording_requires_login()
    {
        using var factory = new JanitorialApiFactory();

        var response = await AttendanceApi.RecordAsync(factory.CreateApiClient(), "ShiftIn");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cleaner_checks_in_at_the_own_area_sign()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var state = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 5), "ShiftIn");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(new DateOnly(2026, 10, 8), state.ShiftDate);
        Assert.Equal("Day", state.Shift);
        Assert.Equal("ShiftIn", Assert.Single(state.Events).EventType);
        Assert.True(state.Events[0].WithinRadius);
        Assert.Equal(new[] { "BreakOut", "ShiftOut" }, state.NextEvents);
        var stored = Assert.Single(await AllAsync(factory, db => db.ShiftAttendances));
        Assert.Equal(AttendanceSource.Scan, stored.Source);
        Assert.Equal((short)0, stored.DistanceM);
        Assert.Equal(13.756300m, stored.Latitude);
    }

    [Theory]
    [InlineData(6, 0, HttpStatusCode.OK)]
    [InlineData(5, 59, HttpStatusCode.Conflict)]
    public async Task Day_cleaner_may_check_in_from_one_hour_before_the_shift(int hour, int minute, HttpStatusCode expected)
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, hour, minute));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Night_shift_out_next_morning_belongs_to_the_night_that_started_yesterday()
    {
        using var factory = new JanitorialApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000002", "0810000002");
        await RecordAtAsync(factory, night, ThaiClock.At(8, 19, 0), "ShiftIn");

        var state = await RecordAtAsync(factory, night, ThaiClock.At(9, 7, 50), "ShiftOut");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(new DateOnly(2026, 10, 8), state.ShiftDate);
        Assert.Equal("Night", state.Shift);
    }

    [Fact]
    public async Task Night_attendance_closes_an_hour_after_the_shift()
    {
        using var factory = new JanitorialApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000002", "0810000002");
        await RecordAtAsync(factory, night, ThaiClock.At(8, 19, 0), "ShiftIn");
        factory.Clock.SetUtcNow(ThaiClock.At(9, 8, 1));

        var response = await AttendanceApi.RecordAsync(night, "ShiftOut");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Pressing_again_keeps_the_first_time()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 0), "ShiftIn");

        var state = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 30), "ShiftIn");

        Assert.Equal("AlreadyRecorded", state.Outcome);
        Assert.Equal(ThaiClock.At(8, 7, 0).UtcDateTime, Assert.Single(state.Events).OccurredAt.ToUniversalTime());
    }

    [Fact]
    public async Task A_different_event_within_five_minutes_is_ignored()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 0), "ShiftIn");

        var tooSoon = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 3), "BreakOut");
        var later = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 5), "BreakOut");

        Assert.Equal("TooSoon", tooSoon.Outcome);
        Assert.Single(tooSoon.Events);
        Assert.Equal("Recorded", later.Outcome);
        Assert.Equal(2, later.Events.Count);
    }

    [Fact]
    public async Task Out_of_order_event_is_refused_with_the_next_allowed_ones()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 0), "ShiftIn");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "BreakIn");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var refused = (await response.Content.ReadFromJsonAsync<AttendanceRefusedModel>())!;
        Assert.Equal(new[] { "BreakOut", "ShiftOut" }, refused.NextEvents);
    }

    [Fact]
    public async Task Shift_out_may_follow_a_break_out_when_break_in_was_forgotten()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 0), "ShiftIn");
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 12, 0), "BreakOut");

        var state = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 19, 0), "ShiftOut");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Empty(state.NextEvents);
    }

    [Fact]
    public async Task Check_in_at_another_areas_sign_is_blocked_and_kept_for_the_admin()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", qrToken: "token-checkin-ar02");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("AR02", (await response.Content.ReadFromJsonAsync<BlockedScanModel>())!.AreaCode);
        var blocked = Assert.Single(await AllAsync(factory, db => db.BlockedScans));
        Assert.Equal(BlockReason.OtherArea, blocked.Reason);
        Assert.Null(blocked.WithinRadius); // AR02's sign has no coordinates yet
        Assert.Empty(await AllAsync(factory, db => db.ShiftAttendances));
    }

    [Theory]
    [InlineData("token-restroom-m1")]
    [InlineData("no-such-token")]
    public async Task Point_signs_and_unknown_tokens_are_not_check_in_signs(string token)
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", qrToken: token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Location_is_required()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await cleaner.PostAsJsonAsync("/api/attendance", new { qrToken = AttendanceApi.Area1CheckIn, eventType = "ShiftIn" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Far_check_in_is_saved_and_flagged()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = Assert.Single(await AllAsync(factory, db => db.ShiftAttendances));
        Assert.False(stored.WithinRadius);
        Assert.True(stored.DistanceM > 50);
    }

    [Fact]
    public async Task Only_cleaners_record_attendance()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(admin, "ShiftIn");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task My_attendance_shows_what_can_be_pressed_next()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 6, 10));
        var before = (await cleaner.GetFromJsonAsync<AttendanceStateModel>("/api/attendance/me"))!;
        factory.Clock.SetUtcNow(ThaiClock.At(8, 5, 0));
        var outside = (await cleaner.GetFromJsonAsync<AttendanceStateModel>("/api/attendance/me"))!;

        Assert.Equal(new DateOnly(2026, 10, 8), before.ShiftDate);
        Assert.Empty(before.Events);
        Assert.Equal(new[] { "ShiftIn" }, before.NextEvents);
        Assert.Null(outside.ShiftDate);
        Assert.Empty(outside.NextEvents);
    }

    [Fact]
    public async Task Coarse_accuracy_is_still_a_location_and_is_flagged()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", accuracyM: 20_000);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(Assert.Single(await AllAsync(factory, db => db.ShiftAttendances)).WithinRadius);
    }

    [Fact]
    public async Task An_impossible_latitude_is_a_bad_request()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", latitude: 200);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AllAsync(factory, db => db.ShiftAttendances));
    }

    [Fact]
    public async Task A_deactivated_account_is_turned_away_even_with_a_valid_token()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "1000001")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var record = await AttendanceApi.RecordAsync(cleaner, "ShiftIn");
        var mine = await cleaner.GetAsync("/api/attendance/me");

        Assert.Equal(HttpStatusCode.Unauthorized, record.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, mine.StatusCode);
    }

    [Fact]
    public async Task A_press_that_loses_the_race_returns_the_first_one()
    {
        var race = new RacingShiftInInterceptor();
        using var factory = new JanitorialApiFactory { ConfigureTestServices = services => services.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(race)) };
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));
        race.Armed = true;

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("AlreadyRecorded", (await response.Content.ReadFromJsonAsync<AttendanceStateModel>())!.Outcome);
        Assert.Single(await AllAsync(factory, db => db.ShiftAttendances));
    }

    /// <summary>Inserts the competing press through a second context on the same connection just before the first save.</summary>
    private sealed class RacingShiftInInterceptor : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var pending = eventData.Context!.ChangeTracker.Entries<ShiftAttendance>().FirstOrDefault(e => e.State == EntityState.Added);
            if (Armed && pending is not null)
            {
                Armed = false;
                var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(eventData.Context.Database.GetDbConnection()).Options;
                await using var other = new AppDbContext(options);
                var p = pending.Entity;
                other.ShiftAttendances.Add(new ShiftAttendance
                {
                    UserId = p.UserId, AreaId = p.AreaId, ShiftDate = p.ShiftDate, Shift = p.Shift, EventType = p.EventType,
                    OccurredAt = p.OccurredAt, Source = p.Source, SignId = p.SignId, CreatedAt = p.CreatedAt,
                });
                await other.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }

    [Theory]
    [InlineData("token-checkin-ar01")]
    [InlineData("token-checkin-ar02")] // another Area's sign: told on the press, as a Blocked Scan (facility-0080)
    public async Task A_working_check_in_sign_answers_no_content_and_records_nothing(string token)
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await cleaner.GetAsync($"/api/attendance/signs/{token}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Empty(await AllAsync(factory, db => db.BlockedScans));
        Assert.Empty(await AllAsync(factory, db => db.ShiftAttendances));
    }

    [Theory]
    [InlineData("token-restroom-m1")]
    [InlineData("no-such-token")]
    public async Task Point_signs_and_unknown_tokens_fail_the_check_in_sign_check(string token)
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.GetAsync($"/api/attendance/signs/{token}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ไม่พบป้ายลงเวลานี้", body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task The_sign_of_a_deactivated_area_fails_the_check()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Code == "AR02")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await cleaner.GetAsync("/api/attendance/signs/token-checkin-ar02");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Only_a_logged_in_cleaner_checks_a_check_in_sign()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var anonymous = await factory.CreateApiClient().GetAsync("/api/attendance/signs/token-checkin-ar01");
        var byAdmin = await admin.GetAsync("/api/attendance/signs/token-checkin-ar01");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byAdmin.StatusCode);
    }

    // ---- Task 9: the slot's lifecycle contract (spec §8) ------------------------------------------------------------

    private static readonly DateOnly Day8 = EffectiveAttendanceApi.Day8;

    [Fact]
    public async Task A_legacy_slot_keeps_the_four_action_flow_and_answers_no_numbered_actions()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 6, 10));

        var before = await EffectiveAttendanceApi.StateAsync(cleaner);

        Assert.Equal("Legacy4", before.LifecycleVersion);
        Assert.Equal(
            new[] { "ShiftIn", "BreakOut", "BreakIn", "ShiftOut" },
            before.Actions!.Select(action => action.EventType));
        Assert.All(before.Actions!, action => Assert.Equal("pending", action.State));
        Assert.Empty(before.Gaps!);
        Assert.Equal("ShiftIn", Assert.Single(before.NextActions!).Action);
        Assert.Empty(Assert.Single(before.NextActions!).RequiredGaps);

        await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 0), "ShiftIn");
        var morning = await EffectiveAttendanceApi.RecordAndReadAsync(
            factory, cleaner, EffectiveAttendanceApi.Thai(8, 12, 0), "BreakOut");
        var evening = await EffectiveAttendanceApi.RecordAndReadAsync(
            factory, cleaner, EffectiveAttendanceApi.Thai(8, 19, 0), "ShiftOut");

        Assert.Equal("Recorded", evening.Outcome);
        Assert.Equal(new[] { "BreakIn", "ShiftOut" }, morning.NextEvents);
        Assert.Empty(evening.NextEvents);
        Assert.Equal(3, evening.Events.Count);
        Assert.Equal("recorded", evening.Action("BreakOut").State);
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
    }

    [Fact]
    public async Task A_legacy_slot_refuses_a_page_that_claims_the_numbered_contract()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 0));

        var numbered = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn", lifecycleVersion: "Numbered6");
        var withGaps = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn", confirmGaps: new[] { "BreakOut" });
        var staleRevision = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn", stateRevision: "not-the-state");

        Assert.Equal(HttpStatusCode.Conflict, numbered.StatusCode);
        Assert.Equal("refresh-required", (await numbered.Content.ReadFromJsonAsync<RefusedAnswer>())!.Code);
        Assert.Equal(HttpStatusCode.Conflict, withGaps.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleRevision.StatusCode);
        Assert.Empty(await EffectiveAttendanceApi.FactsAsync(factory));
    }

    [Fact]
    public async Task A_numbered_slot_records_the_six_actions_in_order()
    {
        using var factory = new JanitorialApiFactory();
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 6, 10));

        var before = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal("Numbered6", before.LifecycleVersion);
        Assert.Equal(
            new[] { "ShiftIn", "Break1Out", "Break1In", "Break2Out", "Break2In", "ShiftOut" },
            before.Actions!.Select(action => action.EventType));
        // The account has no dated plan, so no first-break group is known and those two instants are not invented.
        Assert.Null(before.Action("Break1Out").ExpectedAt);
        Assert.Equal(EffectiveAttendanceApi.Thai(8, 16, 0).UtcDateTime, before.Action("Break2Out").ExpectedAt);
        Assert.Matches("^[0-9a-f]{64}$", before.StateRevision);

        var state = before;
        foreach (var (at, action) in new[]
                 {
                     (EffectiveAttendanceApi.Thai(8, 7, 0), "ShiftIn"),
                     (EffectiveAttendanceApi.Thai(8, 10, 0), "Break1Out"),
                     (EffectiveAttendanceApi.Thai(8, 11, 0), "Break1In"),
                     (EffectiveAttendanceApi.Thai(8, 16, 0), "Break2Out"),
                     (EffectiveAttendanceApi.Thai(8, 17, 0), "Break2In"),
                     (EffectiveAttendanceApi.Thai(8, 19, 0), "ShiftOut"),
                 })
        {
            state = await EffectiveAttendanceApi.RecordAndReadAsync(
                factory, cleaner, at, action, lifecycleVersion: "Numbered6", stateRevision: state.StateRevision);
        }

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(6, state.Events.Count);
        Assert.All(state.Actions!, action => Assert.Equal("recorded", action.State));
        Assert.Empty(state.NextActions!);
        Assert.NotEqual(before.StateRevision, state.StateRevision);
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
    }

    [Fact]
    public async Task A_numbered_slot_shows_a_break_the_leave_stepped_over_as_not_applicable_and_a_forgotten_one_as_missing()
    {
        using var factory = new JanitorialApiFactory();
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        // A half day: in at 07:00, out at 12:00, and the agreed leave 12:00-19:00 covers the rest of the plan.
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftIn,
            EffectiveAttendanceApi.Thai(8, 7, 0));
        await TestData.AddAttendanceAsync(factory, "1000001", Day8, Shift.Day, AttendanceEvent.ShiftOut,
            EffectiveAttendanceApi.Thai(8, 12, 0));
        await EffectiveAttendanceApi.AddLeaveAsync(factory, "1000001", Day8, Shift.Day,
            new DateTimeOffset(2026, 10, 8, 5, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 13, 0));

        var state = await EffectiveAttendanceApi.StateAsync(cleaner);

        Assert.Equal("recorded", state.Action("ShiftIn").State);
        Assert.Equal("missing", state.Action("Break1Out").State);
        Assert.Equal("missing", state.Action("Break1In").State);
        Assert.Equal("not-applicable", state.Action("Break2Out").State);
        Assert.Equal("not-applicable", state.Action("Break2In").State);
        Assert.Equal("recorded", state.Action("ShiftOut").State);
        // The derived states write nothing: no actual event and no skip marker was created for them.
        Assert.Equal(2, (await EffectiveAttendanceApi.FactsAsync(factory)).Count);
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
    }

    [Fact]
    public async Task A_numbered_slot_refuses_a_stale_page_before_writing_anything()
    {
        using var factory = new JanitorialApiFactory();
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 0));

        var noMarker = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn");
        var noRevision = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn", lifecycleVersion: "Numbered6");
        var wrongRevision = await EffectiveAttendanceApi.RecordAsync(
            cleaner, "ShiftIn", lifecycleVersion: "Numbered6", stateRevision: "0" + new string('0', 63));

        Assert.Equal(HttpStatusCode.Conflict, noMarker.StatusCode);
        Assert.Equal("refresh-required", (await noMarker.Content.ReadFromJsonAsync<RefusedAnswer>())!.Code);
        Assert.Equal(HttpStatusCode.Conflict, noRevision.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, wrongRevision.StatusCode);
        Assert.Empty(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
    }

    [Fact]
    public async Task A_numbered_slot_needs_exactly_the_required_gap_confirmations()
    {
        using var factory = new JanitorialApiFactory();
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 0));
        var empty = (await EffectiveAttendanceApi.StateAsync(cleaner)).StateRevision;
        var morning = await EffectiveAttendanceApi.RecordAndReadAsync(
            factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 0), "ShiftIn",
            lifecycleVersion: "Numbered6", stateRevision: empty);

        // The revision of the recorded slot is the one the next page must send back.
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 16, 0));
        var revision = morning.StateRevision;
        Assert.Equal(revision, (await EffectiveAttendanceApi.StateAsync(cleaner)).StateRevision);
        Assert.NotEqual(empty, revision);

        var missing = await EffectiveAttendanceApi.RecordAsync(
            cleaner, "Break2Out", lifecycleVersion: "Numbered6", stateRevision: revision);
        var extra = await EffectiveAttendanceApi.RecordAsync(cleaner, "Break2Out",
            lifecycleVersion: "Numbered6", stateRevision: revision,
            confirmGaps: new[] { "Break1Out", "Break1In", "ShiftOut" });
        var notSkippable = await EffectiveAttendanceApi.RecordAsync(cleaner, "Break2Out",
            lifecycleVersion: "Numbered6", stateRevision: revision, confirmGaps: new[] { "ShiftIn" });

        var refused = (await missing.Content.ReadFromJsonAsync<RefusedAnswer>())!;
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        Assert.Equal("gaps-not-confirmed", refused.Code);
        Assert.Equal(new[] { "Break1Out", "Break1In" }, refused.RequiredGaps);
        Assert.Equal(HttpStatusCode.Conflict, extra.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, notSkippable.StatusCode);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
    }

    [Fact]
    public async Task A_confirmed_gap_jump_stores_skip_markers_with_no_time_of_their_own()
    {
        using var factory = new JanitorialApiFactory();
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 0));
        var empty = (await EffectiveAttendanceApi.StateAsync(cleaner)).StateRevision;
        await EffectiveAttendanceApi.RecordAndReadAsync(
            factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 0), "ShiftIn",
            lifecycleVersion: "Numbered6", stateRevision: empty);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 16, 5));
        var revision = (await EffectiveAttendanceApi.StateAsync(cleaner)).StateRevision;

        var state = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 16, 5),
            "Break2Out", lifecycleVersion: "Numbered6", stateRevision: revision,
            confirmGaps: new[] { "Break1Out", "Break1In" });

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal("skipped", state.Action("Break1Out").State);
        Assert.Null(state.Action("Break1Out").OccurredAt);
        Assert.Equal("skipped", state.Action("Break1In").State);
        Assert.Equal("recorded", state.Action("Break2Out").State);
        Assert.Equal(new[] { "Break1Out", "Break1In" },
            state.Gaps!.Select(gap => gap.EventType));
        Assert.All(state.Gaps!, gap => Assert.Equal("Skipped", gap.Status));
        // A skip is a marker, not a scanned fact: the slot holds the Shift-In and the break-2 departure only.
        Assert.Equal(2, (await EffectiveAttendanceApi.FactsAsync(factory)).Count);
        var area = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var stored = await EffectiveAttendanceApi.GapsAsync(factory);
        Assert.All(stored, gap =>
        {
            Assert.Equal(AttendanceGapStatus.Skipped, gap.Status);
            Assert.Equal(area, gap.AreaId);
            Assert.Equal(auth.User.Id, gap.RecordedById);
            Assert.NotNull(gap.RecordedAt);
            Assert.Null(gap.Reason);
        });
    }

    [Fact]
    public async Task A_numbered_slot_replays_already_recorded_action_idempotently_with_stale_prewrite_revision()
    {
        using var factory = new JanitorialApiFactory();
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 0));
        var empty = (await EffectiveAttendanceApi.StateAsync(cleaner)).StateRevision;

        var afterShiftIn = await EffectiveAttendanceApi.RecordAndReadAsync(
            factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 0), "ShiftIn",
            lifecycleVersion: "Numbered6", stateRevision: empty);
        Assert.NotEqual(empty, afterShiftIn.StateRevision);

        // Re-submitting ShiftIn with the pre-write revision replays idempotently instead of returning refresh-required.
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 2));
        var replayShiftIn = await EffectiveAttendanceApi.RecordAsync(
            cleaner, "ShiftIn", lifecycleVersion: "Numbered6", stateRevision: empty);
        Assert.Equal(HttpStatusCode.OK, replayShiftIn.StatusCode);
        var replayShiftInBody = (await replayShiftIn.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Equal("AlreadyRecorded", replayShiftInBody.Outcome);
        Assert.Equal(afterShiftIn.StateRevision, replayShiftInBody.StateRevision);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));

        // Missing or wrong lifecycleVersion on a repeated submission for an already-recorded action still refuses.
        var missingMarker = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn", stateRevision: empty);
        var wrongMarker = await EffectiveAttendanceApi.RecordAsync(
            cleaner, "ShiftIn", lifecycleVersion: "Legacy4", stateRevision: empty);
        Assert.Equal(HttpStatusCode.Conflict, missingMarker.StatusCode);
        Assert.Equal("refresh-required", (await missingMarker.Content.ReadFromJsonAsync<RefusedAnswer>())!.Code);
        Assert.Equal(HttpStatusCode.Conflict, wrongMarker.StatusCode);
        Assert.Equal("refresh-required", (await wrongMarker.Content.ReadFromJsonAsync<RefusedAnswer>())!.Code);

        // Same idempotent replay behavior for a gap-confirming action (Break2Out).
        var afterBreak2Out = await EffectiveAttendanceApi.RecordAndReadAsync(
            factory, cleaner, EffectiveAttendanceApi.Thai(8, 16, 0), "Break2Out",
            lifecycleVersion: "Numbered6", stateRevision: afterShiftIn.StateRevision,
            confirmGaps: new[] { "Break1Out", "Break1In" });
        Assert.NotEqual(afterShiftIn.StateRevision, afterBreak2Out.StateRevision);

        var replayBreak2Out = await EffectiveAttendanceApi.RecordAsync(
            cleaner, "Break2Out", lifecycleVersion: "Numbered6", stateRevision: afterShiftIn.StateRevision,
            confirmGaps: new[] { "Break1Out", "Break1In" });
        Assert.Equal(HttpStatusCode.OK, replayBreak2Out.StatusCode);
        var replayBreak2OutBody = (await replayBreak2Out.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Equal("AlreadyRecorded", replayBreak2OutBody.Outcome);
        Assert.Equal(afterBreak2Out.StateRevision, replayBreak2OutBody.StateRevision);
        Assert.Equal(2, (await EffectiveAttendanceApi.FactsAsync(factory)).Count);
        Assert.Equal(2, (await EffectiveAttendanceApi.GapsAsync(factory)).Count);
    }

    [Fact]
    public async Task A_numbered_slot_refuses_new_action_with_stale_prewrite_revision()
    {
        using var factory = new JanitorialApiFactory();
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 0));
        var empty = (await EffectiveAttendanceApi.StateAsync(cleaner)).StateRevision;

        await EffectiveAttendanceApi.RecordAndReadAsync(
            factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 0), "ShiftIn",
            lifecycleVersion: "Numbered6", stateRevision: empty);

        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 12, 0));
        var staleNewAction = await EffectiveAttendanceApi.RecordAsync(
            cleaner, "Break1Out", lifecycleVersion: "Numbered6", stateRevision: empty);

        Assert.Equal(HttpStatusCode.Conflict, staleNewAction.StatusCode);
        Assert.Equal("refresh-required", (await staleNewAction.Content.ReadFromJsonAsync<RefusedAnswer>())!.Code);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
    }

    [Fact]
    public async Task A_numbered_slot_press_that_loses_the_same_action_race_returns_already_recorded()
    {
        var race = new RacingShiftInInterceptor();
        using var factory = new JanitorialApiFactory
        {
            ConfigureTestServices = services => services.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(race)),
        };
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 0));
        var empty = (await EffectiveAttendanceApi.StateAsync(cleaner)).StateRevision;
        race.Armed = true;

        var response = await EffectiveAttendanceApi.RecordAsync(
            cleaner, "ShiftIn", lifecycleVersion: "Numbered6", stateRevision: empty);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Equal("AlreadyRecorded", body.Outcome);
        Assert.NotEqual(empty, body.StateRevision);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
    }

    [Fact]
    public async Task A_numbered_slot_refuses_concurrent_different_action_when_slot_advances_under_lock()
    {
        var race = new RacingSlotAdvanceInterceptor(
            AttendanceEvent.Break1Out, EffectiveAttendanceApi.Thai(8, 12, 0).UtcDateTime);
        using var factory = new JanitorialApiFactory
        {
            ConfigureTestServices = services => services.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(race)),
        };
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 0));
        var empty = (await EffectiveAttendanceApi.StateAsync(cleaner)).StateRevision;

        var afterShiftIn = await EffectiveAttendanceApi.RecordAndReadAsync(
            factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 0), "ShiftIn",
            lifecycleVersion: "Numbered6", stateRevision: empty);

        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 16, 0));
        race.Armed = true;
        var response = await EffectiveAttendanceApi.RecordAsync(
            cleaner, "Break2Out", lifecycleVersion: "Numbered6", stateRevision: afterShiftIn.StateRevision,
            confirmGaps: new[] { "Break1Out", "Break1In" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("refresh-required", (await response.Content.ReadFromJsonAsync<RefusedAnswer>())!.Code);
        // Returning 409 rolls back the request transaction on the shared SQLite connection, leaving only ShiftIn and no Break2Out/gaps.
        var facts = await EffectiveAttendanceApi.FactsAsync(factory);
        Assert.Equal(new[] { AttendanceEvent.ShiftIn }, facts.Select(f => f.EventType));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
    }

    /// <summary>Commits a competing action for the slot right as the next writer acquires the slot anchor.</summary>
    private sealed class RacingSlotAdvanceInterceptor(AttendanceEvent competingEvent, DateTime occurredAtUtc) : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var anchor = eventData.Context!.ChangeTracker.Entries<ScheduleAnchor>()
                .FirstOrDefault(e => e.State == EntityState.Modified);
            if (Armed && anchor is not null)
            {
                Armed = false;
                var a = anchor.Entity;
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(eventData.Context.Database.GetDbConnection()).Options;
                await using var other = new AppDbContext(options);
                var existing = await other.ShiftAttendances.AsNoTracking()
                    .FirstAsync(s => s.UserId == a.PersonId && s.ShiftDate == a.ShiftDate && s.Shift == a.Shift,
                        cancellationToken);
                other.ShiftAttendances.Add(new ShiftAttendance
                {
                    UserId = a.PersonId,
                    AreaId = existing.AreaId,
                    ShiftDate = a.ShiftDate,
                    Shift = a.Shift,
                    EventType = competingEvent,
                    OccurredAt = occurredAtUtc,
                    Source = AttendanceSource.Scan,
                    SignId = existing.SignId,
                    CreatedAt = occurredAtUtc,
                });
                await other.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
