using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Points;

public record RoundModel(int Id, TimeOnly Start, TimeOnly End);

public record LastScanModel(DateTime SubmittedAt, string CleanerName, string Placement, string Status);

public record IssueModel(List<string> Tags, string? Note, DateTime ReportedAt);

public record PointModel(
    int Id, string Name, string AreaCode, string BuildingCode, string Status,
    RoundModel? CurrentRound, RoundModel? NextRound, LastScanModel? LastScan, IssueModel? Issue);

/// <summary>
/// Seeded rounds: both restrooms Day 07-09 and 16-18, Night 20-22 and 03-05; the meeting room (day-only Area AR02) Day 08-10.
/// Every test logs in before moving the clock (see TestTimeProvider.SetUtcNow).
/// </summary>
public class ServicePointEndpointTests
{
    private const string MenRestroom = "ห้องน้ำชาย ชั้น 1";
    private const string MeetingRoom = "ห้องประชุม";

    private static async Task<List<PointModel>> DashboardAtAsync(FacilityApiFactory factory, DateTimeOffset at)
    {
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(at);
        return (await admin.GetFromJsonAsync<List<PointModel>>("/api/service-points"))!;
    }

    private static PointModel Point(List<PointModel> points, string name) => points.Single(p => p.Name == name);

    [Fact]
    public async Task Dashboard_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().GetAsync("/api/service-points");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cleaners_cannot_read_the_dashboard()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.GetAsync("/api/service-points");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // facility-0058
    }

    [Fact]
    public async Task Admin_sees_every_active_point_in_area_order_without_qr_tokens()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var body = await admin.GetStringAsync("/api/service-points");
        var points = (await admin.GetFromJsonAsync<List<PointModel>>("/api/service-points"))!;

        Assert.Equal(new[] { MenRestroom, "ห้องน้ำหญิง ชั้น 1", MeetingRoom }, points.Select(p => p.Name));
        Assert.Equal(new[] { "AR01", "AR01", "AR02" }, points.Select(p => p.AreaCode));
        Assert.All(points, p => Assert.Equal("A", p.BuildingCode));
        Assert.DoesNotContain("token-", body); // facility-0060
    }

    [Fact]
    public async Task Open_round_without_a_scan_is_not_yet_done()
    {
        using var factory = new FacilityApiFactory();

        var point = Point(await DashboardAtAsync(factory, ThaiClock.At(8, 8, 30)), MenRestroom);

        Assert.Equal("NotYetDone", point.Status);
        Assert.Equal(new TimeOnly(7, 0), point.CurrentRound?.Start);
        Assert.Equal(new TimeOnly(9, 0), point.CurrentRound?.End);
        Assert.Equal(new TimeOnly(16, 0), point.NextRound?.Start);
        Assert.Null(point.LastScan);
        Assert.Null(point.Issue);
    }

    [Fact]
    public async Task Round_that_ended_without_a_scan_is_overdue()
    {
        using var factory = new FacilityApiFactory();

        var points = await DashboardAtAsync(factory, ThaiClock.At(8, 9, 1));

        Assert.Equal("Overdue", Point(points, MenRestroom).Status);
        Assert.Equal("NotYetDone", Point(points, MeetingRoom).Status); // its round runs until 10:00
    }

    [Fact]
    public async Task Night_shift_before_its_first_round_shows_the_next_round()
    {
        using var factory = new FacilityApiFactory();

        var point = Point(await DashboardAtAsync(factory, ThaiClock.At(8, 19, 30)), MenRestroom);

        Assert.Equal("BeforeFirstRound", point.Status);
        Assert.Equal(new TimeOnly(20, 0), point.NextRound?.Start);
    }

    [Fact]
    public async Task Day_only_area_is_off_hours_at_night()
    {
        using var factory = new FacilityApiFactory();

        var points = await DashboardAtAsync(factory, ThaiClock.At(8, 20, 30));

        Assert.Equal("OffHours", Point(points, MeetingRoom).Status);
        Assert.Equal("NotYetDone", Point(points, MenRestroom).Status);
    }

    [Fact]
    public async Task Night_round_after_midnight_belongs_to_the_shift_that_started_the_evening_before()
    {
        using var factory = new FacilityApiFactory();

        var point = Point(await DashboardAtAsync(factory, ThaiClock.At(9, 6, 30)), MenRestroom);

        Assert.Equal("Overdue", point.Status); // 03:00-05:00 of the night shift of 8 Oct ended unscanned
        Assert.Equal(new TimeOnly(3, 0), point.CurrentRound?.Start);
    }

    [Fact]
    public async Task Scan_in_the_round_waits_for_inspection()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));

        var point = Point(await DashboardAtAsync(factory, ThaiClock.At(8, 8, 30)), MenRestroom);

        Assert.Equal("PendingInspection", point.Status);
        Assert.Equal("สมชาย ใจดี", point.LastScan?.CleanerName);
        Assert.Equal("OnTime", point.LastScan?.Placement);
    }

    [Theory]
    [InlineData(InspectionResult.Passed, "Passed")]
    [InlineData(InspectionResult.Rework, "Rework")]
    public async Task Inspection_result_shows_on_the_card(InspectionResult result, string expectedStatus)
    {
        using var factory = new FacilityApiFactory();
        var scanId = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, scanId, result, ThaiClock.At(8, 8, 20));

        var point = Point(await DashboardAtAsync(factory, ThaiClock.At(8, 8, 30)), MenRestroom);

        Assert.Equal(expectedStatus, point.Status);
    }

    [Fact]
    public async Task Issue_is_a_tag_beside_the_status()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10), CleaningStatus.Issue, "wet_floor,bad_odor", "ก๊อกรั่ว");

        var point = Point(await DashboardAtAsync(factory, ThaiClock.At(8, 8, 30)), MenRestroom);

        Assert.Equal("PendingInspection", point.Status); // facility-0046: not a status
        Assert.Equal(new List<string> { "wet_floor", "bad_odor" }, point.Issue?.Tags);
        Assert.Equal("ก๊อกรั่ว", point.Issue?.Note);
    }

    [Fact]
    public async Task Next_normal_scan_clears_the_issue()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10), CleaningStatus.Issue, "wet_floor");
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 20));

        var point = Point(await DashboardAtAsync(factory, ThaiClock.At(8, 8, 30)), MenRestroom);

        Assert.Null(point.Issue);
    }

    [Fact]
    public async Task Any_logged_in_account_can_open_a_point_by_its_sign_token()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var point = await cleaner.GetFromJsonAsync<PointModel>("/api/service-points/by-token/token-restroom-m1");

        Assert.Equal(MenRestroom, point?.Name);
    }

    [Theory]
    [InlineData("token-checkin-ar01")]
    [InlineData("no-such-token")]
    public async Task Check_in_signs_and_unknown_tokens_are_not_points(string token)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.GetAsync("/api/service-points/by-token/" + token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Point_lookup_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().GetAsync("/api/service-points/by-token/token-restroom-m1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Yesterdays_scan_does_not_count_today()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(7, 8, 10));

        var point = Point(await DashboardAtAsync(factory, ThaiClock.At(8, 8, 30)), MenRestroom);

        Assert.Equal("NotYetDone", point.Status);
        Assert.Equal(ThaiClock.At(7, 8, 10).UtcDateTime, point.LastScan?.SubmittedAt);
    }

    [Fact]
    public async Task Issue_tag_follows_the_latest_scan_across_shifts()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 17, 0), CleaningStatus.Issue, "wet_floor");

        var point = Point(await DashboardAtAsync(factory, ThaiClock.At(8, 20, 30)), MenRestroom);

        Assert.NotNull(point.Issue); // the day-shift scan, seen from the night shift
    }

    [Fact]
    public async Task Deactivated_point_disappears_from_the_dashboard_and_the_lookup()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var point = await db.ServicePoints.SingleAsync(p => p.Name == MenRestroom);
            point.IsActive = false;
            await db.SaveChangesAsync();
        });

        var points = await DashboardAtAsync(factory, ThaiClock.At(8, 8, 30));
        var response = await cleaner.GetAsync("/api/service-points/by-token/token-restroom-m1");

        Assert.DoesNotContain(points, p => p.Name == MenRestroom);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_area_leaves_the_dashboard()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var area = await db.Areas.SingleAsync(a => a.Code == "AR01");
            area.IsActive = false;
            await db.SaveChangesAsync();
        });

        var points = await DashboardAtAsync(factory, ThaiClock.At(8, 8, 30));

        Assert.DoesNotContain(points, p => p.Name == MenRestroom);
    }
}
