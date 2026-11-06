using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Domain.Entities;
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
        var cleared = await ClearGpsAsync(db.ShiftAttendances.Where(x => x.OccurredAt <= cutoff), cancellationToken)
            + await ClearGpsAsync(db.ScanRecords.Where(x => x.SubmittedAt <= cutoff), cancellationToken)
            + await ClearGpsAsync(db.InspectionRecords.Where(x => x.InspectedAt <= cutoff), cancellationToken)
            + await ClearGpsAsync(db.BlockedScans.Where(x => x.ScannedAt <= cutoff), cancellationToken);
        var annual = now.AddYears(-settings.RecordYears);
        var (summaries, archived) = await ArchiveAsync(now, annual, cancellationToken);
        var deleted = archived
            + await DeleteAttendanceAsync(annual, cancellationToken)
            + await DeleteBlockedAsync(annual, cancellationToken)
            + await DeleteCoversAsync(annual, cancellationToken)
            + await DeleteTokensAsync(now.AddDays(-settings.RefreshTokenGraceDays), cancellationToken);
        // Dependent history is excluded from RecordsDeleted, as in source-associated cleanup.
        await SweepOrphanHistoryAsync(cancellationToken);
        return new(cleared, summaries, deleted);
    }

    private async Task<int> ClearGpsAsync<T>(IQueryable<T> query, CancellationToken ct) where T : class, IGpsStamped
    {
        var total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var ids = await query.Where(x => x.Latitude != null || x.Longitude != null || x.AccuracyM != null)
                .OrderBy(x => EF.Property<long>(x, "Id")).Select(x => EF.Property<long>(x, "Id"))
                .Take(settings.BatchSize).ToListAsync(ct);
            if (ids.Count == 0) return total;
            total += await query.Where(x => ids.Contains(EF.Property<long>(x, "Id")))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Latitude, (decimal?)null)
                    .SetProperty(x => x.Longitude, (decimal?)null).SetProperty(x => x.AccuracyM, (short?)null), ct);
        }
    }
}
