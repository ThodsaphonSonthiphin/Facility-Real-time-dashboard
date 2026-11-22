using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence;

/// <summary>Assigns an Area's day and night Cleaners and releases anyone it replaces.</summary>
public static class AreaCleaners
{
    public sealed record ApplyOutcome(
        IReadOnlyList<int> AffectedCleaners,
        RegularBaselineOverlap.OverlapConflict? Conflict = null);

    /// <summary>
    /// Puts the chosen Cleaners on the Area, takes every other active Cleaner off it (area_id NULL), and synchronises
    /// each affected Cleaner's dated profile and regular baseline inside the caller's transaction.
    /// Locks baseline heads and scope anchors in global sorted order (matching <c>ScheduleCommandService</c>) before
    /// checking baseline overlaps and writing, so concurrent Area edits and schedule commands never deadlock or create
    /// duplicate <c>(AreaId, Shift)</c> ownership.
    /// </summary>
    public static async Task<ApplyOutcome> ApplyAsync(
        AppDbContext db,
        ScheduleStore store,
        int areaId,
        int buildingId,
        User? dayCleaner,
        User? nightCleaner,
        int? actorId,
        DateOnly businessDate,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var chosenIds = new[] { dayCleaner?.Id, nightCleaner?.Id }.OfType<int>().ToHashSet();

        // Find current holders from BOTH legacy users.AreaId AND active dated baselines covering businessDate.
        var baselineHolderIds = await db.ScheduleBaselines.AsNoTracking()
            .Where(b => b.Kind == BaselineKind.Regular
                && b.EffectiveFrom <= businessDate
                && (b.EffectiveUntil == null || businessDate < b.EffectiveUntil.Value)
                && b.Days.Any(d => !d.IsDayOff && d.AreaId == areaId))
            .Select(b => b.UserId)
            .Distinct()
            .ToListAsync(ct);

        var current = await db.Users
            .Include(u => u.Area)
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive
                && (u.AreaId == areaId || baselineHolderIds.Contains(u.Id)))
            .ToListAsync(ct);

        var leavingCleaners = current.Where(u => !chosenIds.Contains(u.Id)).ToList();
        var assignedEntries = new List<(User Cleaner, Shift Shift)>();
        if (dayCleaner is not null) assignedEntries.Add((dayCleaner, Shift.Day));
        if (nightCleaner is not null) assignedEntries.Add((nightCleaner, Shift.Night));

        // Acquire all baseline-head locks in ascending UserId order, then scope anchors in sorted (BuildingId, Shift)
        // order — the exact global order ScheduleCommandService uses — to guarantee deadlock freedom and serialize
        // against concurrent set-regular-baseline commands.
        var allPersonIds = leavingCleaners.Select(u => u.Id)
            .Concat(assignedEntries.Select(e => e.Cleaner.Id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        foreach (var personId in allPersonIds)
        {
            await store.LockBaselineHeadAsync(personId, nowUtc, ct, advanceRevision: false);
        }

        var scopesToLock = assignedEntries
            .Select(e => (BuildingId: buildingId, e.Shift))
            .Distinct()
            .OrderBy(s => s.BuildingId)
            .ThenBy(s => s.Shift)
            .ToList();
        foreach (var scope in scopesToLock)
        {
            await store.LockRegularScopeAsync(scope.BuildingId, scope.Shift, nowUtc, ct);
        }

        // Under locks, identify genuine outgoing holders of this Area (those holding it via an active baseline or with
        // no active baseline yet). A Cleaner picked up solely due to a stale users.AreaId whose active baseline already
        // moved them elsewhere is NOT exempted from future-overlap checks and does not have their current baseline wiped.
        var leavingIds = leavingCleaners.Select(u => u.Id).ToHashSet();
        var baselineHolderSet = baselineHolderIds.ToHashSet();
        var activeRegularBaselines = leavingIds.Count == 0
            ? new HashSet<int>()
            : await db.ScheduleBaselines.AsNoTracking()
                .Where(b => leavingIds.Contains(b.UserId)
                    && b.Kind == BaselineKind.Regular
                    && b.EffectiveFrom <= businessDate
                    && (b.EffectiveUntil == null || businessDate < b.EffectiveUntil.Value))
                .Select(b => b.UserId)
                .Distinct()
                .ToHashSetAsync(ct);
        var replacedIds = leavingCleaners
            .Where(u => baselineHolderSet.Contains(u.Id) || !activeRegularBaselines.Contains(u.Id))
            .Select(u => u.Id)
            .ToHashSet();

        foreach (var (cleaner, shift) in assignedEntries)
        {
            var conflict = await RegularBaselineOverlap.FindSlotConflictAsync(
                db, cleaner.Id, areaId, shift, businessDate, ct: ct);
            if (conflict is not null && !replacedIds.Contains(conflict.HolderUserId))
            {
                return new ApplyOutcome([], conflict);
            }
        }

        var leavingAreaBuildings = leavingCleaners.ToDictionary(u => u.Id, u => u.Area?.BuildingId ?? buildingId);
        foreach (var leaving in leavingCleaners)
        {
            leaving.AreaId = null;
        }

        await db.SaveChangesAsync(ct);

        foreach (var (cleaner, shift) in assignedEntries)
        {
            cleaner.Shift = shift;
            cleaner.AreaId = areaId;
        }

        await db.SaveChangesAsync(ct);

        var syncTasks = new List<(int UserId, User Cleaner, int HomeBuildingId, bool PreserveExistingShift, int? ClearFutureAreaId)>();
        foreach (var leaving in leavingCleaners)
        {
            if (!replacedIds.Contains(leaving.Id))
            {
                continue;
            }

            var existingPeriods = await db.CleanerProfilePeriods.AsNoTracking()
                .Where(p => p.UserId == leaving.Id)
                .OrderBy(p => p.EffectiveFrom)
                .ThenBy(p => p.Id)
                .ToListAsync(ct);
            var existingHome = existingPeriods
                .LastOrDefault(p => ScheduleResolver.Applies(p.EffectiveFrom, p.EffectiveUntil, businessDate))
                ?.HomeBuildingId;
            syncTasks.Add((leaving.Id, leaving, existingHome ?? leavingAreaBuildings[leaving.Id], true, areaId));
        }

        foreach (var (cleaner, _) in assignedEntries)
        {
            syncTasks.Add((cleaner.Id, cleaner, buildingId, false, null));
        }

        foreach (var item in syncTasks.OrderBy(x => x.UserId))
        {
            await ScheduleSync.SyncCleanerAsync(
                db, store, item.Cleaner, item.HomeBuildingId, CleanerKind.Regular,
                actorId, businessDate, nowUtc, ct,
                clearFutureAreaId: item.ClearFutureAreaId,
                preserveExistingShift: item.PreserveExistingShift);
        }

        await db.SaveChangesAsync(ct);

        return new ApplyOutcome(allPersonIds);
    }
}
