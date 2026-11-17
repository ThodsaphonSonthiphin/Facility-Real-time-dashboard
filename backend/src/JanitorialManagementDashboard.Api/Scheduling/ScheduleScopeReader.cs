using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §3 and §14: the one reader of the dated Supervisor scope. It answers only for an active Supervisor account, so a
/// missing, deactivated or non-Supervisor actor holds no scope at all, and it never trusts a client-supplied id: the
/// actor comes from the access token.
/// </summary>
public sealed class ScheduleScopeReader(AppDbContext db, TimeProvider clock)
{
    /// <summary>
    /// Every dated period of the actor, once per request. Consumers pass the list to
    /// <see cref="ScheduleAuthority.CanManage"/> rather than reading <c>User.BuildingId</c>/<c>User.Shift</c>.
    /// </summary>
    public async Task<IReadOnlyList<ScopePeriod>> ReadAsync(int actorId, CancellationToken ct = default)
    {
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId, ct);
        if (actor is not { IsActive: true, Role: UserRole.Supervisor })
        {
            return [];
        }

        var periods = await db.SupervisorScopePeriods.AsNoTracking()
            .Where(p => p.SupervisorId == actorId)
            .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.BuildingId)
            .Select(p => new ScopePeriod(p.SupervisorId, p.BuildingId, p.Shift, p.EffectiveFrom, p.EffectiveUntil))
            .ToListAsync(ct);

        // Compatibility projection (spec §14): an account created before the dated rows existed still works from its
        // current BuildingId and Shift, effective from today and never retroactively. As soon as one dated row exists it
        // is the only source, and this branch stops applying.
        return periods.Count > 0
            ? periods
            : actor.BuildingId is int buildingId && actor.Shift is Shift shift
                ? [new ScopePeriod(actor.Id, buildingId, shift, ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime), null)]
                : [];
    }

    /// <summary>
    /// The active Supervisors responsible for a building on a date, both shifts, for scoped live updates (spec §12: a
    /// deactivated account gets no event). Static because the notification fan-out already holds the database and the
    /// business date and needs no clock of its own.
    /// </summary>
    public static async Task<IReadOnlyList<int>> SupervisorsOfAsync(
        AppDbContext db, int buildingId, DateOnly date, CancellationToken ct = default)
    {
        var responsible = await db.SupervisorScopePeriods.AsNoTracking()
            .Where(p => p.BuildingId == buildingId && p.EffectiveFrom <= date
                && (p.EffectiveUntil == null || date < p.EffectiveUntil.Value))
            .Select(p => p.SupervisorId)
            .Distinct()
            .ToListAsync(ct);

        // The same compatibility projection as ReadAsync: a Supervisor with no dated row at all is still the projection's
        var dated = await db.SupervisorScopePeriods.AsNoTracking().Select(p => p.SupervisorId).Distinct().ToListAsync(ct);
        var projected = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == buildingId && !dated.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(ct);

        return await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Supervisor && u.IsActive
                && (responsible.Contains(u.Id) || projected.Contains(u.Id)))
            .Select(u => u.Id)
            .ToListAsync(ct);
    }
}
