using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Retention;

[Collection("MySQL")]
public class MySqlRetentionTests
{
    [MySqlFact]
    public async Task Migrations_GPS_bulk_mutation_summary_transaction_and_typed_cleanup_run_on_MySQL()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            await DbInitializer.SeedAsync(db, new Pbkdf2PasswordHasher(iterations: 1_000));
            var clock = new TestTimeProvider(new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero));
            var sign = await db.Signs.FirstAsync(s => s.ServicePointId != null);
            var user = await db.Users.FirstAsync(u => u.Role == UserRole.Cleaner);
            var supervisor = await db.Users.FirstAsync(u => u.Role == UserRole.Supervisor);
            var point = await db.ServicePoints.Include(p => p.Area).SingleAsync(p => p.Id == sign.ServicePointId);
            point.CreatedAt = new(2025, 10, 1); point.Area!.CreatedAt = new(2020, 1, 1); point.Area.ShiftPattern = ShiftPattern.DayOnly;
            foreach (var window in await db.PointRoundWindows.Where(w => w.ServicePointId == point.Id).ToListAsync()) window.CreatedAt = point.CreatedAt;
            var source = new ScanRecord { ServicePointId = point.Id, SignId = sign.Id, UserId = user.Id, ShiftDate = new(2025, 10, 1),
                RoundStart = new(7, 0), RoundEnd = new(9, 0), SubmittedAt = new(2025, 10, 1, 1, 0, 0), Latitude = 1, AccuracyM = 10, DistanceM = 5 };
            var inspection = new InspectionRecord { ScanRecord = source, ServicePointId = point.Id, SupervisorId = supervisor.Id,
                Result = InspectionResult.Passed, InspectedAt = source.SubmittedAt.AddMinutes(1), Longitude = 1 };
            db.ScanRecords.Add(source); db.InspectionRecords.Add(inspection);
            var retained = new ScanRecord { ServicePointId = point.Id, SignId = sign.Id, UserId = user.Id, ShiftDate = new(2026, 8, 1),
                SubmittedAt = clock.GetUtcNow().UtcDateTime.AddDays(-90), Latitude = 1, Longitude = 2, AccuracyM = 10, DistanceM = 5, WithinRadius = false };
            db.ScanRecords.Add(retained);
            db.ShiftAttendances.Add(new() { UserId = user.Id, AreaId = sign.AreaId, ShiftDate = new(2025, 10, 1), OccurredAt = source.SubmittedAt, Latitude = 1 });
            db.BlockedScans.Add(new() { UserId = user.Id, SignId = sign.Id, ScannedAt = source.SubmittedAt, Latitude = 1 });
            await db.SaveChangesAsync();
            db.AlertReviews.Add(new() { AlertType = AlertType.SubmissionGps, RecordId = source.Id, ReviewedById = supervisor.Id });
            db.AlertReviews.Add(new() { AlertType = AlertType.InspectionGps, RecordId = inspection.Id, ReviewedById = supervisor.Id });
            db.AuditLog.Add(new() { EntityType = "scan_records", EntityId = source.Id, Action = "TEST", Summary = "synthetic" });
            await db.SaveChangesAsync();
            var result = await new RetentionRunner(db, clock, new() { BatchSize = 1 }).RunAsync(default);
            Assert.Equal(5, result.GpsCleared); Assert.Equal(1, result.SummariesCreated);
            db.ChangeTracker.Clear();
            var row = Assert.Single(await db.ScanRecords.ToListAsync());
            Assert.Equal(retained.Id, row.Id); Assert.Null(row.Latitude); Assert.Null(row.Longitude); Assert.Null(row.AccuracyM);
            Assert.Equal(((short?)5, (bool?)false), (row.DistanceM, row.WithinRadius));
            Assert.Empty(await db.InspectionRecords.ToListAsync()); Assert.Empty(await db.ShiftAttendances.ToListAsync());
            Assert.Empty(await db.BlockedScans.ToListAsync()); Assert.Empty(await db.AlertReviews.ToListAsync()); Assert.Empty(await db.AuditLog.ToListAsync());
            var summary = await db.PointMonthlySummaries.SingleAsync(); Assert.Equal(1, summary.SubmittedOnTime); Assert.Equal(1, summary.InspectedPass);
            Assert.Equal(0, (await new RetentionRunner(db, clock, new()).RunAsync(default)).SummariesCreated);
            // A late dependent write must not escape cleanup merely because its point/month marker already exists.
            db.AlertReviews.Add(new() { AlertType = AlertType.InspectionGps, RecordId = retained.Id, ReviewedById = supervisor.Id });
            db.AlertReviews.Add(new() { AlertType = AlertType.SubmissionGps, RecordId = retained.Id, ReviewedById = supervisor.Id });
            db.AuditLog.AddRange(
                new AuditEntry { EntityType = "inspections", EntityId = retained.Id, Action = "TEST", Summary = "late orphan" },
                new AuditEntry { EntityType = "scan_records", EntityId = retained.Id, Action = "TEST", Summary = "live source" },
                new AuditEntry { EntityType = "cover_assignments", EntityId = retained.Id, Action = "TEST", Summary = "late cover orphan" },
                new AuditEntry { EntityType = "scan_records", EntityId = null, Action = "TEST", Summary = "null identity" },
                new AuditEntry { EntityType = "users", EntityId = retained.Id, Action = "TEST", Summary = "setup" });
            await db.SaveChangesAsync();
            await new RetentionRunner(db, clock, new() { BatchSize = 1 }).RunAsync(default);
            Assert.Equal(AlertType.SubmissionGps, (await db.AlertReviews.SingleAsync()).AlertType);
            Assert.Equal(3, await db.AuditLog.CountAsync());
            Assert.False(await db.AuditLog.AnyAsync(a => a.EntityType == "inspections" || a.EntityType == "cover_assignments"));
            await new RetentionRunner(db, clock, new()).RunAsync(default);
            Assert.Single(await db.AlertReviews.ToListAsync()); Assert.Equal(3, await db.AuditLog.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
