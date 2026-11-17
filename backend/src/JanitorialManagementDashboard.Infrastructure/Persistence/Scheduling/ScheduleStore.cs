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
    /// A null <paramref name="effectiveUntil"/> is an open-ended claim.
    /// </summary>
    public Task<bool> RegularScopeOverlapsAsync(
        int buildingId,
        Shift shift,
        DateOnly effectiveFrom,
        DateOnly? effectiveUntil,
        int? excludingPeriodId = null,
        CancellationToken ct = default)
    {
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
    /// Rejects a regular Supervisor period that overlaps another one. Call inside the write transaction, after locking
    /// the building/shift anchor, so two people cannot both claim the same range.
    /// </summary>
    public async Task EnsureRegularScopeAvailableAsync(
        int buildingId,
        Shift shift,
        DateOnly effectiveFrom,
        DateOnly? effectiveUntil,
        int? excludingPeriodId = null,
        CancellationToken ct = default)
    {
        if (await RegularScopeOverlapsAsync(buildingId, shift, effectiveFrom, effectiveUntil, excludingPeriodId, ct))
        {
            throw new ScheduleConflictException("scope-overlap");
        }
    }
}

/// <summary>A scheduling operation refused because it would overlap another person's effective responsibility.</summary>
public sealed class ScheduleConflictException(string code)
    : Exception($"The schedule change conflicts with existing responsibility ({code}).")
{
    public string Code { get; } = code;
}
