using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.Retention;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence.Retention;

public sealed partial class RetentionRunner
{
    private async Task<(int Summaries, int Deleted)> ArchiveAsync(DateTime now, DateTime annual, CancellationToken ct)
    {
        // Complete Thai calendar months only: November 2026 archives October 2025 and earlier.
        var thai = ThaiTime.FromUtc(now);
        var before = new DateOnly(thai.Year, thai.Month, 1).AddYears(-settings.RecordYears);
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
                await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
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
                var verdicts = await inspections.AsNoTracking().Select(i => new RetentionInspection(i.ScanRecordId, i.Result, i.InspectedAt)).ToListAsync(ct);
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
                var summary = MonthlySummaryRules.Build(point.Id, month, submissions, verdicts, expected);
                db.PointMonthlySummaries.Add(summary);
                try
                {
                    await db.SaveChangesAsync(ct);
                    await DeleteDependentsAsync(sources.Select(s => s.Id), AlertType.SubmissionGps, "scan_records", ct);
                    await DeleteDependentsAsync(inspections.Select(i => i.Id), AlertType.InspectionGps, "inspections", ct);
                    var removed = await inspections.ExecuteDeleteAsync(ct) + await sources.ExecuteDeleteAsync(ct);
                    await transaction.CommitAsync(ct);
                    summaries++;
                    deleted += removed;
                    db.Entry(summary).State = EntityState.Detached;
                }
                catch
                {
                    // SaveChanges may have accepted the marker before a later statement failed; never keep a rolled-back marker tracked.
                    db.Entry(summary).State = EntityState.Detached;
                    throw;
                }
            }
        }
        return (summaries, deleted);
    }

    private async Task DeleteDependentsAsync(IQueryable<long> ids, AlertType type, string entityType, CancellationToken ct)
    {
        await db.AlertReviews.Where(r => r.AlertType == type && ids.Contains(r.RecordId)).ExecuteDeleteAsync(ct);
        await db.AuditLog.Where(a => a.EntityType == entityType && a.EntityId != null && ids.Contains(a.EntityId.Value)).ExecuteDeleteAsync(ct);
    }
}
