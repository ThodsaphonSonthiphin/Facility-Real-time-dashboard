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
        User supervisor, bool open, int actorId, (int BuildingId, Shift Shift)? replaced = null,
        (int? BuildingId, Shift? Shift)? targetOverride = null, Func<Task>? persistUserAsync = null,
        CancellationToken ct = default)
    {
        if (supervisor.Role != UserRole.Supervisor)
        {
            return new MembershipOutcome(null, []);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var businessDate = ScheduleAuthority.BusinessDate(now);
        // Include finite future periods when deactivating so no active or future Supervisor scope survives deactivation.
        var held = supervisor.Id > 0
            ? await db.SupervisorScopePeriods
                .Where(p => p.SupervisorId == supervisor.Id
                    && (p.EffectiveUntil == null || (!open && p.EffectiveUntil.Value > businessDate)))
                .ToListAsync(ct)
            : [];

        var targetBuildingId = targetOverride is not null ? targetOverride.Value.BuildingId : supervisor.BuildingId;
        var targetShift = targetOverride is not null ? targetOverride.Value.Shift : supervisor.Shift;
        var target = open && targetBuildingId is int buildingId && targetShift is Shift shift
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
                if (persistUserAsync is not null)
                {
                    await persistUserAsync();
                }

                return new MembershipOutcome(null, []);
            }

            ends = held.Where(p => (p.BuildingId, p.Shift) == (pair.BuildingId, pair.Shift)).ToList();
        }
        else
        {
            ends = [];
        }

        var ended = new List<EndedScope>(ends.Count);
        var alreadyOpen = target is { } t
            && held.Any(p => !ends.Contains(p) && p.BuildingId == t.BuildingId && p.Shift == t.Shift);

        // Acquire the scope lock and verify availability BEFORE mutating/adding User or closing prior periods,
        // so LockRegularScopeAsync's SaveChangesAsync never flushes an unvalidated User row early.
        if (target is { } wanted && !alreadyOpen)
        {
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

            // Clear stale users.BuildingId projection on any other active Supervisor whose dated scope already ended,
            // preventing false users.supervisor_slot collisions once the dated lock has confirmed the slot is free.
            // Never touch a pre-backfill Supervisor who has no dated periods at all (spec §14: they still hold the slot).
            var staleHolders = await db.Users
                .Where(u => u.Id != supervisor.Id && u.Role == UserRole.Supervisor && u.IsActive
                    && u.BuildingId == wanted.BuildingId && u.Shift == wanted.Shift
                    && db.SupervisorScopePeriods.Any(p => p.SupervisorId == u.Id))
                .ToListAsync(ct);
            if (staleHolders.Count > 0)
            {
                foreach (var stale in staleHolders)
                {
                    stale.BuildingId = null;
                }

                await db.SaveChangesAsync(ct);
            }
        }

        if (persistUserAsync is not null)
        {
            await persistUserAsync();
        }

        foreach (var period in ends)
        {
            ended.Add(new EndedScope(period.BuildingId, period.Shift));
            Close(period, businessDate, now);
        }

        if (target is not { } openTarget || alreadyOpen)
        {
            return new MembershipOutcome(null, ended);
        }

        db.SupervisorScopePeriods.Add(new SupervisorScopePeriod
        {
            SupervisorId = supervisor.Id,
            BuildingId = openTarget.BuildingId,
            Shift = openTarget.Shift,
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
