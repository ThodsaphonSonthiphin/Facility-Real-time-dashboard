using JanitorialManagementDashboard.Api.Endpoints;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>One responsibility a membership write ended: the building and shift of the period it closed or removed.</summary>
public sealed record EndedScope(int BuildingId, Shift Shift);

/// <summary>
/// What a membership write did: the answer to give when it refused the change, and the responsibilities it ended, which
/// the caller names in its audit row because the period rows alone show dates and not "which building".
/// </summary>
public sealed record MembershipOutcome(IResult? Refusal, IReadOnlyList<EndedScope> Ended);

/// <summary>
/// The dated membership writes of spec §3: an Admin's account create, activate, building/shift change or deactivate of a
/// Supervisor takes effect on the business clock's Thai date at once. The period being replaced is closed end-exclusive on
/// that date and the new one opens on it; a period that would end where it starts is removed instead of being stored
/// empty. Only the building and shift the form replaced is ended: a Supervisor may hold several buildings (spec §3), so a
/// single-field edit must never take the others away. Every check runs under the building/shift
/// <c>ScheduleScopeAnchor</c> the store takes, so two Admins cannot both give one building and shift to different people.
/// </summary>
public sealed class SupervisorMembership(AppDbContext db, ScheduleStore store, TimeProvider clock)
{
    /// <summary>
    /// Applies the membership, inside the caller's transaction. Null <see cref="MembershipOutcome.Refusal"/> means it was
    /// applied; otherwise it is the answer to give (a 409 naming the other holder). <paramref name="open"/> is false for a
    /// deactivation, which ends everything it held. <paramref name="replaced"/> is the building and shift the account row
    /// carried before the edit (<c>null</c> on create and on activation), so only that pair ends.
    /// </summary>
    public async Task<MembershipOutcome> ApplyAsync(
        User supervisor, bool open, int actorId, (int BuildingId, Shift Shift)? replaced = null, CancellationToken ct = default)
    {
        if (supervisor.Role != UserRole.Supervisor)
        {
            return new MembershipOutcome(null, []);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var businessDate = ScheduleAuthority.BusinessDate(now);
        var held = await db.SupervisorScopePeriods
            .Where(p => p.SupervisorId == supervisor.Id && p.EffectiveUntil == null)
            .ToListAsync(ct);

        var target = open && supervisor.BuildingId is int buildingId && supervisor.Shift is Shift shift
            ? (BuildingId: buildingId, Shift: shift)
            : ((int BuildingId, Shift Shift)?)null;

        // Which open periods this write ends: a deactivation ends all of them, an edit ends only the pair the form
        // replaced, and a create or an activation has nothing to replace.
        List<SupervisorScopePeriod> ends;
        if (target is null)
        {
            ends = held;
        }
        else if (replaced is { } pair)
        {
            if ((pair.BuildingId, pair.Shift) == (target.Value.BuildingId, target.Value.Shift))
            {
                // The pair replaced is the pair asked for: the membership already says exactly this
                return new MembershipOutcome(null, []);
            }

            ends = held.Where(p => (p.BuildingId, p.Shift) == (pair.BuildingId, pair.Shift)).ToList();
        }
        else
        {
            ends = [];
        }

        var ended = new List<EndedScope>(ends.Count);
        foreach (var period in ends)
        {
            ended.Add(new EndedScope(period.BuildingId, period.Shift));
            Close(period, businessDate, now);
        }

        if (target is not { } wanted)
        {
            return new MembershipOutcome(null, ended);
        }

        if (held.Any(p => !ends.Contains(p) && p.BuildingId == wanted.BuildingId && p.Shift == wanted.Shift))
        {
            // The period for this building and shift is already open: nothing changed for the members it names
            return new MembershipOutcome(null, ended);
        }

        await store.LockRegularScopeAsync(wanted.BuildingId, wanted.Shift, now, ct);
        try
        {
            await store.EnsureRegularScopeAvailableUnderLockAsync(wanted.BuildingId, wanted.Shift, businessDate, null, ct);
        }
        catch (ScheduleConflictException)
        {
            return new MembershipOutcome(
                AccountChecks.Conflict(await TakenMessageAsync(wanted.BuildingId, wanted.Shift, ct)), ended);
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
        return new MembershipOutcome(null, ended);
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

        // EffectiveUntil must be after EffectiveFrom: a period that starts today has no day left to keep. The caller names
        // the ended building in its audit row, so the history of the responsibility is not lost with the row.
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
