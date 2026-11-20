using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Retention;

public sealed partial class RetentionRunner(AppDbContext db, TimeProvider clock, RetentionSettings settings) : IRetentionRunner
{
    public async Task<RetentionResult> RunAsync(CancellationToken cancellationToken)
    {
        settings.Validate();
        if (!settings.Enabled) return new();
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-settings.GpsDays);
        // The transfer arrival is the fifth GPS-stamped fact. It shares the shorter GPS policy so a retained arrival keeps
        // its time, destination and source while never keeping a location longer than every other reading.
        var cleared = await ClearGpsAsync<ShiftAttendance, long>(db.ShiftAttendances.Where(x => x.OccurredAt <= cutoff), cancellationToken)
            + await ClearGpsAsync<ScanRecord, long>(db.ScanRecords.Where(x => x.SubmittedAt <= cutoff), cancellationToken)
            + await ClearGpsAsync<InspectionRecord, long>(db.InspectionRecords.Where(x => x.InspectedAt <= cutoff), cancellationToken)
            + await ClearGpsAsync<BlockedScan, long>(db.BlockedScans.Where(x => x.ScannedAt <= cutoff), cancellationToken)
            + await ClearGpsAsync<AreaTransferConfirmation, int>(
                db.AreaTransferConfirmations.Where(x => x.OccurredAt <= cutoff), cancellationToken);
        var annual = settings.RecordCutoffUtc(now);
        var (summaries, archived) = await ArchiveAsync(now, annual, cancellationToken);
        var deleted = archived
            + await DeleteAttendanceAsync(annual, cancellationToken)
            + await DeleteBlockedAsync(annual, cancellationToken)
            + await DeleteCoversAsync(annual, cancellationToken)
            + await DeleteTokensAsync(now.AddDays(-settings.RefreshTokenGraceDays), cancellationToken)
            // Spec §12: the scheduling detail rides the same detail window, keeping active/future configuration and the
            // boundary anchors the retained dates still need.
            + await new SchedulingRetention(db).PruneAsync(now, settings, cancellationToken);
        // Dependent history is excluded from RecordsDeleted, as in source-associated cleanup.
        await SweepOrphanHistoryAsync(cancellationToken);
        await OverviewTargetRetention.PruneAsync(db, now, settings, cancellationToken);
        return new(cleared, summaries, deleted);
    }

    private async Task<int> ClearGpsAsync<T, TKey>(IQueryable<T> query, CancellationToken ct)
        where T : class, IGpsStamped
        where TKey : struct
    {
        var total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var ids = await query.Where(x => x.Latitude != null || x.Longitude != null || x.AccuracyM != null)
                .OrderBy(x => EF.Property<TKey>(x, "Id")).Select(x => EF.Property<TKey>(x, "Id"))
                .Take(settings.BatchSize).ToListAsync(ct);
            if (ids.Count == 0) return total;
            total += await query.Where(x => ids.Contains(EF.Property<TKey>(x, "Id")))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Latitude, (decimal?)null)
                    .SetProperty(x => x.Longitude, (decimal?)null).SetProperty(x => x.AccuracyM, (short?)null), ct);
        }
    }
}
