using JanitorialManagementDashboard.Api.Endpoints;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// The dated membership writes of spec §3: an Admin's account create, activate, building/shift change or deactivate of a
/// Supervisor takes effect on the business clock's Thai date at once. The replaced period is closed end-exclusive on that
/// date and the new one opens on it; a period that would end where it starts is removed instead of being stored empty.
/// Every check runs under the building/shift <c>ScheduleScopeAnchor</c> the store takes, so two Admins cannot both give
/// one building and shift to different people.
/// </summary>
public sealed class SupervisorMembership(AppDbContext db, ScheduleStore store, TimeProvider clock)
{
    /// <summary>
    /// Applies the membership, inside the caller's transaction. Null means it was applied; otherwise the answer to give
    /// (a 409 naming the other holder). <paramref name="open"/> is false for a deactivation, which closes what it held.
    /// </summary>
    public async Task<IResult?> ApplyAsync(User supervisor, bool open, int actorId, CancellationToken ct = default)
    {
        if (supervisor.Role != UserRole.Supervisor)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var businessDate = ScheduleAuthority.BusinessDate(now);
        var held = await db.SupervisorScopePeriods
            .Where(p => p.SupervisorId == supervisor.Id && p.EffectiveUntil == null)
            .ToListAsync(ct);

        var target = open && supervisor.BuildingId is int buildingId && supervisor.Shift is Shift shift
            ? (BuildingId: buildingId, Shift: shift)
            : ((int BuildingId, Shift Shift)?)null;

        foreach (var period in held.Where(p => target is null || p.BuildingId != target.Value.BuildingId || p.Shift != target.Value.Shift).ToList())
        {
            Close(period, businessDate, now);
        }

        if (target is not { } wanted)
        {
            return null;
        }

        if (held.Any(p => p.BuildingId == wanted.BuildingId && p.Shift == wanted.Shift))
        {
            // The period for this building and shift is already open: nothing changed for the members it names
            return null;
        }

        await store.LockRegularScopeAsync(wanted.BuildingId, wanted.Shift, now, ct);
        try
        {
            await store.EnsureRegularScopeAvailableUnderLockAsync(wanted.BuildingId, wanted.Shift, businessDate, null, ct);
        }
        catch (ScheduleConflictException)
        {
            return AccountChecks.Conflict(await TakenMessageAsync(wanted.BuildingId, wanted.Shift, ct));
        }

        db.SupervisorScopePeriods.Add(new SupervisorScopePeriod
        {
            SupervisorId = supervisor.Id,
            BuildingId = wanted.BuildingId,
            Shift = wanted.Shift,
            EffectiveFrom = businessDate,
            Revision = 1,
            CreatedById = actorId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        return null;
    }

    /// <summary>Ends a period on the business date, or removes it when it never covered a day anyway.</summary>
    private void Close(SupervisorScopePeriod period, DateOnly businessDate, DateTime now)
    {
        if (businessDate > period.EffectiveFrom)
        {
            period.EffectiveUntil = businessDate;
            period.Revision += 1;
            period.UpdatedAt = now;
            return;
        }

        // EffectiveUntil must be after EffectiveFrom: a period that starts today has no day left to keep, and the
        // history of the responsibility it replaced is the Admin's audit row.
        db.SupervisorScopePeriods.Remove(period);
    }

    /// <summary>The same words the account forms already use for a taken Supervisor slot, naming who holds it now.</summary>
    private async Task<string> TakenMessageAsync(int buildingId, Shift shift, CancellationToken ct)
    {
        var businessDate = ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime);
        var holderId = await db.SupervisorScopePeriods.AsNoTracking()
            .Where(p => p.BuildingId == buildingId && p.Shift == shift
                && (p.EffectiveUntil == null || p.EffectiveUntil > businessDate))
            .Select(p => (int?)p.SupervisorId)
            .FirstOrDefaultAsync(ct);
        var buildingCode = await db.Buildings.Where(b => b.Id == buildingId).Select(b => b.Code).FirstOrDefaultAsync(ct) ?? string.Empty;
        var holderName = holderId is null
            ? null
            : await db.Users.Where(u => u.Id == holderId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
        return AccountChecks.SupervisorSlotTaken(buildingCode, shift, holderName ?? "หัวหน้าคนอื่น");
    }
}
