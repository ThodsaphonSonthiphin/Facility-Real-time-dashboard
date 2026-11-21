using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Retention;

public sealed partial class RetentionRunner
{
    private async Task<(int Summaries, int Deleted)> ArchiveAsync(DateTime now, DateTime annual, CancellationToken ct)
    {
        // Complete Thai calendar months only: November 2026 archives October 2025 and earlier.
        var before = settings.WorkFromShiftDate(now);
        var points = await db.ServicePoints.AsNoTracking().Select(p => new
        {
            p.Id, p.CreatedAt, Counted = p.IsActive && p.Area!.IsActive,
            AreaCreatedAt = p.Area!.CreatedAt, p.Area.ShiftPattern,
        }).ToListAsync(ct);
        var summaries = 0;
        var deleted = 0;
        foreach (var point in points)
        {
            ct.ThrowIfCancellationRequested();
            var earliestScan = await db.ScanRecords.Where(s => s.ServicePointId == point.Id && s.ShiftDate < before)
                .Select(s => (DateOnly?)s.ShiftDate).MinAsync(ct);
            var created = DateOnly.FromDateTime(ThaiTime.FromUtc(point.CreatedAt));
            var first = earliestScan is { } scanDate && scanDate < created ? scanDate : created;
            var windows = await db.PointRoundWindows.AsNoTracking().Where(w => w.ServicePointId == point.Id).ToListAsync(ct);
            for (var month = new DateOnly(first.Year, first.Month, 1); month < before; month = month.AddMonths(1))
            {
                ct.ThrowIfCancellationRequested();
                await using var transaction = await BeginRetentionTransactionAsync(ct);
                // Inserts into either source table check their Service Point foreign key and lock this row.
                // Take the lock before checking the marker and reading facts, including for an empty month.
                if (!OverviewMutation.IsSqlite(db))
                {
                    var locked = await db.Database.SqlQuery<int>(
                        $"SELECT id AS Value FROM service_points WHERE id = {point.Id} FOR UPDATE").ToListAsync(ct);
                    if (locked.Count == 0) break;
                }
                if (await db.PointMonthlySummaries.AnyAsync(s => s.ServicePointId == point.Id && s.Month == month, ct))
                    continue;
                var end = month.AddMonths(1);
                var sources = db.ScanRecords.Where(s => s.ServicePointId == point.Id && s.ShiftDate >= month && s.ShiftDate < end);
                var inspections = db.InspectionRecords.Where(i => sources.Select(s => s.Id).Contains(i.ScanRecordId));
                // Imported/corrected old shift dates may still have young records. Never partially archive their month.
                if (await sources.AnyAsync(s => s.SubmittedAt > annual, ct) || await inspections.AnyAsync(i => i.InspectedAt > annual, ct))
                    continue;
                var submissions = await sources.AsNoTracking().Select(s => new RetentionSubmission(s.Id, s.ShiftDate, s.Shift,
                    s.RoundStart, s.RoundEnd, s.SubmittedAt, s.Placement, s.Status)).ToListAsync(ct);
                if (!point.Counted && submissions.Count == 0) continue;
                var verdicts = await inspections.AsNoTracking()
                    .Select(i => new { i.Id, Fact = new RetentionInspection(i.ScanRecordId, i.Result, i.InspectedAt) }).ToListAsync(ct);
                if (submissions.Any(s => s.SubmittedAt > annual) || verdicts.Any(i => i.Fact.InspectedAt > annual))
                    continue;
                var expected = new List<RetentionRound>();
                if (point.Counted)
                {
                    for (var date = month; date < end; date = date.AddDays(1))
                    foreach (var window in windows)
                    {
                        if (window.Shift == Shift.Night && point.ShiftPattern == ShiftPattern.DayOnly) continue;
                        var start = ShiftCalendar.ToUtc(new(date, window.Shift), window.StartTime);
                        if (point.CreatedAt <= start && point.AreaCreatedAt <= start && window.CreatedAt <= start)
                            expected.Add(new(date, window.Shift, window.StartTime, window.EndTime));
                    }
                }
                var summary = MonthlySummaryRules.Build(point.Id, month, submissions, verdicts.Select(i => i.Fact).ToArray(), expected);
                db.PointMonthlySummaries.Add(summary);
                try
                {
                    await db.SaveChangesAsync(ct);
                    var removed = await DeleteArchivedFactsAsync(inspections.Where(i => i.InspectedAt <= annual),
                        verdicts.Select(i => i.Id), AlertType.InspectionGps, "inspections", ct);
                    removed += await DeleteArchivedFactsAsync(sources.Where(s => s.SubmittedAt <= annual),
                        submissions.Select(s => s.Id), AlertType.SubmissionGps, "scan_records", ct);
                    // A writer outside the normal lock protocol must not leave a summary over a partial month.
                    if (await sources.AnyAsync(ct))
                        throw new DbUpdateConcurrencyException("Retention found new sources in the archived month.");

                    await transaction.CommitAsync(ct);
                    summaries++;
                    deleted += removed;
                }
                finally
                {
                    // SaveChanges may have accepted a marker whose transaction did not commit.
                    db.Entry(summary).State = EntityState.Detached;
                }
            }
        }
        return (summaries, deleted);
    }

    private async Task<int> DeleteArchivedFactsAsync<T>(IQueryable<T> eligible, IEnumerable<long> capturedIds,
        AlertType type, string entityType, CancellationToken ct) where T : class
    {
        var deleted = 0;
        foreach (var batch in capturedIds.Chunk(settings.BatchSize))
        {
            var rows = eligible.Where(row => batch.Contains(EF.Property<long>(row, "Id")));
            await DeleteDependentsAsync(rows.Select(row => EF.Property<long>(row, "Id")), type, entityType, ct);
            deleted += CheckDeletedCount(await rows.ExecuteDeleteAsync(ct), batch.Length);
        }

        return deleted;
    }

    private async Task DeleteDependentsAsync(IQueryable<long> ids, AlertType type, string entityType, CancellationToken ct)
    {
        await db.AlertReviews.Where(r => r.AlertType == type && ids.Contains(r.RecordId)).ExecuteDeleteAsync(ct);
        await db.AuditLog.Where(a => a.EntityType == entityType && a.EntityId != null && ids.Contains(a.EntityId.Value)).ExecuteDeleteAsync(ct);
    }
}
