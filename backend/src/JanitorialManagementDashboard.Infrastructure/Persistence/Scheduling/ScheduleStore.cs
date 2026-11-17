using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;

/// <summary>
/// The scheduling reads and conflict checks that are stored data, not pure rules. Interval overlap cannot be expressed
/// as a portable unique index on MySQL and SQLite, so regular Supervisor ownership is checked here while the caller
/// holds the building/shift <see cref="Domain.Entities.Scheduling.ScheduleScopeAnchor"/>.
/// </summary>
public sealed class ScheduleStore(AppDbContext db)
{
    /// <summary>
    /// True when another regular Supervisor period for the building and shift overlaps the proposed end-exclusive range.
    /// A null <paramref name="effectiveUntil"/> is an open-ended claim. An inverted range throws instead of answering
    /// "free", because no stored row can ever overlap a range that describes no dates.
    /// </summary>
    public Task<bool> RegularScopeOverlapsAsync(
        int buildingId,
        Shift shift,
        DateOnly effectiveFrom,
        DateOnly? effectiveUntil,
        int? excludingPeriodId = null,
        CancellationToken ct = default)
    {
        EnsureRangeIsForward(effectiveFrom, effectiveUntil);
        var proposedEnd = effectiveUntil ?? DateOnly.MaxValue;
        return db.SupervisorScopePeriods.AnyAsync(
            period => period.BuildingId == buildingId
                && period.Shift == shift
                && (excludingPeriodId == null || period.Id != excludingPeriodId)
                && period.EffectiveFrom < proposedEnd
                && effectiveFrom < (period.EffectiveUntil ?? DateOnly.MaxValue),
            ct);
    }

    /// <summary>
    /// Rejects a regular Supervisor period that overlaps another one, or whose own range is inverted. Call inside the
    /// write transaction, after locking the building/shift anchor, so two people cannot both claim the same range.
    /// </summary>
    public async Task EnsureRegularScopeAvailableAsync(
        int buildingId,
        Shift shift,
        DateOnly effectiveFrom,
        DateOnly? effectiveUntil,
        int? excludingPeriodId = null,
        CancellationToken ct = default)
    {
        EnsureRangeIsForward(effectiveFrom, effectiveUntil);
        if (await RegularScopeOverlapsAsync(buildingId, shift, effectiveFrom, effectiveUntil, excludingPeriodId, ct))
        {
            throw new ScheduleConflictException("scope-overlap");
        }
    }

    /// <summary>
    /// Takes the building/shift <see cref="Domain.Entities.Scheduling.ScheduleScopeAnchor"/> for the rest of the caller's
    /// transaction: it is created when missing and its revision is bumped, which writes the row and so holds the lock a
    /// second writer of the same building/shift must wait for (spec §11: lock the stable anchor, not the target row that
    /// may not exist yet). Every regular membership check for that building/shift then runs while it is held.
    /// </summary>
    public async Task<ScheduleScopeAnchor> LockRegularScopeAsync(
        int buildingId, Shift shift, DateTime nowUtc, CancellationToken ct = default)
    {
        var anchor = await db.ScheduleScopeAnchors.FirstOrDefaultAsync(
            a => a.BuildingId == buildingId && a.Shift == shift, ct);
        if (anchor is null)
        {
            var created = new ScheduleScopeAnchor { BuildingId = buildingId, Shift = shift, Revision = 1, UpdatedAt = nowUtc };
            db.ScheduleScopeAnchors.Add(created);
            try
            {
                await db.SaveChangesAsync(ct);
                return created;
            }
            catch (DbUpdateException)
            {
                // Another writer created the anchor first, which is the case this row exists for. Anything else (a
                // duplicate login name or slot on the same save) is not ours to explain: it leaves no anchor row behind.
                db.Entry(created).State = EntityState.Detached;
                var existing = await db.ScheduleScopeAnchors.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.BuildingId == buildingId && a.Shift == shift, ct);
                if (existing is null)
                {
                    throw;
                }

                anchor = await db.ScheduleScopeAnchors.FirstAsync(a => a.Id == existing.Id, ct);
            }
        }

        anchor.Revision += 1;
        anchor.UpdatedAt = nowUtc;
        await db.SaveChangesAsync(ct);
        return anchor;
    }

    /// <summary>
    /// <see cref="EnsureRegularScopeAvailableAsync"/> for a caller that already holds the building/shift anchor. On MySQL
    /// the rows are read with a locking read: under REPEATABLE READ a plain read would answer from the snapshot the
    /// transaction took before the other writer committed, so two people could both see a free slot.
    /// </summary>
    public async Task EnsureRegularScopeAvailableUnderLockAsync(
        int buildingId, Shift shift, DateOnly effectiveFrom, DateOnly? effectiveUntil, CancellationToken ct = default)
    {
        EnsureRangeIsForward(effectiveFrom, effectiveUntil);
        var proposedEnd = effectiveUntil ?? DateOnly.MaxValue;
        var rows = IsMySql
            ? await db.SupervisorScopePeriods
                .FromSqlRaw("SELECT * FROM supervisor_scope_periods WHERE building_id = {0} FOR UPDATE", buildingId)
                .AsNoTracking().ToListAsync(ct)
            : await db.SupervisorScopePeriods.AsNoTracking().Where(p => p.BuildingId == buildingId).ToListAsync(ct);
        if (rows.Any(period => period.Shift == shift
            && period.EffectiveFrom < proposedEnd
            && effectiveFrom < (period.EffectiveUntil ?? DateOnly.MaxValue)))
        {
            throw new ScheduleConflictException("scope-overlap");
        }
    }

    private bool IsMySql =>
        db.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The same rule the ck_*_effective_range CHECK constraints apply, so a caller hears about it first.</summary>
    private static void EnsureRangeIsForward(DateOnly effectiveFrom, DateOnly? effectiveUntil)
    {
        if (effectiveUntil is { } until && until <= effectiveFrom)
        {
            throw new ScheduleConflictException("invalid-range");
        }
    }
}

/// <summary>A scheduling operation refused because it would overlap another person's effective responsibility.</summary>
public sealed class ScheduleConflictException(string code)
    : Exception($"The schedule change conflicts with existing responsibility ({code}).")
{
    public string Code { get; } = code;
}
