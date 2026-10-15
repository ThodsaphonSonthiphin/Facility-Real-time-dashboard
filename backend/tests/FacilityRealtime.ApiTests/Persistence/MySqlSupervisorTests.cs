using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>
/// Plan 6 on the Oracle MySQL provider: the one-place CHECK on cover_assignments, nullable area_id/building_id compared
/// with parameters (the duplicate-cover check), the inspection history joins, the alert list's inspection flags and a
/// Supervisor's Blocked Scan, and the defect read with the round facts. SQLite hides how MySQL treats these.
/// </summary>
[Collection("MySQL")]
public class MySqlSupervisorTests
{
    [MySqlFact]
    public async Task Supervisor_queries_and_rules_run_on_MySQL()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(MySqlTestDatabase.ConnectionString())
            .Options;

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.EnsureCreatedAsync();
        }

        try
        {
            var oct8 = new DateOnly(2026, 10, 8);
            var at = new DateTime(2026, 10, 8, 1, 10, 0, DateTimeKind.Utc); // 08:10 Thai
            int supervisorId, buildingAId, buildingBId, pointId;
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                var cleaner = await write.Users.SingleAsync(u => u.EmployeeId == "E1001");
                var supervisor = await write.Users.SingleAsync(u => u.EmployeeId == "S2001");
                var admin = await write.Users.SingleAsync(u => u.Username == "admin");
                var sign = await write.Signs.SingleAsync(s => s.Code == "AR01-01");
                var window = (await write.PointRoundWindows.Where(w => w.ServicePointId == sign.ServicePointId && w.Shift == Shift.Day).ToListAsync())
                    .Single(w => w.StartTime == new TimeOnly(7, 0));
                var buildingB = new Building { Code = "B", Name = "ตึก B", CreatedAt = at };
                write.Buildings.Add(buildingB);
                await write.SaveChangesAsync();

                var cover = new CoverAssignment
                {
                    UserId = supervisor.Id, BuildingId = buildingB.Id, ShiftDate = oct8, Shift = Shift.Day, AssignedById = admin.Id, AssignedAt = at,
                };
                var scan = new ScanRecord
                {
                    ServicePointId = sign.ServicePointId!.Value, SignId = sign.Id, UserId = cleaner.Id, ShiftDate = oct8, Shift = Shift.Day,
                    RoundWindowId = window.Id, RoundStart = window.StartTime, RoundEnd = window.EndTime, Placement = Placement.OnTime, SubmittedAt = at,
                };
                write.CoverAssignments.Add(cover);
                write.ScanRecords.Add(scan);
                await write.SaveChangesAsync();
                write.InspectionRecords.Add(new InspectionRecord
                {
                    ScanRecordId = scan.Id, ServicePointId = scan.ServicePointId, SupervisorId = supervisor.Id, Result = InspectionResult.Rework,
                    Defect = "พื้นยังเปียก", InspectedAt = at.AddMinutes(10), CoverAssignmentId = cover.Id,
                    Latitude = 13.800000m, Longitude = 100.600000m, AccuracyM = 10, DistanceM = 12000, WithinRadius = false,
                });
                write.BlockedScans.Add(new BlockedScan
                {
                    UserId = supervisor.Id, SignId = sign.Id, Reason = BlockReason.OutsideShift, ScannedAt = at.AddHours(12), DistanceM = 5, WithinRadius = true,
                });
                await write.SaveChangesAsync();

                // CHECK ck_cover_assignments_one_place: neither an Area nor a building is refused
                write.CoverAssignments.Add(new CoverAssignment
                {
                    UserId = supervisor.Id, ShiftDate = oct8, Shift = Shift.Night, AssignedById = admin.Id, AssignedAt = at,
                });
                await Assert.ThrowsAsync<DbUpdateException>(() => write.SaveChangesAsync());

                supervisorId = supervisor.Id;
                buildingAId = supervisor.BuildingId!.Value;
                buildingBId = buildingB.Id;
                pointId = scan.ServicePointId;
            }

            await using (var read = new AppDbContext(options))
            {
                // The duplicate-cover check compares nullable columns with parameters, as CoverAssignmentEndpoints does
                int? noArea = null;
                int? building = buildingBId;
                Assert.True(await read.CoverAssignments.AnyAsync(c =>
                    c.UserId == supervisorId && c.AreaId == noArea && c.BuildingId == building && c.ShiftDate == oct8 && c.Shift == Shift.Day && c.CancelledAt == null));

                var submissions = await InspectionHistoryQuery.LoadRoundSubmissionsAsync(read, oct8, oct8, buildingAId);
                Assert.Equal(new TimeOnly(7, 0), Assert.Single(submissions).RoundStart);

                var inspection = Assert.Single(await InspectionHistoryQuery.LoadInspectionsAsync(read, oct8, oct8, buildingAId));
                Assert.Equal(("S2001", true, false, "พื้นยังเปียก"), (inspection.EmployeeId, inspection.IsCover, inspection.WithinRadius!.Value, inspection.Defect));
                Assert.Equal("AR01-01", inspection.SignCode);
                Assert.Empty(await InspectionHistoryQuery.LoadInspectionsAsync(read, oct8, oct8, buildingBId));

                var places = await InspectionHistoryQuery.LoadPointPlacesAsync(read, buildingAId);
                Assert.Equal(3, places.Count);
                Assert.True(places.All(p => p.Counted)); // the seed has no deactivated point or Area
                Assert.True(places.Single(p => p.SignCode == "AR01-01").WorksNight);

                var facts = (await RoundFactsQuery.LoadAsync(read, [pointId], new ShiftSlot(oct8, Shift.Day)))[pointId];
                Assert.Equal("พื้นยังเปียก", Assert.Single(facts.Inspections).Defect);

                var from = new DateTime(2026, 10, 7, 17, 0, 0, DateTimeKind.Utc); // 8 Oct 00:00 Thai
                var alerts = await AlertQuery.LoadAsync(read, from, from.AddDays(1), at.AddHours(13), new AttendanceSettings());
                Assert.Contains(alerts, a => a.Kind == AlertKind.InspectionGps && a.Person.EmployeeId == "S2001" && a.Sign!.Code == "AR01-01");
                var blocked = Assert.Single(alerts, a => a.Kind == AlertKind.BlockedScan);
                Assert.Equal(BlockReason.OutsideShift, blocked.BlockReason);
                Assert.Equal(new ShiftSlot(oct8, Shift.Night), blocked.CoverSlot);
            }
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
