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
}
