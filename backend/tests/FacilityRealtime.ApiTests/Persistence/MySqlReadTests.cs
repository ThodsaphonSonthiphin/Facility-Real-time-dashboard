using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>The Oracle MySQL provider returns DATE/TIME as DateTime/TimeSpan; SQLite hides that, so this reads DateOnly/TimeOnly back from real MySQL.</summary>
[Collection("MySQL")]
public class MySqlReadTests
{
    [MySqlFact]
    public async Task DateOnly_and_TimeOnly_columns_are_read_back_from_MySQL()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(Environment.GetEnvironmentVariable("FACILITY_MYSQL_TEST")!)
            .Options;

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.EnsureCreatedAsync();
        }

        try
        {
            int userId, pointId, signId, area2Id;
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                var sign = await write.Signs.SingleAsync(s => s.QrToken == "token-restroom-m1");
                signId = sign.Id;
                pointId = sign.ServicePointId!.Value;
                userId = (await write.Users.FirstAsync()).Id;
                write.ScanRecords.Add(new ScanRecord
                {
                    ServicePointId = pointId,
                    SignId = signId,
                    UserId = userId,
                    ShiftDate = new DateOnly(2026, 10, 8),
                    Shift = Shift.Day,
                    RoundStart = new TimeOnly(7, 0),
                    RoundEnd = new TimeOnly(9, 0),
                    Placement = Placement.OnTime,
                    SubmittedAt = DateTime.UtcNow,
                });
                await write.SaveChangesAsync();
                var checkInSign = await write.Signs.SingleAsync(s => s.QrToken == "token-checkin-ar01");
                var cleaner = await write.Users.SingleAsync(u => u.EmployeeId == "E1001");
                var area2 = await write.Areas.SingleAsync(a => a.Code == "AR02");
                write.ShiftAttendances.Add(new ShiftAttendance
                {
                    UserId = cleaner.Id,
                    AreaId = cleaner.AreaId!.Value,
                    ShiftDate = new DateOnly(2026, 10, 8),
                    Shift = Shift.Day,
                    EventType = AttendanceEvent.ShiftIn,
                    OccurredAt = DateTime.UtcNow,
                    Source = AttendanceSource.Scan,
                    SignId = checkInSign.Id,
                    Latitude = 13.756300m,
                    Longitude = 100.501800m,
                    AccuracyM = 10,
                    DistanceM = 0,
                    WithinRadius = true,
                    CreatedAt = DateTime.UtcNow,
                });
                area2Id = area2.Id;
                var coverAssignment = new CoverAssignment
                {
                    UserId = cleaner.Id,
                    AreaId = area2.Id,
                    ShiftDate = new DateOnly(2026, 10, 8),
                    Shift = Shift.Day,
                    AssignedById = userId,
                    AssignedAt = DateTime.UtcNow,
                };
                write.CoverAssignments.Add(coverAssignment);
                await write.SaveChangesAsync();
                AuditTrail.Add(write, userId, DateTime.UtcNow, "COVER_ASSIGN", "cover_assignments", coverAssignment.Id, "test cover", before: null, after: new { AreaId = area2.Id });
                await write.SaveChangesAsync();
            }

            await using var read = new AppDbContext(options);
            var windows = await read.PointRoundWindows.AsNoTracking().ToListAsync();
            Assert.NotEmpty(windows);
            Assert.All(windows, w => Assert.NotEqual(w.StartTime, w.EndTime));

            var scan = await read.ScanRecords.AsNoTracking().SingleAsync();
            Assert.Equal(new DateOnly(2026, 10, 8), scan.ShiftDate);
            Assert.Equal(new TimeOnly(7, 0), scan.RoundStart);
            Assert.Equal(new TimeOnly(9, 0), scan.RoundEnd);

            var attendance = await read.ShiftAttendances.AsNoTracking().SingleAsync();
            Assert.Equal(new DateOnly(2026, 10, 8), attendance.ShiftDate);
            Assert.Equal(AttendanceEvent.ShiftIn, attendance.EventType);
            Assert.Equal(13.756300m, attendance.Latitude);
            Assert.True(attendance.WithinRadius);

            var cover = await read.CoverAssignments.AsNoTracking().SingleAsync();
            Assert.Equal(new DateOnly(2026, 10, 8), cover.ShiftDate);

            var entry = await read.AuditLog.AsNoTracking().SingleAsync();
            Assert.Equal("COVER_ASSIGN", entry.Action);
            Assert.Equal(area2Id, JsonDocument.Parse(entry.AfterJson!).RootElement.GetProperty("AreaId").GetInt32());
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
