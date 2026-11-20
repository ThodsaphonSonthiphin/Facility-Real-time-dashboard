using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;

/// <summary>
/// Spec §3 and §14: the one load of a Supervisor's dated authority, shared by every reader so the rule cannot be
/// re-implemented per caller. It answers only for an active Supervisor account (a missing, deactivated or
/// non-Supervisor actor holds no scope at all) and it never reads the actor from a client-supplied id. The periods it
/// returns are plain ranges: whether a period covers a date is answered only by <see cref="ScheduleAuthority.Covers"/>,
/// and whether it may be acted on by <see cref="ScheduleAuthority.CanManage"/> or
/// <see cref="ScheduleAuthority.CanCorrectHistory"/>.
/// </summary>
public static class SupervisorScopes
{
    /// <summary>The dated periods of <paramref name="actorId"/>, loaded from the database, or none when it holds no scope.</summary>
    public static async Task<IReadOnlyList<ScopePeriod>> ReadAsync(
        AppDbContext db, int actorId, TimeProvider clock, CancellationToken ct = default)
    {
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId, ct);
        return await ReadAsync(db, actor, clock, ct);
    }

    /// <summary>
    /// The dated periods of an actor the caller has already loaded — the same rule, without the second read of the row.
    /// </summary>
    public static async Task<IReadOnlyList<ScopePeriod>> ReadAsync(
        AppDbContext db, User? actor, TimeProvider clock, CancellationToken ct = default)
    {
        if (actor is not { IsActive: true, Role: UserRole.Supervisor })
        {
            return [];
        }

        var periods = await db.SupervisorScopePeriods.AsNoTracking()
            .Where(p => p.SupervisorId == actor.Id)
            .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.BuildingId)
            .Select(p => new ScopePeriod(p.SupervisorId, p.BuildingId, p.Shift, p.EffectiveFrom, p.EffectiveUntil))
            .ToListAsync(ct);

        return ApplyCompatibility(actor, periods, ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime));
    }

    /// <summary>
    /// Batch-loads dated periods for many Supervisor accounts in one query using the same compatibility rule.
    /// </summary>
    public static async Task<IReadOnlyList<ScopePeriod>> ReadManyAsync(
        AppDbContext db, IReadOnlyCollection<User> actors, DateOnly compatibilityDate, CancellationToken ct = default)
    {
        var supervisors = actors
            .Where(actor => actor is { IsActive: true, Role: UserRole.Supervisor })
            .ToList();
        if (supervisors.Count == 0)
        {
            return [];
        }

        var ids = supervisors.Select(actor => actor.Id).Distinct().ToList();
        var bySupervisor = (await db.SupervisorScopePeriods.AsNoTracking()
                .Where(p => ids.Contains(p.SupervisorId))
                .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.BuildingId)
                .Select(p => new ScopePeriod(p.SupervisorId, p.BuildingId, p.Shift, p.EffectiveFrom, p.EffectiveUntil))
                .ToListAsync(ct))
            .GroupBy(p => p.SupervisorId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ScopePeriod>)g.ToList());

        return supervisors
            .SelectMany(actor => ApplyCompatibility(
                actor,
                bySupervisor.TryGetValue(actor.Id, out var found) ? found : [],
                compatibilityDate))
            .ToList();
    }

    // Compatibility projection (spec §14): an account created before the dated rows existed still works from its
    // current BuildingId and Shift, effective from today and never retroactively. As soon as one dated row exists it is
    // the only source, and this branch stops applying.
    private static IReadOnlyList<ScopePeriod> ApplyCompatibility(
        User actor, IReadOnlyList<ScopePeriod> periods, DateOnly compatibilityDate) =>
        periods.Count > 0
            ? periods
            : actor.BuildingId is int buildingId && actor.Shift is Shift shift
                ? [new ScopePeriod(actor.Id, buildingId, shift, compatibilityDate, null)]
                : [];
}
