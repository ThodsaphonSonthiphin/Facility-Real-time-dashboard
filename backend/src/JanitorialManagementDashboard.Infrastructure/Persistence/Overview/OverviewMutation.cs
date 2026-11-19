using System.Data;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Overview;

public sealed class OverviewMutation(AppDbContext db, OverviewTargetRecorder recorder)
{
    private static bool IsSqlite(AppDbContext db) =>
        db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

    public static async Task<OverviewTargetState> LockStateAsync(AppDbContext db, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Locking overview state requires an open transaction.");

        // A reused context may still track the head read before another transaction committed.
        if (db.OverviewTargetStates.Local.SingleOrDefault() is { } previous)
            db.Entry(previous).State = EntityState.Detached;

        if (IsSqlite(db))
            return await db.OverviewTargetStates.SingleAsync(s => s.Id == 1, ct);

        var rows = await db.OverviewTargetStates.FromSqlRaw(
            "SELECT * FROM overview_target_state WHERE id = 1 FOR UPDATE").ToListAsync(ct);
        return rows.Single();
    }

    public Task<OverviewMutationLease> BeginAsync(CancellationToken ct) =>
        BeginAsync(ct, capturePreMutation: true);

    internal async Task<OverviewMutationLease> BeginAsync(CancellationToken ct, bool capturePreMutation)
    {
        var transaction = await db.Database.BeginTransactionAsync(
            IsSqlite(db) ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct);
        var lease = new OverviewMutationLease(db, recorder, transaction);
        try
        {
            var state = await LockStateAsync(db, ct);
            if (capturePreMutation && state.LatestRevisionId == null)
                await lease.CaptureAsync(ct, null);
            return lease;
        }
        catch
        {
            await lease.DisposeAsync();
            throw;
        }
    }
}

public sealed class OverviewMutationLease : IAsyncDisposable
{
    private readonly AppDbContext db;
    private readonly OverviewTargetRecorder recorder;
    private readonly IDbContextTransaction transaction;
    private bool committed;
    private bool disposed;

    internal OverviewMutationLease(AppDbContext db, OverviewTargetRecorder recorder, IDbContextTransaction transaction)
    {
        this.db = db;
        this.recorder = recorder;
        this.transaction = transaction;
    }

    public Task<long> CommitAsync(CancellationToken ct, DateTime? coverageGapFromUtc = null) =>
        CommitAsync(ct, coverageGapFromUtc, observedImage: null);

    internal async Task<long> CommitAsync(CancellationToken ct, DateTime? coverageGapFromUtc, TargetImage? observedImage)
    {
        if (committed || disposed)
            throw new InvalidOperationException("The overview mutation has already ended.");
        var id = await CaptureAsync(ct, coverageGapFromUtc, observedImage);
        await transaction.CommitAsync(ct);
        committed = true;
        return id;
    }

    internal async Task<long> CaptureAsync(CancellationToken ct, DateTime? coverageGapFromUtc, TargetImage? observedImage = null)
    {
        try
        {
            return await recorder.CaptureAsync(ct, coverageGapFromUtc, observedImage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new OverviewCaptureException(ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        disposed = true;
        try
        {
            if (!committed)
                await transaction.RollbackAsync();
        }
        finally
        {
            if (!committed)
                db.ChangeTracker.Clear();
            await transaction.DisposeAsync();
        }
    }
}

public sealed class OverviewCaptureException(Exception cause) : Exception("Could not capture overview target history.", cause);
