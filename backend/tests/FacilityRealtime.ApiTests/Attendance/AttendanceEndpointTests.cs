using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Attendance;

public record AttendanceEntryModel(string EventType, DateTime OccurredAt, string Source, bool? WithinRadius);

public record AttendanceStateModel(DateOnly? ShiftDate, string? Shift, List<AttendanceEntryModel> Events, List<string> NextEvents, string? Outcome);

public record AttendanceRefusedModel(string Message, List<string> NextEvents);

public record BlockedScanModel(string Message, string AreaCode, string AreaName);

/// <summary>E1001 is AR01's Day cleaner, E1002 its Night cleaner; AR01's check-in sign is at TestGps.Near. Log in before moving the clock.</summary>
public class AttendanceEndpointTests
{
    private static async Task<AttendanceStateModel> RecordAtAsync(FacilityApiFactory factory, HttpClient client, DateTimeOffset at, string eventType)
    {
        factory.Clock.SetUtcNow(at);
        var response = await AttendanceApi.RecordAsync(client, eventType);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AttendanceStateModel>())!;
    }

    private static async Task<List<T>> AllAsync<T>(FacilityApiFactory factory, Func<AppDbContext, IQueryable<T>> query)
    {
        var rows = new List<T>();
        await factory.WithDbAsync(async db => rows = await query(db).ToListAsync());
        return rows;
    }

    [Fact]
    public async Task Recording_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await AttendanceApi.RecordAsync(factory.CreateApiClient(), "ShiftIn");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cleaner_checks_in_at_the_own_area_sign()
    {
        using var factory = new FacilityApiFactory();
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
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, hour, minute));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Night_shift_out_next_morning_belongs_to_the_night_that_started_yesterday()
    {
        using var factory = new FacilityApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1002", "0810000002");
        await RecordAtAsync(factory, night, ThaiClock.At(8, 19, 0), "ShiftIn");

        var state = await RecordAtAsync(factory, night, ThaiClock.At(9, 7, 50), "ShiftOut");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(new DateOnly(2026, 10, 8), state.ShiftDate);
        Assert.Equal("Night", state.Shift);
    }

    [Fact]
    public async Task Night_attendance_closes_an_hour_after_the_shift()
    {
        using var factory = new FacilityApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1002", "0810000002");
        await RecordAtAsync(factory, night, ThaiClock.At(8, 19, 0), "ShiftIn");
        factory.Clock.SetUtcNow(ThaiClock.At(9, 8, 1));

        var response = await AttendanceApi.RecordAsync(night, "ShiftOut");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Pressing_again_keeps_the_first_time()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 0), "ShiftIn");

        var state = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 30), "ShiftIn");

        Assert.Equal("AlreadyRecorded", state.Outcome);
        Assert.Equal(ThaiClock.At(8, 7, 0).UtcDateTime, Assert.Single(state.Events).OccurredAt.ToUniversalTime());
    }

    [Fact]
    public async Task A_different_event_within_five_minutes_is_ignored()
    {
        using var factory = new FacilityApiFactory();
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
        using var factory = new FacilityApiFactory();
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
        using var factory = new FacilityApiFactory();
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
        using var factory = new FacilityApiFactory();
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
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", qrToken: token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Location_is_required()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await cleaner.PostAsJsonAsync("/api/attendance", new { qrToken = AttendanceApi.Area1CheckIn, eventType = "ShiftIn" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Far_check_in_is_saved_and_flagged()
    {
        using var factory = new FacilityApiFactory();
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
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(admin, "ShiftIn");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task My_attendance_shows_what_can_be_pressed_next()
    {
        using var factory = new FacilityApiFactory();
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
}
