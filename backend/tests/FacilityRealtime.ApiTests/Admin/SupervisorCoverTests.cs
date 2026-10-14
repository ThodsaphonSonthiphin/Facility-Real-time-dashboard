using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Inspections;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// facility-0048 rule 2 and its amendment: the Admin lets a Supervisor inspect another building, or their own building
/// outside their shift, for one shift. Seeded S2001 is the Day Supervisor of building A.
/// </summary>
public class SupervisorCoverTests
{
    private const string Path = "/api/admin/cover-assignments";
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private static async Task<HttpResponseMessage> AssignAsync(
        FacilityApiFactory factory, HttpClient admin, string buildingCode, string shift, long? blockedScanId = null) =>
        await admin.PostAsJsonAsync(Path, new
        {
            userId = await AdminSetupApi.UserIdAsync(factory, "S2001"),
            buildingId = await AdminSetupApi.BuildingIdAsync(factory, buildingCode),
            shiftDate = Oct8,
            shift,
            blockedScanId,
        });

    [Fact]
    public async Task Admin_lets_a_supervisor_inspect_another_building_and_it_is_logged()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, "B", "Day");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cover = (await response.Content.ReadFromJsonAsync<CoverModel>())!;
        Assert.Equal("สมปอง ตรวจดี", cover.PersonName);
        Assert.Equal("B", cover.BuildingCode);
        Assert.Null(cover.AreaId);
        Assert.Null(cover.AreaCode);
        var audit = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("COVER_ASSIGN", audit.Action);
        Assert.Equal("มอบหมาย สมปอง ตรวจดี ตรวจแทนตึก B กะเช้า 2026-10-08", audit.Summary);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        Assert.True((await InspectionApi.ScannedAsync(supervisor, InspectionApi.BuildingBPoint)).IsCover);
    }

    [Fact]
    public async Task The_own_building_in_the_own_shift_needs_no_cover()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, "A", "Day");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_own_building_in_the_other_shift_can_be_covered()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, "A", "Night");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_supervisor_cover_names_a_building_and_no_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var userId = await AdminSetupApi.UserIdAsync(factory, "S2001");

        var noBuilding = await admin.PostAsJsonAsync(Path, new { userId, shiftDate = Oct8, shift = "Night" });
        var withArea = await admin.PostAsJsonAsync(Path, new
        {
            userId,
            areaId = await AdminSetupApi.AreaIdAsync(factory, "AR01"),
            buildingId = await AdminSetupApi.BuildingIdAsync(factory),
            shiftDate = Oct8,
            shift = "Night",
        });

        Assert.Equal(HttpStatusCode.BadRequest, noBuilding.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withArea.StatusCode);
    }

    [Fact]
    public async Task A_cleaner_cover_names_an_area_and_no_building()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var userId = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var withBuilding = await admin.PostAsJsonAsync(Path, new
        {
            userId,
            areaId = await AdminSetupApi.AreaIdAsync(factory, "AR02"),
            buildingId = await AdminSetupApi.BuildingIdAsync(factory),
            shiftDate = Oct8,
            shift = "Day",
        });
        var noArea = await admin.PostAsJsonAsync(Path, new { userId, shiftDate = Oct8, shift = "Day" });

        Assert.Equal(HttpStatusCode.BadRequest, withBuilding.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noArea.StatusCode);
    }

    [Fact]
    public async Task The_same_cover_twice_is_409()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        Assert.Equal(HttpStatusCode.Created, (await AssignAsync(factory, admin, "B", "Day")).StatusCode);

        var again = await AssignAsync(factory, admin, "B", "Day");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_shift_that_has_ended_is_refused()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 19, 0));

        var response = await AssignAsync(factory, admin, "B", "Day");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_ends_the_cover_and_is_logged()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var cover = (await (await AssignAsync(factory, admin, "B", "Day")).Content.ReadFromJsonAsync<CoverModel>())!;

        var response = await admin.DeleteAsync($"{Path}/{cover.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("ยกเลิกการตรวจแทนของ สมปอง ตรวจดี ที่ตึก B กะเช้า 2026-10-08", (await AdminSetupApi.AuditAsync(factory))[1].Summary);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        Assert.Equal(HttpStatusCode.Forbidden, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);
    }

    [Fact]
    public async Task Assigning_from_the_supervisors_blocked_scan_closes_its_alert()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        Assert.Equal(HttpStatusCode.Forbidden, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);
        var blockedId = await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.Select(b => b.Id).SingleAsync());

        var response = await AssignAsync(factory, admin, "B", "Day", blockedId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var audit = await AdminSetupApi.AuditAsync(factory);
        Assert.Equal(new[] { "COVER_ASSIGN", "ALERT_REVIEW" }, audit.Select(a => a.Action));
        Assert.Equal("ปิดเตือนสแกนนอก Area ของ สมปอง ตรวจดี เวลา 08:30: มอบหมายตรวจแทนตึก B แล้ว", audit[1].Summary);
        Assert.Equal(HttpStatusCode.OK, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);
    }

    [Theory]
    [InlineData("A", "Night")]
    [InlineData("B", "Night")]
    public async Task A_blocked_scan_of_another_shift_does_not_fit(string buildingCode, string shift)
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint); // blocked at B in the Day shift
        var blockedId = await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.Select(b => b.Id).SingleAsync());

        var response = await AssignAsync(factory, admin, buildingCode, shift, blockedId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.CoverAssignments.ToListAsync()));
    }

    [Fact]
    public async Task A_blocked_scan_at_another_building_does_not_fit()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddBuildingAsync(factory, "C");
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint); // blocked at B in the Day shift
        var blockedId = await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.Select(b => b.Id).SingleAsync());

        var response = await AssignAsync(factory, admin, "C", "Day", blockedId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.CoverAssignments.ToListAsync()));
    }

    [Fact]
    public async Task Another_users_blocked_scan_does_not_fit()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "E1001");
            var sign = await db.Signs.SingleAsync(s => s.Code == "BR01-01");
            db.BlockedScans.Add(new BlockedScan
            {
                UserId = cleaner.Id, SignId = sign.Id, Reason = BlockReason.OtherArea, ScannedAt = ThaiClock.At(8, 8, 30).UtcDateTime,
            });
            await db.SaveChangesAsync();
        });
        var blockedId = await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.Select(b => b.Id).SingleAsync());

        var response = await AssignAsync(factory, admin, "B", "Day", blockedId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.CoverAssignments.ToListAsync()));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task An_unknown_building_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await admin.PostAsJsonAsync(Path, new
        {
            userId = await AdminSetupApi.UserIdAsync(factory, "S2001"),
            buildingId = 9999,
            shiftDate = Oct8,
            shift = "Day",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_be_given_a_cover()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var adminId = await AdminSetupApi.ReadAsync(factory, db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

        var response = await admin.PostAsJsonAsync(Path, new
        {
            userId = adminId,
            areaId = await AdminSetupApi.AreaIdAsync(factory, "AR02"),
            shiftDate = Oct8,
            shift = "Day",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_building_cover_stays_off_the_attendance_board()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        Assert.Equal(HttpStatusCode.Created, (await AssignAsync(factory, admin, "B", "Day")).StatusCode);

        var board = await AdminAttendanceApi.BoardAsync(admin, "?shiftDate=2026-10-08&shift=Day");

        Assert.DoesNotContain(board.Rows, r => r.State == "Covered");
        Assert.All(board.Rows, r => Assert.Empty(r.Covers));
    }
}
