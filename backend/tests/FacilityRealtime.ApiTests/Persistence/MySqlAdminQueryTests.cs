using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>
/// The alert and history queries use DATE ranges, nullable flags, IN lists and JSON columns. SQLite hides how the Oracle
/// MySQL provider treats them, so this runs both queries, and the alert_reviews unique key, on real MySQL.
/// </summary>
[Collection("MySQL")]
public class MySqlAdminQueryTests
{
    [MySqlFact]
    public async Task Alert_and_history_queries_run_on_MySQL()
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
            var shiftIn = new DateTime(2026, 10, 7, 23, 50, 0, DateTimeKind.Utc);   // 06:50 Thai on 8 Oct
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                var cleaner = await write.Users.SingleAsync(u => u.EmployeeId == "E1001");
                var admin = await write.Users.SingleAsync(u => u.Username == "admin");
                var checkIn = await write.Signs.SingleAsync(s => s.Code == "AR01-IN");
                var otherAreaSign = await write.Signs.SingleAsync(s => s.Code == "AR02-IN");
                var far = new ShiftAttendance
                {
                    UserId = cleaner.Id,
                    AreaId = cleaner.AreaId!.Value,
                    ShiftDate = new DateOnly(2026, 10, 8),
                    Shift = Shift.Day,
                    EventType = AttendanceEvent.ShiftIn,
                    OccurredAt = shiftIn,
                    Source = AttendanceSource.Scan,
                    SignId = checkIn.Id,
                    Latitude = 13.800000m,
                    Longitude = 100.600000m,
                    AccuracyM = 10,
                    DistanceM = 900,
                    WithinRadius = false,
                    CreatedAt = shiftIn,
                };
                var blocked = new BlockedScan { UserId = cleaner.Id, SignId = otherAreaSign.Id, Reason = BlockReason.OtherArea, ScannedAt = shiftIn.AddMinutes(20) };
                write.AddRange(far, blocked);
                await write.SaveChangesAsync();

                write.AlertReviews.Add(new AlertReview { AlertType = AlertType.BlockedScan, RecordId = blocked.Id, ReviewedById = admin.Id, ReviewedAt = shiftIn.AddMinutes(30) });
                AuditTrail.Add(write, admin.Id, shiftIn.AddMinutes(30), "ALERT_REVIEW", "blocked_scans", blocked.Id, "ปิดเตือน", new { Reviewed = false }, new { Reviewed = true });
                AuditTrail.Add(write, admin.Id, shiftIn.AddMinutes(31), "SIGN_RADIUS", "signs", checkIn.Id, "รัศมี", new { RadiusM = 50 }, new { RadiusM = 80 });
                await write.SaveChangesAsync();

                // alert_reviews: an alert is closed once
                write.AlertReviews.Add(new AlertReview { AlertType = AlertType.BlockedScan, RecordId = blocked.Id, ReviewedById = admin.Id, ReviewedAt = shiftIn });
                await Assert.ThrowsAsync<DbUpdateException>(() => write.SaveChangesAsync());
            }

            await using var read = new AppDbContext(options);
            var from = new DateTime(2026, 10, 6, 17, 0, 0, DateTimeKind.Utc);   // 7 Oct 00:00 Thai
            var to = new DateTime(2026, 10, 8, 17, 0, 0, DateTimeKind.Utc);     // 9 Oct 00:00 Thai
            var now = new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc);    // 20:00 Thai: the Day shift has ended

            var alerts = await AlertQuery.LoadAsync(read, from, to, now, new AttendanceSettings());

            Assert.Equal(new[] { AlertKind.MissingAttendance, AlertKind.BlockedScan, AlertKind.AttendanceGps }, alerts.Select(a => a.Kind));
            Assert.NotNull(alerts.Single(a => a.Kind == AlertKind.BlockedScan).Review);
            Assert.Null(alerts.Single(a => a.Kind == AlertKind.AttendanceGps).Review);
            Assert.Equal(new[] { AttendanceEvent.ShiftOut }, alerts.Single(a => a.Kind == AlertKind.MissingAttendance).MissingEvents);
            Assert.Equal("AR01", alerts.Single(a => a.Kind == AlertKind.MissingAttendance).Area.Code);

            var newest = Assert.Single(await AuditLogQuery.LoadAsync(
                read, new AuditLogFilter(from, to, ActorId: null, Actions: ["SIGN_RADIUS", "ALERT_REVIEW"], BeforeId: null, Limit: 1)));
            Assert.Equal("SIGN_RADIUS", newest.Entry.Action);
            Assert.Equal("ผู้ดูแลระบบ", newest.ActorName);
            Assert.Contains("80", newest.Entry.AfterJson);
            var older = await AuditLogQuery.LoadAsync(read, new AuditLogFilter(from, to, null, null, newest.Entry.Id, 10));
            Assert.Equal("ALERT_REVIEW", Assert.Single(older).Entry.Action);
            Assert.Single(await AuditLogQuery.ActorsAsync(read));
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
