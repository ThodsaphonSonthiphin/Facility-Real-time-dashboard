using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>
/// Spec §8 and §11: the one read of a person's slot as a page and a write both see it — the ordered lifecycle, what each
/// action is right now, the stored gap markers, what may be recorded next, and the revision a write sends back. The scan
/// route, the attendance page, the work page and the scoped missing-time correction all answer from here, so a screen can
/// never be offered a different contract from the one the write enforces.
/// </summary>
internal static class AttendanceStateView
{
    /// <summary>An actual event exists for the action.</summary>
    public const string Recorded = "recorded";

    /// <summary>An explicit skip marker was stored: the action is answered without inventing a time for it.</summary>
    public const string Skipped = "skipped";

    /// <summary>The plan's instant has passed with no scan and no applicable leave.</summary>
    public const string Missing = "missing";

    /// <summary>A recorded departure plus applicable leave make the action unnecessary (spec §7).</summary>
    public const string NotApplicable = "not-applicable";

    /// <summary>Nothing is due yet, or the plan knows no instant to hold the action against.</summary>
    public const string Pending = "pending";

    public static async Task<AttendanceSlotState> LoadAsync(
        AppDbContext db, int userId, ShiftSlot slot, EffectiveDuty duty, DateTime nowUtc, CancellationToken ct = default)
    {
        var facts = await AttendanceEndpoints.LoadEventsAsync(db, userId, slot);
        var gaps = await db.AttendanceGaps.AsNoTracking()
            .Where(gap => gap.UserId == userId && gap.ShiftDate == slot.ShiftDate && gap.Shift == slot.Shift)
            .OrderBy(gap => gap.Id)
            .ToListAsync(ct);
        var stored = await db.AttendanceSlotLifecycles.AsNoTracking()
            .Where(row => row.ShiftDate == slot.ShiftDate && row.Shift == slot.Shift)
            .Select(row => (AttendanceLifecycleVersion?)row.Version)
            .FirstOrDefaultAsync(ct);

        var version = AttendanceLifecycle.VersionFor(stored);
        var recorded = facts.Select(fact => fact.EventType).ToList();
        var skipped = gaps.Where(gap => gap.Status == AttendanceGapStatus.Skipped)
            .Select(gap => gap.EventType)
            .ToHashSet();
        var choices = AttendanceLifecycle.NextActions(version, recorded, skipped);

        return new AttendanceSlotState(
            slot,
            version,
            AttendanceStateRevision.Of(userId, slot, facts),
            facts.Select(fact => new AttendanceEntryDto(fact.EventType, Utc(fact.OccurredAt), fact.Source,
                fact.WithinRadius, Utc(fact.CreatedAt))).ToList(),
            Describe(version, slot, duty, facts, skipped, nowUtc),
            gaps.Select(gap => new AttendanceGapDto(gap.EventType, gap.Status.ToString(), gap.Reason,
                gap.RecordedAt is { } at ? Utc(at) : null)).ToList(),
            choices.Select(choice => new AttendanceChoiceDto(choice.Action, choice.RequiredGaps)).ToList());
    }

    /// <summary>
    /// Every action of the slot's lifecycle with what it is now. A recorded fact wins; a stored skip marker comes next; then
    /// a Numbered6 slot classifies an action whose expected instant the plan knows — using the recorded final Shift-Out as
    /// the departure and the live leave interval as the bounds — while an action with no known instant, and every unresolved
    /// action of a Legacy4 slot, stays "pending", which is that contract's old meaning.
    /// </summary>
    private static List<AttendanceActionDto> Describe(
        AttendanceLifecycleVersion version,
        ShiftSlot slot,
        EffectiveDuty duty,
        IReadOnlyList<ShiftAttendance> facts,
        HashSet<AttendanceEvent> skipped,
        DateTime nowUtc)
    {
        var order = AttendanceLifecycle.Order(version);
        var byAction = facts.GroupBy(fact => fact.EventType).ToDictionary(group => group.Key, group => group.First());
        var expected = AttendanceExpectations.ForSlot(slot, duty.BreakGroup);
        var departure = facts.FirstOrDefault(fact => fact.EventType == AttendanceEvent.ShiftOut) is { } final
            ? UtcOffset(final.OccurredAt)
            : (DateTimeOffset?)null;
        var now = UtcOffset(nowUtc);

        var actions = new List<AttendanceActionDto>(order.Count);
        foreach (var action in order)
        {
            var expectedAt = expected.TryGetValue(action, out var instant) ? Utc(instant) : (DateTime?)null;
            if (byAction.TryGetValue(action, out var fact))
            {
                actions.Add(new AttendanceActionDto(action, Recorded, Utc(fact.OccurredAt), fact.Source, expectedAt));
                continue;
            }

            if (skipped.Contains(action))
            {
                actions.Add(new AttendanceActionDto(action, Skipped, null, null, expectedAt));
                continue;
            }

            var state = version == AttendanceLifecycleVersion.Numbered6 && expected.TryGetValue(action, out var due)
                ? Word(AttendanceApplicability.Classify(UtcOffset(due), now, departure, duty.LeaveStart, duty.LeaveEnd))
                : Pending;
            actions.Add(new AttendanceActionDto(action, state, null, null, expectedAt));
        }

        return actions;
    }

    private static string Word(AttendanceApplicabilityStatus status) => status switch
    {
        AttendanceApplicabilityStatus.Missing => Missing,
        AttendanceApplicabilityStatus.NotApplicable => NotApplicable,
        _ => Pending,
    };

    /// <summary>A stored DATETIME is UTC; the kind is attached explicitly instead of from the machine's zone.</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);

    private static DateTimeOffset UtcOffset(DateTime stored) => new(Utc(stored), TimeSpan.Zero);
}

/// <summary>One loaded slot: everything a screen shows and everything a write needs to check, read once.</summary>
internal sealed record AttendanceSlotState(
    ShiftSlot Slot,
    AttendanceLifecycleVersion Version,
    string Revision,
    IReadOnlyList<AttendanceEntryDto> Events,
    IReadOnlyList<AttendanceActionDto> Actions,
    IReadOnlyList<AttendanceGapDto> Gaps,
    IReadOnlyList<AttendanceChoiceDto> NextActions)
{
    /// <summary>The wire shape, with the effective duty the state was read against (spec §7) and its transfer (spec §9).</summary>
    public AttendanceStateDto ToDto(EffectiveDuty duty, AttendanceOutcome? outcome) => new(
        Slot.ShiftDate,
        Slot.Shift,
        Events,
        NextActions.Select(choice => choice.Action).ToList(),
        outcome,
        Version.ToString(),
        Revision,
        Actions,
        Gaps,
        NextActions,
        duty.AreaId,
        duty.BreakGroup,
        duty.Source,
        duty.IsDayOff,
        duty.IsLeave,
        duty.Transfer is { } transfer
            ? new AttendanceTransferDto(transfer.Id, transfer.FromAreaId, transfer.ToAreaId,
                transfer.EffectiveAt.UtcDateTime, transfer.State, transfer.ConfirmedAt?.UtcDateTime, transfer.Revision)
            : null);
}
