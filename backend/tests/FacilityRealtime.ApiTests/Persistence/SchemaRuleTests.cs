using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>Rules docs/design/database.html puts in the database itself ("กติกาที่ใครกัน"), so an app bug cannot break them.</summary>
public class SchemaRuleTests
{
    [Fact]
    public async Task An_area_has_one_check_in_sign()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var area = await db.Areas.SingleAsync(a => a.Code == "AR01");
            db.Signs.Add(new Sign { AreaId = area.Id, Code = "AR01-IN2", QrToken = "token-second-checkin", QrIssuedAt = DateTime.UtcNow });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Point_signs_are_not_check_in_signs()
    {
        using var factory = new FacilityApiFactory();
        var checkinAreaIds = new List<int?>();

        await factory.WithDbAsync(async db =>
            checkinAreaIds = await db.Signs.Where(s => s.ServicePointId != null).Select(s => s.CheckinAreaId).ToListAsync());

        Assert.Equal(3, checkinAreaIds.Count);
        Assert.All(checkinAreaIds, id => Assert.Null(id));
    }

    [Fact]
    public async Task A_service_point_has_one_sign()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.SingleAsync(s => s.QrToken == "token-restroom-m1");
            db.Signs.Add(new Sign
            {
                AreaId = sign.AreaId,
                ServicePointId = sign.ServicePointId,
                Code = "AR01-01B",
                QrToken = "token-second-m1",
                QrIssuedAt = DateTime.UtcNow,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Rework_inspection_must_name_the_defect()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.SingleAsync(s => s.QrToken == "token-restroom-m1");
            var user = await db.Users.FirstAsync();
            var scan = new ScanRecord
            {
                ServicePointId = sign.ServicePointId!.Value,
                SignId = sign.Id,
                UserId = user.Id,
                ShiftDate = new DateOnly(2026, 10, 8),
                Shift = Shift.Day,
                Placement = Placement.OnTime,
                SubmittedAt = DateTime.UtcNow,
            };
            db.ScanRecords.Add(scan);
            await db.SaveChangesAsync();

            db.InspectionRecords.Add(new InspectionRecord
            {
                ScanRecordId = scan.Id,
                ServicePointId = scan.ServicePointId,
                SupervisorId = user.Id,
                Result = InspectionResult.Rework,
                Defect = null,
                InspectedAt = DateTime.UtcNow,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task An_area_has_one_active_cleaner_per_shift()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var area = await db.Areas.SingleAsync(a => a.Code == "AR01");
            db.Users.Add(Account(UserRole.Cleaner, "E1901", areaId: area.Id, shift: Shift.Day)); // E1001 holds AR01 Day

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Deactivating_a_cleaner_frees_the_slot()
    {
        using var factory = new FacilityApiFactory();
        var activeDayCleaners = 0;

        await factory.WithDbAsync(async db =>
        {
            var current = await db.Users.SingleAsync(u => u.EmployeeId == "E1001");
            current.IsActive = false;
            await db.SaveChangesAsync();

            db.Users.Add(Account(UserRole.Cleaner, "E1901", areaId: current.AreaId, shift: Shift.Day));
            await db.SaveChangesAsync();

            activeDayCleaners = await db.Users.CountAsync(u => u.AreaId == current.AreaId && u.Shift == Shift.Day && u.IsActive);
        });

        Assert.Equal(1, activeDayCleaners);
    }

    [Fact]
    public async Task A_building_has_one_active_supervisor_per_shift()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var building = await db.Buildings.SingleAsync(b => b.Code == "A");
            db.Users.Add(Account(UserRole.Supervisor, "S2901", buildingId: building.Id, shift: Shift.Day)); // S2001 holds A Day

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Admin_needs_a_username()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(Account(UserRole.Admin, employeeId: null));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Cleaner_needs_an_employee_id()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var area = await db.Areas.SingleAsync(a => a.Code == "AR02");
            db.Users.Add(Account(UserRole.Cleaner, employeeId: null, areaId: area.Id, shift: Shift.Night));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    private static User Account(UserRole role, string? employeeId, int? areaId = null, int? buildingId = null, Shift? shift = null) => new()
    {
        Role = role,
        EmployeeId = employeeId,
        DisplayName = "บัญชีทดสอบ",
        SecretHash = "not-a-real-hash",
        AreaId = areaId,
        BuildingId = buildingId,
        Shift = shift,
        CreatedAt = DateTime.UtcNow,
    };
}
