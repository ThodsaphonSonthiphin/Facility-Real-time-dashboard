using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Retention;

public class RetentionOrphanTests
{
    [Fact]
    public async Task Every_run_sweeps_late_typed_orphans_after_completed_archive_preserving_live_other_types_setup_and_null_ids()
    {
        using var factory = new FacilityApiFactory();
        var ids = await AnnualRetentionTests.PrepareAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var old = AnnualRetentionTests.Scan(ids, 1); old.Id = 100;
            db.ScanRecords.Add(old); await db.SaveChangesAsync();
            await new RetentionRunner(db, factory.Clock, new() { BatchSize = 1 }).RunAsync(default);
            Assert.True(await db.PointMonthlySummaries.AnyAsync(s => s.ServicePointId == ids.Point));
            Assert.Empty(await db.ScanRecords.ToListAsync());
            db.ChangeTracker.Clear();
            // Emulate dependent writes completing after source deletion. Matching numeric IDs in OTHER tables cannot protect them.
            var live = AnnualRetentionTests.Scan(ids, 1); live.Id = 100; live.ShiftDate = new(2026, 11, 3); live.SubmittedAt = factory.Clock.GetUtcNow().UtcDateTime;
            db.ScanRecords.Add(live);
            var sign = await db.Signs.SingleAsync(s => s.Id == ids.Sign);
            db.ShiftAttendances.Add(new() { Id = 101, AreaId = sign.AreaId, UserId = ids.User, ShiftDate = new(2026, 11, 3), OccurredAt = live.SubmittedAt });
            foreach (var type in Enum.GetValues<AlertType>()) db.AlertReviews.Add(new() { AlertType = type, RecordId = 100, ReviewedById = ids.Supervisor });
            db.AlertReviews.Add(new() { AlertType = AlertType.SubmissionGps, RecordId = 101, ReviewedById = ids.Supervisor });
            db.AlertReviews.Add(new() { AlertType = AlertType.AttendanceGps, RecordId = 101, ReviewedById = ids.Supervisor });
            foreach (var type in new[] { "shift_attendances", "scan_records", "inspections", "blocked_scans", "cover_assignments" })
            {
                db.AuditLog.Add(new() { EntityType = type, EntityId = 100, Action = "TEST", Summary = "late synthetic" });
                db.AuditLog.Add(new() { EntityType = type, EntityId = null, Action = "TEST", Summary = "preserve null identity" });
            }
            foreach (var type in new[] { "users", "signs" }) db.AuditLog.Add(new() { EntityType = type, EntityId = 100, Action = "TEST", Summary = "setup" });
            db.AuditLog.Add(new() { EntityType = "scan_records", EntityId = 101, Action = "TEST", Summary = "other-table same ID" });
            db.AuditLog.Add(new() { EntityType = "shift_attendances", EntityId = 101, Action = "TEST", Summary = "retained source" });
            await db.SaveChangesAsync();
            await new RetentionRunner(db, factory.Clock, new() { BatchSize = 1 }).RunAsync(default);
            Assert.Equal(new[] { (AlertType.AttendanceGps, 101L), (AlertType.SubmissionGps, 100L) },
                (await db.AlertReviews.OrderBy(r => r.RecordId).ToListAsync()).OrderBy(r => r.AlertType).Select(r => (r.AlertType, r.RecordId)).ToArray());
            var audits = await db.AuditLog.ToListAsync();
            Assert.Equal(9, audits.Count); Assert.Equal(5, audits.Count(a => a.EntityId == null));
            Assert.Contains(audits, a => a.EntityType == "scan_records" && a.EntityId == 100);
            Assert.Contains(audits, a => a.EntityType == "shift_attendances" && a.EntityId == 101);
            Assert.Contains(audits, a => a.EntityType == "users"); Assert.Contains(audits, a => a.EntityType == "signs");
            Assert.Equal(0, (await new RetentionRunner(db, factory.Clock, new()).RunAsync(default)).SummariesCreated);
            Assert.Equal(2, await db.AlertReviews.CountAsync()); Assert.Equal(9, await db.AuditLog.CountAsync());
        });
    }
}
