using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §3 and §14: the one reader of the dated Supervisor scope. It answers only for an active Supervisor account, so a
/// missing, deactivated or non-Supervisor actor holds no scope at all, and it never trusts a client-supplied id: the
/// actor comes from the access token. The dated-plus-compatibility rule itself lives in
/// <see cref="SupervisorScopes"/>, which the scheduling store reads through too, so no caller has its own copy of it.
/// </summary>
public sealed class ScheduleScopeReader(AppDbContext db, TimeProvider clock)
{
    /// <summary>
    /// Every dated period of the actor, once per request. Consumers pass the list to
    /// <see cref="ScheduleAuthority.CanManage"/> rather than reading <c>User.BuildingId</c>/<c>User.Shift</c>.
    /// </summary>
    public Task<IReadOnlyList<ScopePeriod>> ReadAsync(int actorId, CancellationToken ct = default) =>
        SupervisorScopes.ReadAsync(db, actorId, clock, ct);

    /// <summary>
    /// The active Supervisors responsible for a building on a date, both shifts, for scoped live updates (spec §12: a
    /// deactivated account gets no event). Static because the notification fan-out already holds the database and the
    /// business date and needs no clock of its own.
    /// </summary>
    public static Task<IReadOnlyList<int>> SupervisorsOfAsync(
        AppDbContext db, int buildingId, DateOnly date, CancellationToken ct = default) =>
        SupervisorsOfAsync(db, buildingId, date, shift: null, includeCompatibility: true, ct);

    public static async Task<IReadOnlyList<int>> SupervisorsOfAsync(
        AppDbContext db, int buildingId, DateOnly date, Shift? shift, bool includeCompatibility, CancellationToken ct = default)
    {
        var responsible = await db.SupervisorScopePeriods.AsNoTracking()
            .Where(p => p.BuildingId == buildingId && (shift == null || p.Shift == shift) && p.EffectiveFrom <= date
                && (p.EffectiveUntil == null || date < p.EffectiveUntil.Value))
            .Select(p => p.SupervisorId)
            .Distinct()
            .ToListAsync(ct);

        return await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Supervisor && u.IsActive &&
                (responsible.Contains(u.Id) ||
                 (includeCompatibility && u.BuildingId == buildingId && (shift == null || u.Shift == shift)
                    && !db.SupervisorScopePeriods.Any(period => period.SupervisorId == u.Id))))
            .Select(u => u.Id)
            .ToListAsync(ct);
    }
}
