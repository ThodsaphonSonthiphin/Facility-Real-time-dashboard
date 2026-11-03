using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence.Retention;

public sealed partial class RetentionRunner
{
    private async Task SweepOrphanHistoryAsync(CancellationToken ct)
    {
        // A request can finish writing a review/audit after its source was archived. These typed sweeps
        // run independently of source discovery and month markers, so the next run still removes it.
        var reviews = db.AlertReviews.Where(r =>
            (r.AlertType == AlertType.AttendanceGps && !db.ShiftAttendances.Any(s => s.Id == r.RecordId))
            || (r.AlertType == AlertType.SubmissionGps && !db.ScanRecords.Any(s => s.Id == r.RecordId))
            || (r.AlertType == AlertType.InspectionGps && !db.InspectionRecords.Any(s => s.Id == r.RecordId))
            || (r.AlertType == AlertType.BlockedScan && !db.BlockedScans.Any(s => s.Id == r.RecordId)));
        await DeleteOrphanBatchesAsync(reviews, ct);
        var audits = db.AuditLog.Where(a => a.EntityId != null && (
            (a.EntityType == "shift_attendances" && !db.ShiftAttendances.Any(s => s.Id == a.EntityId))
            || (a.EntityType == "scan_records" && !db.ScanRecords.Any(s => s.Id == a.EntityId))
            || (a.EntityType == "inspections" && !db.InspectionRecords.Any(s => s.Id == a.EntityId))
            || (a.EntityType == "blocked_scans" && !db.BlockedScans.Any(s => s.Id == a.EntityId))
            || (a.EntityType == "cover_assignments" && !db.CoverAssignments.Any(s => s.Id == a.EntityId))));
        await DeleteOrphanBatchesAsync(audits, ct);
    }

    private async Task DeleteOrphanBatchesAsync<T>(IQueryable<T> orphans, CancellationToken ct) where T : class
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var ids = await orphans.OrderBy(x => EF.Property<long>(x, "Id")).Select(x => EF.Property<long>(x, "Id"))
                .Take(settings.BatchSize).ToListAsync(ct);
            if (ids.Count == 0) return;
            // Reapply the typed NOT EXISTS in DELETE: a source may appear after the ID selection.
            await orphans.Where(x => ids.Contains(EF.Property<long>(x, "Id"))).ExecuteDeleteAsync(ct);
        }
    }
}
