using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Inspections;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0037, 0041, 0048 on the alert list (wireframe D3): inspections far from the sign, and Supervisors' Blocked Scans.</summary>
public class InspectionAlertTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    [Fact]
    public async Task An_inspection_far_from_the_sign_is_an_alert_the_admin_closes()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var inspected = await InspectionApi.InspectAsync(supervisor, scanId, "Passed", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);
        var inspectionId = (await inspected.Content.ReadFromJsonAsync<InspectionCreatedModel>())!.InspectionId;

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("InspectionGps", alert.Kind);
        Assert.Equal(inspectionId, alert.RecordId);
        Assert.Equal("2000001", alert.Person.EmployeeId);
        Assert.Equal("Supervisor", alert.Person.Role);
        Assert.Equal("AR01", alert.AreaCode);
        Assert.Equal("AR01-01", alert.SignCode);
        Assert.Equal("ห้องน้ำชาย ชั้น 1", alert.PointName);
        Assert.True(alert.DistanceM > 1000);
        Assert.Null(alert.Cover);
        Assert.Null(alert.BlockReason);
        Assert.False(alert.Reviewed);

        var review = await AdminAttendanceApi.ReviewAsync(admin, "InspectionGps", inspectionId, "GPS ในอาคารเพี้ยน");

        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        Assert.True(Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Reviewed);
        var audit = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ALERT_REVIEW", audit.Action);
        Assert.Equal("inspections", audit.EntityType);
        Assert.Equal("ปิดเตือนตรวจงานไกลจากป้าย ของ สมปอง ตรวจดี เวลา 08:30", audit.Summary);
    }

    [Fact]
    public async Task An_inspection_inside_the_radius_is_no_alert_and_cannot_be_closed()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var inspected = await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        Assert.Empty(await AdminAttendanceApi.AlertsAsync(admin));
        Assert.Equal(HttpStatusCode.NotFound, (await AdminAttendanceApi.ReviewAsync(admin, "InspectionGps", inspected.InspectionId)).StatusCode);
    }

    [Fact]
    public async Task A_supervisors_blocked_scan_says_why_and_offers_a_building_cover()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        Assert.Equal(HttpStatusCode.Forbidden, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("BlockedScan", alert.Kind);
        Assert.Equal("OtherBuilding", alert.BlockReason);
        Assert.Equal("BR01", alert.AreaCode);
        Assert.Equal(
            new CoverSuggestionModel(alert.Person.Id, null, await AdminSetupApi.BuildingIdAsync(factory, "B"), Oct8, "Day"),
            alert.Cover);

        var assigned = await admin.PostAsJsonAsync("/api/admin/cover-assignments", new
        {
            userId = alert.Cover!.UserId,
            buildingId = alert.Cover.BuildingId,
            shiftDate = alert.Cover.ShiftDate,
            shift = alert.Cover.Shift,
            blockedScanId = alert.RecordId,
        });

        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        var after = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));
        Assert.True(after.Reviewed);
        Assert.Null(after.Cover);
    }

    [Fact]
    public async Task Outside_the_shift_it_offers_a_cover_of_the_own_building_for_that_shift()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));
        await InspectionApi.ScanAsync(supervisor);

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("OutsideShift", alert.BlockReason);
        Assert.Equal(new CoverSuggestionModel(alert.Person.Id, null, await AdminSetupApi.BuildingIdAsync(factory, "A"), Oct8, "Night"), alert.Cover);
    }

    [Fact]
    public async Task Reviewing_a_scan_into_another_building_is_logged_as_a_wrong_building_scan()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint);
        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        var review = await AdminAttendanceApi.ReviewAsync(admin, "BlockedScan", alert.RecordId);

        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.StartsWith("ปิดเตือนสแกนผิดตึก ของ ", entry.Summary); // named by its reason (facility-0085)
    }

    [Fact]
    public async Task Reviewing_a_scan_outside_the_shift_is_logged_as_a_scan_outside_the_shift()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));
        await InspectionApi.ScanAsync(supervisor);
        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        var review = await AdminAttendanceApi.ReviewAsync(admin, "BlockedScan", alert.RecordId);

        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.StartsWith("ปิดเตือนสแกนนอกกะ ของ ", entry.Summary); // named by its reason (facility-0085)
    }

    [Fact]
    public async Task No_cover_is_offered_once_that_shift_has_ended()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));
        await InspectionApi.ScanAsync(supervisor);
        factory.Clock.SetUtcNow(ThaiClock.At(9, 7, 30));

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Null(alert.Cover);
    }

    [Fact]
    public async Task No_cover_is_offered_for_the_supervisors_own_building_and_shift()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));
        await InspectionApi.ScanAsync(supervisor);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "2000001")).Shift = Shift.Night;
            await db.SaveChangesAsync();
        });

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("OutsideShift", alert.BlockReason);
        Assert.Null(alert.Cover);
    }
}
