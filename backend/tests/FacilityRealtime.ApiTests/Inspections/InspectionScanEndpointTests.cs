using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Inspections;

/// <summary>
/// facility-0047 rule 7, 0048: the Supervisor's scan. Seeded men's restroom (AR01, building A): Day rounds 07:00-09:00 and
/// 16:00-18:00, Night 20:00-22:00 and 03:00-05:00. TestData.AddScanAsync writes a submission by Cleaner 1000001.
/// </summary>
public class InspectionScanEndpointTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private static async Task<List<BlockedScan>> BlockedAsync(FacilityApiFactory factory)
    {
        var rows = new List<BlockedScan>();
        await factory.WithDbAsync(async db => rows = await db.BlockedScans.ToListAsync());
        return rows;
    }

    [Fact]
    public async Task Scanning_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await InspectionApi.ScanAsync(factory.CreateApiClient());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("cleaner")]
    [InlineData("admin")]
    public async Task Only_supervisors_inspect(string who)
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = who == "admin" ? await AuthApi.LoggedInAdminAsync(factory) : await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.ScanAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await BlockedAsync(factory));
    }

    [Fact]
    public async Task Location_is_required()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);

        var response = await supervisor.PostAsJsonAsync("/api/inspections/scan", new { qrToken = InspectionApi.MenRestroom });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("no-such-token")]
    [InlineData("token-checkin-ar01")]
    public async Task Unknown_or_check_in_tokens_are_404(string token)
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);

        var response = await InspectionApi.ScanAsync(supervisor, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Nothing_submitted_in_this_round_means_no_inspect_button()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.Null(scan.Submission);
        Assert.Equal("ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน", scan.Refusal);
        Assert.Equal("NotYetDone", scan.Point.Status);
        Assert.False(scan.IsCover);
        Assert.True(scan.WithinRadius);
        Assert.NotNull(scan.DistanceM);
        Assert.Empty(await BlockedAsync(factory));
    }

    [Fact]
    public async Task Far_from_the_sign_is_flagged_not_blocked()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.ScanAsync(supervisor, InspectionApi.MenRestroom, TestGps.FarLatitude, TestGps.FarLongitude);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scan = (await response.Content.ReadFromJsonAsync<InspectionScanModel>())!;
        Assert.False(scan.WithinRadius);
        Assert.True(scan.DistanceM > 1000);
        Assert.Empty(await BlockedAsync(factory));
    }

    [Fact]
    public async Task The_current_rounds_submission_is_offered()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(
            factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10), CleaningStatus.Issue, issueTags: "wet_floor", note: "ก๊อกรั่ว");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.Null(scan.Refusal);
        Assert.Equal(scanId, scan.Submission!.Id);
        Assert.Equal("สมชาย ใจดี", scan.Submission.CleanerName);
        Assert.Equal("Issue", scan.Submission.Status);
        Assert.Equal(new[] { "wet_floor" }, scan.Submission.IssueTags);
        Assert.Equal("ก๊อกรั่ว", scan.Submission.Note);
        Assert.Equal("PendingInspection", scan.Point.Status);
    }

    [Fact]
    public async Task Rework_still_owed_means_no_inspect_button()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, scanId, InspectionResult.Rework, ThaiClock.At(8, 8, 20));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.Null(scan.Submission);
        Assert.Equal("รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ", scan.Refusal);
        Assert.Equal("Rework", scan.Point.Status);
    }

    [Fact]
    public async Task An_earlier_rounds_work_is_not_offered()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 16, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.Null(scan.Submission);
        Assert.Equal("ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน", scan.Refusal);
    }

    [Fact]
    public async Task A_point_in_another_building_is_a_blocked_scan()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BlockedModel>())!;
        Assert.Equal("จุดนี้ไม่ได้อยู่ในตึกของคุณ", body.Message);
        Assert.Equal("OtherBuilding", body.Reason);
        Assert.Equal("B", body.BuildingCode);
        Assert.Equal("BR01", body.AreaCode);
        var blocked = Assert.Single(await BlockedAsync(factory));
        Assert.Equal(BlockReason.OtherBuilding, blocked.Reason);
        Assert.Equal(ThaiClock.At(8, 8, 30).UtcDateTime, blocked.ScannedAt);
        Assert.True(blocked.WithinRadius); // the GPS is kept like on every scan (facility-0037)
    }

    [Fact]
    public async Task The_own_building_outside_the_own_shift_is_a_blocked_scan()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));

        var response = await InspectionApi.ScanAsync(supervisor);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("OutsideShift", (await response.Content.ReadFromJsonAsync<BlockedModel>())!.Reason);
        Assert.Equal(BlockReason.OutsideShift, Assert.Single(await BlockedAsync(factory)).Reason);
    }

    [Fact]
    public async Task A_cover_lets_the_supervisor_inspect_another_building()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorCoverAsync(factory, "2000001", "B", Oct8, Shift.Day);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor, InspectionApi.BuildingBPoint);

        Assert.True(scan.IsCover);
        Assert.Equal("B", scan.Point.BuildingCode);
        Assert.Empty(await BlockedAsync(factory));
    }

    [Fact]
    public async Task A_night_cover_of_the_own_building_lets_the_day_supervisor_inspect_at_night()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorCoverAsync(factory, "2000001", "A", Oct8, Shift.Night);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.True(scan.IsCover);
    }

    [Theory]
    [InlineData(Shift.Night, false)]
    [InlineData(Shift.Day, true)]
    public async Task A_cover_of_another_shift_or_a_cancelled_one_does_not_count(Shift coverShift, bool cancelled)
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorCoverAsync(factory, "2000001", "B", Oct8, coverShift, cancelled);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
