using System.Data;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Retention;

public static class OverviewTargetRetention
{
    public static async Task<int> PruneAsync(AppDbContext db, DateTime nowUtc, RetentionSettings settings, CancellationToken ct)
    {
        var attendance = settings.RecordCutoffUtc(nowUtc);
        var work = ShiftCalendar.StartUtc(new(settings.WorkFromShiftDate(nowUtc), Shift.Day));
        // Leave one overnight date and the largest permitted opening offset before either source boundary.
        var lowerUtc = (attendance < work ? attendance : work).AddDays(-1)
            .AddMinutes(-AttendanceSettings.MaxReceivingOffsetMinutes);
        var deleted = 0;

        while (true)
        {
            // SQLite uses a serializable writer transaction in place of a MySQL row lock.
            var isolation = OverviewMutation.IsSqlite(db) ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted;
            await using var transaction = await db.Database.BeginTransactionAsync(isolation, ct);
            var state = await OverviewMutation.LockStateAsync(db, ct);
            var anchor = await db.OverviewTargetRevisions.AsNoTracking()
                .Where(revision => revision.EffectiveAt <= lowerUtc)
                .OrderByDescending(revision => revision.EffectiveAt).ThenByDescending(revision => revision.Id)
                .Select(revision => new { revision.Id, revision.EffectiveAt })
                .FirstOrDefaultAsync(ct);
            if (anchor == null) return deleted;

            var headId = state.LatestRevisionId;
            var obsolete = await db.OverviewTargetRevisions.AsNoTracking()
                .Where(revision => revision.Id != headId &&
                    (revision.EffectiveAt < anchor.EffectiveAt || revision.EffectiveAt == anchor.EffectiveAt && revision.Id < anchor.Id))
                .OrderBy(revision => revision.EffectiveAt).ThenBy(revision => revision.Id)
                .Select(revision => revision.Id).Take(settings.BatchSize).ToArrayAsync(ct);
            if (obsolete.Length == 0) return deleted;

            // CaptureStartedAtUtc lives in each image. Deleting predecessors leaves retained payloads intact.
            deleted += await db.OverviewTargetRevisions.Where(revision => obsolete.Contains(revision.Id)).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
