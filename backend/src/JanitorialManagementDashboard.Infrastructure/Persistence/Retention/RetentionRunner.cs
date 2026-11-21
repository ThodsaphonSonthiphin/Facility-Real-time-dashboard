using System.Data;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Retention;

public sealed partial class RetentionRunner(AppDbContext db, TimeProvider clock, RetentionSettings settings,
    ILogger<RetentionRunner>? logger = null) : IRetentionRunner
{
    public async Task<RetentionResult> RunAsync(CancellationToken cancellationToken)
    {
        var stage = "ValidateSettings";
        try
        {
            settings.Validate();
            if (!settings.Enabled) return new();
            var now = clock.GetUtcNow().UtcDateTime;
            var cutoff = now.AddDays(-settings.GpsDays);

            stage = "ClearAttendanceGps";
            var cleared = await ClearGpsAsync<ShiftAttendance, long>(db.ShiftAttendances.Where(x => x.OccurredAt <= cutoff), cancellationToken);
            stage = "ClearScanGps";
            cleared += await ClearGpsAsync<ScanRecord, long>(db.ScanRecords.Where(x => x.SubmittedAt <= cutoff), cancellationToken);
            stage = "ClearInspectionGps";
            cleared += await ClearGpsAsync<InspectionRecord, long>(db.InspectionRecords.Where(x => x.InspectedAt <= cutoff), cancellationToken);
            stage = "ClearBlockedScanGps";
            cleared += await ClearGpsAsync<BlockedScan, long>(db.BlockedScans.Where(x => x.ScannedAt <= cutoff), cancellationToken);
            // Transfer arrivals share the GPS window while keeping their time, destination and source.
            stage = "ClearTransferArrivalGps";
            cleared += await ClearGpsAsync<AreaTransferConfirmation, int>(
                db.AreaTransferConfirmations.Where(x => x.OccurredAt <= cutoff), cancellationToken);

            stage = "ArchiveMonthlyWork";
            var annual = settings.RecordCutoffUtc(now);
            var (summaries, archived) = await ArchiveAsync(now, annual, cancellationToken);
            stage = "DeleteAttendance";
            var deleted = archived + await DeleteAttendanceAsync(annual, cancellationToken);
            stage = "DeleteBlockedScans";
            deleted += await DeleteBlockedAsync(annual, cancellationToken);
            stage = "DeleteCovers";
            deleted += await DeleteCoversAsync(annual, cancellationToken);
            stage = "DeleteRefreshTokens";
            deleted += await DeleteTokensAsync(now.AddDays(-settings.RefreshTokenGraceDays), cancellationToken);
            // Scheduling keeps active and future configuration and the anchors retained dates still need.
            stage = "PruneScheduling";
            deleted += await new SchedulingRetention(db).PruneAsync(now, settings, cancellationToken);

            // Dependent history is excluded from RecordsDeleted, as in source-associated cleanup.
            stage = "SweepOrphanHistory";
            await SweepOrphanHistoryAsync(cancellationToken);
            stage = "PruneOverviewTargets";
            await OverviewTargetRetention.PruneAsync(db, now, settings, cancellationToken);
            return new(cleared, summaries, deleted);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var cause = error;
            while (cause is not MySqlException && cause.InnerException is { } inner)
                cause = inner;
            // The exception and its message may include database or record details.
            logger?.LogWarning(new EventId(1, "RetentionStageFailed"),
                "Retention stage {Stage} failed ({FailureType}, MySQL error {MySqlErrorNumber})",
                stage, cause.GetType().Name, (cause as MySqlException)?.Number);
            throw;
        }
    }

    private Task<IDbContextTransaction> BeginRetentionTransactionAsync(CancellationToken ct) =>
        db.Database.BeginTransactionAsync(
            OverviewMutation.IsSqlite(db) ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct);

    private static int CheckDeletedCount(int deleted, int expected)
    {
        if (deleted != expected)
            throw new DbUpdateConcurrencyException("Retention source rows changed before deletion.");

        return deleted;
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
