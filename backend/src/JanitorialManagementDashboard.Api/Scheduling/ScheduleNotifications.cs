using System.Globalization;
using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §12: which stored facts a committed command invalidated, in identities only — the people whose slots moved and the
/// building/shift scopes it applied to. It deliberately carries no schedule payload: the event tells a page to refetch its
/// own authorised read, and nothing in it can be used by a recipient who is not entitled to the underlying rows.
/// </summary>
public sealed record ScheduleChangeScope(
    IReadOnlyList<ScheduleSlotKey> Slots,
    IReadOnlyList<ScheduleScopeKey> Scopes,
    IReadOnlyList<int> BaselinePeople,
    IReadOnlyList<ScheduleScopedDate>? DatedScopes = null)
{
    /// <summary>A command that touched no slot and no scope has no affected staff to refresh.</summary>
    public static ScheduleChangeScope None { get; } = new([], [], []);
}

/// <summary>Spec §12: the whole pushed payload — the identity of the command, never a person or a schedule value.</summary>
public sealed record ScheduleInvalidation(Guid CommandId);

/// <summary>
/// Spec §12 and facility-0105: the real post-commit fan-out. Recipients are re-resolved at send time, not from a group a
/// connection joined when it opened: the affected people, the dated Supervisors of every affected building/shift on the
/// affected dates, and the active Admins. A deactivated account is filtered out of the final set, so a page that stayed
/// open after the account lost its rights receives nothing further. Every recipient already holds a right to some part of
/// what changed, and the push itself says only "refetch".
/// <para>
/// This never swallows a failure itself: the command engine logs and swallows it, because a push that failed must not
/// change the outcome of a committed command.
/// </para>
/// </summary>
public sealed class ScheduleNotifications(IHubContext<ScanHub> hub, AppDbContext db, TimeProvider clock)
    : IScheduleCommandNotifier
{
    public const string EventName = "ScheduleChanged";

    public Task NotifyCommittedAsync(CommandResult result, ScheduleChangeScope scope, CancellationToken ct = default) =>
        ChangedAsync(result.CommandId, scope, ct);

    /// <summary>
    /// Sends one invalidation to every account that is active and authorised now. The recipients are computed after the
    /// command committed, so a Supervisor who lost a building or a Cleaner who was deactivated between the read and the
    /// write is not told anything, and an Admin added a moment ago is.
    /// </summary>
    public async Task ChangedAsync(
        Guid commandId, ScheduleChangeScope scope, CancellationToken ct = default)
    {
        var recipients = await RecipientsAsync(scope, ct);
        if (recipients.Count == 0)
        {
            return;
        }

        await hub.Clients.Users(recipients).SendAsync(EventName, new ScheduleInvalidation(commandId), ct);
    }

    private async Task<IReadOnlyList<string>> RecipientsAsync(ScheduleChangeScope scope, CancellationToken ct)
    {
        var businessDate = ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime);

        var slots = scope.Slots.Distinct().ToList();
        var ids = new HashSet<int>(slots.Select(slot => slot.PersonId));
        ids.UnionWith(scope.BaselinePeople);
        foreach (var (target, date) in await TargetsAsync(scope, slots, businessDate, ct))
        {
            var affected = await ScheduleScopeReader.SupervisorsOfAsync(
                db, target.BuildingId, date, target.Shift, includeCompatibility: date >= businessDate, ct);
            if (date < businessDate)
            {
                // Historical schedule reads also require responsibility for the same building and shift today.
                var current = await ScheduleScopeReader.SupervisorsOfAsync(
                    db, target.BuildingId, businessDate, target.Shift, includeCompatibility: true, ct);
                ids.UnionWith(affected.Intersect(current));
            }
            else
            {
                ids.UnionWith(affected);
            }
        }

        ids.UnionWith(await db.Users.AsNoTracking()
            .Where(user => user.Role == UserRole.Admin && user.IsActive)
            .Select(user => user.Id)
            .ToListAsync(ct));

        // The final read is the authority on "active now": a person or Supervisor deactivated since the command ran is
        // dropped here, and the connection they still hold receives nothing.
        var active = await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id) && user.IsActive)
            .Select(user => user.Id)
            .ToListAsync(ct);
        return active.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
    }

    /// <summary>
    /// Every building whose view holds an affected slot: the scopes the command itself applied to, the person's dated home
    /// on that slot's date, and the origin/destination building of a transfer instruction on the slot. The transfer kinds
    /// lock only the person's slot (the Area rows are the instruction's own identity), so their building views would
    /// otherwise never learn that the person moved.
    /// </summary>
    private async Task<IReadOnlyCollection<ScheduleScopedDate>> TargetsAsync(
        ScheduleChangeScope scope, IReadOnlyList<ScheduleSlotKey> slots, DateOnly businessDate, CancellationToken ct)
    {
        var datesByShift = slots.ToLookup(slot => slot.Shift, slot => slot.ShiftDate);
        var targets = (scope.DatedScopes ?? scope.Scopes.SelectMany(key => datesByShift[key.Shift].DefaultIfEmpty(businessDate)
            .Select(date => new ScheduleScopedDate(key, date))).ToList()).ToHashSet();
        if (slots.Count == 0)
        {
            return targets;
        }

        var personIds = slots.Select(slot => slot.PersonId).Distinct().ToList();
        var profiles = await db.CleanerProfilePeriods.AsNoTracking()
            .Where(profile => personIds.Contains(profile.UserId) && profile.HomeBuildingId != null)
            .Select(profile => new { profile.UserId, profile.HomeBuildingId, profile.EffectiveFrom, profile.EffectiveUntil })
            .ToListAsync(ct);

        foreach (var slot in slots)
        {
            var home = profiles
                .Where(profile => profile.UserId == slot.PersonId)
                .Where(profile => profile.EffectiveFrom <= slot.ShiftDate
                    && (profile.EffectiveUntil == null || slot.ShiftDate < profile.EffectiveUntil.Value))
                .MaxBy(profile => profile.EffectiveFrom)?.HomeBuildingId;
            if (home is int building)
            {
                targets.Add(new ScheduleScopedDate(new ScheduleScopeKey(building, slot.Shift), slot.ShiftDate));
            }

            var areas = await db.AreaTransfers.AsNoTracking()
                .Where(transfer => transfer.PersonId == slot.PersonId
                    && transfer.ShiftDate == slot.ShiftDate && transfer.Shift == slot.Shift)
                .Select(transfer => new { transfer.FromAreaId, transfer.ToAreaId })
                .ToListAsync(ct);
            if (areas.Count == 0)
            {
                continue;
            }

            var areaIds = areas.Select(area => area.FromAreaId)
                .Concat(areas.Select(area => area.ToAreaId)).Distinct().ToList();
            var buildings = await db.Areas.AsNoTracking().Where(area => areaIds.Contains(area.Id))
                .Select(area => area.BuildingId).Distinct().ToListAsync(ct);
            targets.UnionWith(buildings.Select(id => new ScheduleScopedDate(new ScheduleScopeKey(id, slot.Shift), slot.ShiftDate)));
        }

        return targets;
    }
}
