using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Attendance;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Points;

public record ScanCreatedModel(
    long ScanRecordId, int ServicePointId, string Placement, int? LateMinutes, string NewPointStatus, int? DistanceM, bool? WithinRadius, DateTime SubmittedAt);

/// <summary>
/// Seeded men's restroom (token-restroom-m1, AR01): Day rounds 07:00-09:00 and 16:00-18:00, Night 20:00-22:00 and 03:00-05:00.
/// E1001 = AR01 Day cleaner, E1002 = AR01 Night cleaner, E1003 = AR02 Day cleaner. Log in before moving the clock.
/// </summary>
public class ScanRecordEndpointTests
{
    private const string MenRestroomToken = "token-restroom-m1";
    private const string MeetingRoomToken = "token-meeting-room";

    private static object Body(
        string qrToken = MenRestroomToken,
        string status = "Normal",
        double latitude = TestGps.NearLatitude,
        double longitude = TestGps.NearLongitude,
        double accuracyM = 10) =>
        new { qrToken, status, latitude, longitude, accuracyM };

    /// <summary>Checks in at AR01's sign at <paramref name="at"/>, then submits <paramref name="body"/>; asserts 201.</summary>
    private static async Task<ScanCreatedModel> CheckInAndScanAtAsync(
        FacilityApiFactory factory, HttpClient client, DateTimeOffset at, object? body = null)
    {
        await AttendanceApi.CheckInAsync(factory, client, at);
        var response = await client.PostAsJsonAsync("/api/scan-records", body ?? Body());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ScanCreatedModel>())!;
    }

    private static async Task<ScanRecord> StoredAsync(FacilityApiFactory factory, long id)
    {
        ScanRecord? record = null;
        await factory.WithDbAsync(async db => record = await db.ScanRecords.SingleAsync(r => r.Id == id));
        return record!;
    }

    private static async Task<int> CountAsync<T>(FacilityApiFactory factory, Func<AppDbContext, IQueryable<T>> query)
    {
        var count = 0;
        await factory.WithDbAsync(async db => count = await query(db).CountAsync());
        return count;
    }

    [Fact]
    public async Task Scanning_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("supervisor")]
    public async Task Only_cleaners_submit(string who)
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = who == "admin"
            ? await AuthApi.LoggedInAdminAsync(factory)
            : await AuthApi.LoggedInAsEmployeeAsync(factory, "S2001", "0820000001");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        var response = await client.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory, db => db.BlockedScans)); // refused for the role, not blocked as another Area
        Assert.Equal(0, await CountAsync(factory, db => db.ScanRecords));
    }

    [Theory]
    [InlineData("no-such-token")]
    [InlineData("token-checkin-ar01")]
    public async Task Unknown_or_check_in_tokens_are_404(string token)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body(qrToken: token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Location_is_required()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", new { qrToken = MenRestroomToken, status = "Normal" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Scan_before_checking_in_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory, db => db.ScanRecords));
    }

    [Fact]
    public async Task Scan_after_shift_out_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));
        Assert.Equal(HttpStatusCode.OK, (await AttendanceApi.RecordAsync(cleaner, "ShiftOut")).StatusCode);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 20));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Scan_during_a_break_is_allowed()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));
        Assert.Equal(HttpStatusCode.OK, (await AttendanceApi.RecordAsync(cleaner, "BreakOut")).StatusCode);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        Assert.Equal("OnTime", created.Placement);
    }

    [Fact]
    public async Task Scan_inside_the_round_is_on_time_with_gps_and_waits_for_inspection()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        Assert.Equal("OnTime", created.Placement);
        Assert.Equal("PendingInspection", created.NewPointStatus);
        Assert.True(created.WithinRadius);
        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(auth.User.Id, stored.UserId);
        Assert.Equal(new DateOnly(2026, 10, 8), stored.ShiftDate);
        Assert.Equal(Shift.Day, stored.Shift);
        Assert.Equal(new TimeOnly(7, 0), stored.RoundStart);
        Assert.Equal(13.756300m, stored.Latitude);
        Assert.Null(stored.CoverAssignmentId);
    }

    [Fact]
    public async Task Scan_after_an_empty_round_is_late_for_that_round()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 9, 40));

        Assert.Equal("Late", created.Placement);
        Assert.Equal(40, created.LateMinutes);
    }

    [Fact]
    public async Task Second_scan_after_the_round_is_off_round()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 9, 40));

        Assert.Equal("OffRound", created.Placement);
        Assert.Null((await StoredAsync(factory, created.ScanRecordId)).RoundWindowId);
    }

    [Fact]
    public async Task Scan_after_a_rework_inspection_is_the_rework()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var first = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, first.ScanRecordId, InspectionResult.Rework, ThaiClock.At(8, 8, 30));

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 10, 0));

        Assert.Equal("Rework", created.Placement);
    }

    [Fact]
    public async Task Early_check_in_and_scan_belong_to_the_day_shift_before_its_first_round()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (_, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await using var adminHub = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        var adminGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminHub.On<JsonElement>("ScanRecorded", point => adminGot.TrySetResult(point));
        await Task.Delay(200);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 6, 45));

        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(new DateOnly(2026, 10, 8), stored.ShiftDate);
        Assert.Equal(Shift.Day, stored.Shift);
        Assert.Equal("OffRound", created.Placement);
        // The Cleaner sees the point in the Day shift; the Dashboard still shows the shift running now (Night, until 07:00)
        Assert.Equal("BeforeFirstRound", created.NewPointStatus);
        var payload = await adminGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Overdue", payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Night_cleaner_after_midnight_is_late_for_the_previous_evenings_round()
    {
        using var factory = new FacilityApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1002", "0810000002");

        var created = await CheckInAndScanAtAsync(factory, night, ThaiClock.At(9, 2, 0));

        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(new DateOnly(2026, 10, 8), stored.ShiftDate);
        Assert.Equal(Shift.Night, stored.Shift);
        Assert.Equal("Late", created.Placement);
        Assert.Equal(240, created.LateMinutes);
    }

    [Fact]
    public async Task Scan_of_another_areas_point_is_blocked_and_kept_for_the_admin()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body(qrToken: MeetingRoomToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("AR02", (await response.Content.ReadFromJsonAsync<BlockedScanModel>())!.AreaCode);
        Assert.Equal(1, await CountAsync(factory, db => db.BlockedScans.Where(b => b.Reason == BlockReason.OtherArea)));
        Assert.Equal(0, await CountAsync(factory, db => db.ScanRecords));
    }

    [Fact]
    public async Task Cover_assignment_lets_the_cleaner_work_the_other_area_this_shift()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var coverId = await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, 8), Shift.Day);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 30), Body(qrToken: MeetingRoomToken));

        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(coverId, stored.CoverAssignmentId);
        Assert.Equal("OnTime", created.Placement);
        Assert.Null(created.WithinRadius); // AR02's signs have no coordinates yet: no verdict
        Assert.Null(created.DistanceM);
    }

    [Fact]
    public async Task Cancelled_cover_no_longer_lets_the_cleaner_in()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, 8), Shift.Day, cancelled: true);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body(qrToken: MeetingRoomToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(7, Shift.Day)]    // yesterday's Day cover
    [InlineData(8, Shift.Night)]  // tonight's cover, not the Day cleaner's shift
    public async Task Cover_for_another_date_or_shift_does_not_count(int coverDay, Shift coverShift)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, coverDay), coverShift);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body(qrToken: MeetingRoomToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, await CountAsync(factory, db => db.BlockedScans.Where(b => b.Reason == BlockReason.OtherArea)));
        Assert.Equal(0, await CountAsync(factory, db => db.ScanRecords));
    }

    [Fact]
    public async Task Scan_outside_the_attendance_window_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 21, 0)); // the Day window closed at 20:00

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory, db => db.ScanRecords));
    }

    [Fact]
    public async Task Far_scan_is_saved_and_flagged()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10),
            Body(latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude));

        Assert.False(created.WithinRadius);
        Assert.True(created.DistanceM > 50);
    }

    [Fact]
    public async Task Inaccurate_fix_is_flagged_even_at_the_sign()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10), Body(accuracyM: 80));

        Assert.False(created.WithinRadius);
    }

    [Fact]
    public async Task Scanner_is_the_logged_in_user_even_if_the_body_names_someone_else()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        var adminId = 0;
        await factory.WithDbAsync(async db => adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10), new
        {
            qrToken = MenRestroomToken, userId = adminId, status = "Normal",
            latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        Assert.Equal(auth.User.Id, (await StoredAsync(factory, created.ScanRecordId)).UserId);
    }

    [Fact]
    public async Task Issue_scan_stores_its_tags_and_note()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10), new
        {
            qrToken = MenRestroomToken, status = "Issue", issueTags = new[] { "wet_floor", "plumbing_issue" }, notes = " ก๊อกรั่ว ",
            latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(CleaningStatus.Issue, stored.Status);
        Assert.Equal("wet_floor,plumbing_issue", stored.IssueTags);
        Assert.Equal("ก๊อกรั่ว", stored.Note);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("undefined-number")]
    public async Task Missing_or_unknown_status_is_rejected(string kind)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        object body = kind == "missing"
            ? new { qrToken = MenRestroomToken, latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 }
            : new { qrToken = MenRestroomToken, status = 7, latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 };

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Overlong_note_is_rejected()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", new
        {
            qrToken = MenRestroomToken, status = "Normal", notes = new string('x', 1001),
            latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(66, 66, 66, HttpStatusCode.Created)]      // 66+66+66+2 commas = 200, at the limit
    [InlineData(67, 66, 66, HttpStatusCode.BadRequest)]   // 201
    public async Task Issue_tags_length_boundary(int a, int b, int c, HttpStatusCode expected)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", new
        {
            qrToken = MenRestroomToken, status = "Issue",
            issueTags = new[] { new string('a', a), new string('b', b), new string('c', c) },
            latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_point_is_404()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.Include(s => s.ServicePoint).SingleAsync(s => s.QrToken == MenRestroomToken);
            sign.ServicePoint!.IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_area_is_404()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.Include(s => s.ServicePoint).ThenInclude(p => p!.Area).SingleAsync(s => s.QrToken == MenRestroomToken);
            sign.ServicePoint!.Area!.IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_is_401()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == auth.User.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Only_admins_receive_the_live_update()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, cleanerAuth, _) = await AuthApi.LoggedInAsync(factory);
        var (_, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await using var adminHub = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        await using var cleanerHub = await HubClient.ConnectAsync(factory, cleanerAuth.AccessToken);
        var adminGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanerGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminHub.On<JsonElement>("ScanRecorded", point => adminGot.TrySetResult(point));
        cleanerHub.On<JsonElement>("ScanRecorded", point => cleanerGot.TrySetResult(point));
        // StartAsync returns after the handshake; OnConnectedAsync, which joins the admin group, runs just after it
        await Task.Delay(200);

        await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        var payload = await adminGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("PendingInspection", payload.GetProperty("status").GetString());
        Assert.False(payload.TryGetProperty("qrToken", out _)); // facility-0060
        await Task.Delay(500);
        Assert.False(cleanerGot.Task.IsCompleted); // facility-0058
    }
}
