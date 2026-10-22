using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// Wireframe D3: Blocked Scans (facility-0041), scans far from their sign (facility-0035/0037) and missing Shift-Outs
/// (facility-0051). The default range is yesterday and today, Thai time. Log in before moving the clock.
/// </summary>
public class AlertEndpointTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    /// <summary>E1001 (AR01, Day) checks in at 06:50, then scans AR02's check-in sign at 07:10 and is blocked.</summary>
    private static async Task BlockedAtAr02Async(FacilityApiFactory factory, HttpClient cleaner)
    {
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 10));
        Assert.Equal(HttpStatusCode.Forbidden, (await AttendanceApi.RecordAsync(cleaner, "ShiftIn", "token-checkin-ar02")).StatusCode);
    }

    private static async Task FarCheckInAsync(FacilityApiFactory factory, HttpClient cleaner)
    {
        factory.Clock.SetUtcNow(ThaiClock.At(8, 6, 50));
        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>A Scan Record at 08:00 whose GPS verdict is "outside the radius" (120 m); the clock is left at 08:09.</summary>
    private static async Task<long> FarSubmissionAsync(FacilityApiFactory factory)
    {
        var scanId = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 0));
        await factory.WithDbAsync(async db =>
        {
            var scan = await db.ScanRecords.SingleAsync(s => s.Id == scanId);
            scan.WithinRadius = false;
            scan.DistanceM = 120;
            await db.SaveChangesAsync();
        });
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 0));
        return scanId;
    }

    [Fact]
    public async Task A_blocked_scan_is_listed_with_the_cover_the_admin_can_assign()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await BlockedAtAr02Async(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("BlockedScan", alert.Kind);
        Assert.Equal(ThaiClock.At(8, 7, 10).UtcDateTime, alert.At);
        Assert.Equal("E1001", alert.Person.EmployeeId);
        Assert.Equal("Cleaner", alert.Person.Role);
        Assert.Equal("AR01", alert.Person.AreaCode);
        Assert.Equal("AR02", alert.AreaCode);
        Assert.Equal("AR02-IN", alert.SignCode);
        Assert.False(alert.Reviewed);
        Assert.Equal(
            new CoverSuggestionModel(alert.Person.Id, await AdminSetupApi.AreaIdAsync(factory, "AR02"), null, Oct8, "Day"),
            alert.Cover);
    }

    [Fact]
    public async Task The_cover_offer_goes_once_the_cover_exists()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await BlockedAtAr02Async(factory, cleaner);
        await TestData.AddCoverAsync(factory, "E1001", "AR02", Oct8, Shift.Day);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        Assert.Null(Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Cover);
    }

    [Fact]
    public async Task The_cover_offer_goes_once_the_shift_is_over()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await BlockedAtAr02Async(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(9, 10, 0));

        var alerts = await AdminAttendanceApi.AlertsAsync(admin, "?from=2026-10-08&to=2026-10-09");

        Assert.Null(alerts.Single(a => a.Kind == "BlockedScan").Cover);
    }

    [Fact]
    public async Task A_check_in_far_from_its_sign_is_listed_with_distance_and_radius()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await FarCheckInAsync(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("AttendanceGps", alert.Kind);
        Assert.Equal("AR01-IN", alert.SignCode);
        Assert.Equal("AR01", alert.AreaCode);
        Assert.True(alert.DistanceM > 50);
        Assert.Equal((short)50, alert.RadiusM);
        Assert.Equal((short)10, alert.AccuracyM);
        Assert.Null(alert.Cover);
    }

    [Fact]
    public async Task A_submission_far_from_its_sign_is_listed()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var scanId = await FarSubmissionAsync(factory);

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("SubmissionGps", alert.Kind);
        Assert.Equal(scanId, alert.RecordId);
        Assert.Equal("AR01-01", alert.SignCode);
        Assert.Equal("ห้องน้ำชาย ชั้น 1", alert.PointName);
        Assert.Equal((short)120, alert.DistanceM);
    }

    [Fact]
    public async Task A_missing_shift_out_shows_when_the_shift_ends_and_goes_when_the_admin_fills_it()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 18, 0));
        Assert.Empty(await AdminAttendanceApi.AlertsAsync(admin));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 19, 30));
        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));
        Assert.Equal("MissingAttendance", alert.Kind);
        Assert.Equal(ThaiClock.At(8, 19, 0).UtcDateTime, alert.At);
        Assert.Equal(new[] { "ShiftOut" }, alert.MissingEvents);
        Assert.Equal("AR01", alert.AreaCode);
        Assert.Null(alert.SignCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AdminAttendanceApi.ReviewAsync(admin, "MissingAttendance", alert.RecordId)).StatusCode);

        var fill = await AdminAttendanceApi.CorrectAsync(admin, alert.Person.Id, Oct8, "Day", "ShiftOut", "18:58", "ลืมสแกนเลิกงาน");

        Assert.Equal(HttpStatusCode.OK, fill.StatusCode);
        Assert.Empty(await AdminAttendanceApi.AlertsAsync(admin));
    }

    [Fact]
    public async Task Reviewing_closes_the_alert_once_and_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await FarCheckInAsync(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        var review = await AdminAttendanceApi.ReviewAsync(admin, "AttendanceGps", alert.RecordId, "GPS ในอาคารเพี้ยน ยืนอยู่หน้าป้ายจริง");
        var again = await AdminAttendanceApi.ReviewAsync(admin, "AttendanceGps", alert.RecordId);

        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var reviewed = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));
        Assert.True(reviewed.Reviewed);
        Assert.Equal(ThaiClock.At(8, 7, 30).UtcDateTime, reviewed.ReviewedAt);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ALERT_REVIEW", entry.Action);
        Assert.Equal(adminAuth.User.Id, entry.ActorId);
        Assert.Equal("shift_attendances", entry.EntityType);
        Assert.Equal(alert.RecordId, entry.EntityId);
        Assert.Equal("GPS ในอาคารเพี้ยน ยืนอยู่หน้าป้ายจริง", entry.Reason);
    }

    [Fact]
    public async Task A_cancelled_cover_does_not_hide_the_cover_offer()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await BlockedAtAr02Async(factory, cleaner);
        await TestData.AddCoverAsync(factory, "E1001", "AR02", Oct8, Shift.Day, cancelled: true);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        Assert.NotNull(Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Cover);
    }

    [Fact]
    public async Task No_cover_is_offered_into_a_deactivated_area()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await BlockedAtAr02Async(factory, cleaner);
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Code == "AR02")).IsActive = false;
            await db.SaveChangesAsync();
        });
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        Assert.Null(Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Cover);
    }

    [Fact]
    public async Task No_cover_is_offered_to_a_cleaner_without_an_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var userId = await TestData.AddCleanerAsync(factory, "E1009", Shift.Day);
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.SingleAsync(s => s.Code == "AR02-IN");
            db.BlockedScans.Add(new FacilityRealtime.Domain.Entities.BlockedScan
            {
                UserId = userId,
                SignId = sign.Id,
                Reason = BlockReason.OtherArea,
                ScannedAt = ThaiClock.At(8, 7, 10).UtcDateTime,
            });
            await db.SaveChangesAsync();
        });
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("E1009", alert.Person.EmployeeId);
        Assert.Null(alert.Person.AreaCode);
        Assert.Null(alert.Cover);
    }

    [Fact]
    public async Task A_blocked_scan_can_be_reviewed()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await BlockedAtAr02Async(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        var review = await AdminAttendanceApi.ReviewAsync(admin, "BlockedScan", alert.RecordId);

        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        Assert.True(Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Reviewed);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ALERT_REVIEW", entry.Action);
        Assert.Equal("blocked_scans", entry.EntityType);
        Assert.Equal(alert.RecordId, entry.EntityId);
        Assert.StartsWith("ปิดเตือนสแกนผิดพื้นที่ ของ สมชาย ใจดี เวลา ", entry.Summary); // named by its reason (facility-0085)
    }

    [Fact]
    public async Task A_submission_far_from_its_sign_can_be_reviewed()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var scanId = await FarSubmissionAsync(factory);

        var review = await AdminAttendanceApi.ReviewAsync(admin, "SubmissionGps", scanId);

        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        Assert.True(Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Reviewed);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ALERT_REVIEW", entry.Action);
        Assert.Equal("scan_records", entry.EntityType);
        Assert.Equal(scanId, entry.EntityId);
    }

    [Fact]
    public async Task A_note_over_500_characters_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await FarCheckInAsync(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        var response = await AdminAttendanceApi.ReviewAsync(admin, "AttendanceGps", alert.RecordId, new string('ก', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
        Assert.False(Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Reviewed);
    }

    [Theory]
    [InlineData("AttendanceGps")]   // the check-in was near its sign: not an alert
    [InlineData("BlockedScan")]     // no Blocked Scan has this id
    public async Task Only_real_alerts_can_be_reviewed(string kind)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        var attendanceId = await AdminSetupApi.ReadAsync(factory, db => db.ShiftAttendances.Select(a => a.Id).SingleAsync());

        var response = await AdminAttendanceApi.ReviewAsync(admin, kind, attendanceId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_review_needs_a_kind()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(AdminAttendanceApi.AlertsPath + "/review", new { recordId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("?from=2026-10-08")]
    [InlineData("?from=2026-10-09&to=2026-10-08")]
    [InlineData("?from=2026-09-01&to=2026-10-08")]
    [InlineData("?from=8/10/2026&to=2026-10-08")]
    public async Task A_date_range_needs_two_dates_in_order_and_at_most_31_days(string query)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync(AdminAttendanceApi.AlertsPath + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_admins_see_and_close_alerts()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(AdminAttendanceApi.AlertsPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminAttendanceApi.ReviewAsync(cleaner, "BlockedScan", 1)).StatusCode);
    }
}
