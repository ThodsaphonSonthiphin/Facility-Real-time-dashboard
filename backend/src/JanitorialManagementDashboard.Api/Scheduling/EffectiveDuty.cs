using System.Globalization;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §7, §8, §9 and §14 with ruling A: the one answer to "which Area is this person on duty in for this slot", which every
/// consuming path reads instead of comparing against the mutable <c>User.AreaId</c> projection. <see cref="AreaId"/> is the
/// effective duty Area, <see cref="BreakGroup"/> the first-break group that goes with it (null when nobody has assigned
/// one), and <see cref="Source"/> names where the answer came from: a live relief assignment, the person's resolved day
/// (its own source kind — dated, baseline, unset or unavailable), a confirmed midshift transfer, the pending state of a
/// transfer nobody has arrived under, or the spec §14 compatibility projection.
/// <para>
/// <see cref="IsDayOff"/> and <see cref="IsLeave"/> are conflict markers kept apart from the Area, because an absence record
/// never deletes the underlying eligible Area: <c>สำหรับ day-off/leave check-in ให้ตรวจ Area จาก underlying baseline/dated
/// assignment ที่มีสิทธิ์อยู่ ไม่ลบทิ้งเพียงเพราะมี absence record</c>. <see cref="LeaveStart"/> and <see cref="LeaveEnd"/>
/// are the live leave interval the §8 applicability rule needs, and are null when no agreed leave covers the slot.
/// </para>
/// <para>
/// <see cref="Transfer"/> is the slot's latest midshift instruction as the spec §9 wire contract reports it — null when the
/// slot has none — and it is the only place a transfer changes the answer. Once an instruction has taken effect with no
/// confirmation the duty is no Area at all (<see cref="EffectiveTransfer.IsPending"/>); a confirmed arrival makes the
/// destination the duty, and a later forward instruction or cancellation builds on that confirmed destination rather than
/// reverting to the plan. The transfer never resets the break group or the Shift-In, and it is never a departure timestamp.
/// </para>
/// </summary>
public sealed record EffectiveDuty(
    int? AreaId,
    int? BreakGroup,
    string? Source,
    bool IsDayOff,
    bool IsLeave,
    DateTimeOffset? LeaveStart,
    DateTimeOffset? LeaveEnd,
    EffectiveTransfer? Transfer = null)
{
    /// <summary>The Area came from a live relief assignment for the slot (spec §6).</summary>
    public const string ReliefSource = "relief";

    /// <summary>No dated identity exists for the date, so the account's own Area is the only statement of duty (spec §14).</summary>
    public const string CompatibilitySource = "compatibility";

    /// <summary>A confirmed midshift transfer makes the destination the slot's duty (spec §9).</summary>
    public const string TransferSource = "transfer";

    /// <summary>
    /// An instruction has taken effect and nobody has confirmed arrival, so no Area may record an actual action: the
    /// source's responsibility has left and the destination's has not been evidenced (spec §9).
    /// </summary>
    public const string TransferPendingSource = "transfer-pending";

    /// <summary>True while an instruction is in effect and unconfirmed: the duty Area is unusable, not merely unknown.</summary>
    public bool IsTransferPending => Transfer is { IsPending: true };
}

/// <summary>
/// Spec §9: the state words the transfer contract uses, on the wire and in the read. They are the three states an
/// instruction can be in — never arrived under, arrived under, or cancelled before either — and a confirmed one is history
/// rather than something to remove (ADR facility-0213).
/// </summary>
public static class TransferStates
{
    /// <summary>Recorded and not yet arrived under. Before <c>effectiveAt</c> the origin is still the duty.</summary>
    public const string Instructed = "instructed";

    /// <summary>The person confirmed arrival at the destination sign; the destination is the duty.</summary>
    public const string Confirmed = "confirmed";

    /// <summary>Cancelled before any arrival. The row is kept with its history and holds no responsibility.</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>
/// Spec §9: one slot's transfer instruction as the effective-duty read reports it — the instruction's identity, the two
/// Areas, its plan time, its state word, the actual arrival time when there is one, and the revision a client echoes back.
/// <see cref="IsPending"/> is the only derived decision: the instruction is in force and unconfirmed, so neither Area may act.
/// </summary>
public sealed record EffectiveTransfer(
    int Id,
    int FromAreaId,
    int ToAreaId,
    DateTimeOffset EffectiveAt,
    string State,
    DateTimeOffset? ConfirmedAt,
    string Revision,
    bool IsPending);

/// <summary>
/// Ruling A: the single reader of <see cref="EffectiveDuty"/>. It does not copy the resolver or the relief rule — it asks
/// <see cref="ScheduleStore"/> for the live relief assignment, the slot's transfer and the person's resolved day. A person
/// with no dated identity at all has no history to resolve, so their account's Area answers as the compatibility projection,
/// while a person who has one never borrows it — a date their periods do not reach stays unavailable (spec §10); a home
/// building alone grants nothing, which is why a relief with no assignment has no duty Area and is refused exactly as
/// another Area's sign is refused today.
/// </summary>
public static class EffectiveDutyReader
{
    /// <summary>
    /// The slot's effective duty at <paramref name="now"/>. The clock is a parameter rather than a dependency because the
    /// transfer rule is the one thing here that needs it: before an instruction's effective time the duty is still the
    /// origin, at and after it the answer depends on whether arrival was confirmed (spec §9).
    /// </summary>
    public static async Task<EffectiveDuty> ReadAsync(
        AppDbContext db, ScheduleStore store, User user, ShiftSlot slot, DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var duty = await ResolveAsync(db, store, user, slot, ct);

        // Spec §9 and ADRs facility-0203/0204: the instruction moves responsibility, not the shift. Before the effective
        // time nothing changes; an unconfirmed instruction in force removes the duty entirely (both Areas are refused until
        // the destination is confirmed); a confirmed arrival makes the destination the duty and keeps every other answer —
        // the break group and the absence markers — exactly as it was.
        return await store.ReadSlotTransferAsync(user.Id, slot.ShiftDate, slot.Shift, ct) is { } transfer
            ? ApplyTransfer(duty, transfer, now)
            : duty;
    }

    private static EffectiveDuty ApplyTransfer(EffectiveDuty duty, SlotTransfer transfer, DateTimeOffset now)
    {
        var instruction = transfer.Instruction;
        var state = instruction.IsCancelled
            ? TransferStates.Cancelled
            : transfer.Confirmation is not null ? TransferStates.Confirmed : TransferStates.Instructed;
        var effectiveAt = new DateTimeOffset(
            DateTime.SpecifyKind(instruction.EffectiveAt, DateTimeKind.Utc), TimeSpan.Zero);
        var confirmedAt = transfer.Confirmation is { } confirmation
            ? new DateTimeOffset(DateTime.SpecifyKind(confirmation.OccurredAt, DateTimeKind.Utc), TimeSpan.Zero)
            : (DateTimeOffset?)null;

        var instructed = new EffectiveTransfer(instruction.Id, instruction.FromAreaId, instruction.ToAreaId,
            effectiveAt, state, confirmedAt, instruction.Revision.ToString(CultureInfo.InvariantCulture),
            // A cancelled instruction holds nothing, and an instruction that has not taken effect yet has not moved anything.
            IsPending: state == TransferStates.Instructed && now >= effectiveAt);

        if (state == TransferStates.Confirmed)
        {
            return duty with { AreaId = instruction.ToAreaId, Source = EffectiveDuty.TransferSource, Transfer = instructed };
        }

        // ADR facility-0213: a confirmed arrival stays evidence, so a later forward instruction builds on the destination it
        // reached rather than on the plan's original Area. Both the instruction that has not taken effect yet and the one
        // that was cancelled leave responsibility exactly where the last confirmed arrival put it.
        var areaId = transfer.LastConfirmedAreaId ?? duty.AreaId;
        var source = transfer.LastConfirmedAreaId is null ? duty.Source : EffectiveDuty.TransferSource;
        if (!instructed.IsPending)
        {
            // Before the effective time (or after a cancellation) the answer is unchanged from the last confirmed state: the
            // row is shown, and the plan only answers when nothing was ever confirmed.
            return duty with { AreaId = areaId, Source = source, Transfer = instructed };
        }

        return duty with { AreaId = null, Source = EffectiveDuty.TransferPendingSource, Transfer = instructed };
    }

    /// <summary>
    /// The Area the plan gives the slot, before any transfer is considered: a live relief assignment, else the resolved day,
    /// else — only for a person with no dated identity at all — the spec §14 compatibility projection.
    /// </summary>
    private static async Task<EffectiveDuty> ResolveAsync(
        AppDbContext db, ScheduleStore store, User user, ShiftSlot slot, CancellationToken ct)
    {
        var leave = await LiveLeaveIntervalAsync(db, user.Id, slot, ct);
        // Spec §6: a live assignment for this very slot is the day's duty; a cancelled row holds nothing, and an agreed
        // leave interval still applies to the slot (spec §7).
        if (await store.LiveReliefAsync(user.Id, slot.ShiftDate, slot.Shift, ct) is { } relief)
        {
            return new EffectiveDuty(relief.AreaId, relief.BreakGroup, EffectiveDuty.ReliefSource,
                IsDayOff: false, IsLeave: leave.Start is not null, leave.Start, leave.End);
        }

        // Spec §5 and §7: the resolved day, through the very resolver the week read uses, so a dated Area, a baseline Area
        // and a day off or leave with the underlying Area all answer here and nowhere else.
        var day = await store.ReadPersonDayAsync(user.Id, slot.ShiftDate, slot.Shift, ct);

        // An agreed leave is a record of its own, so the marker comes from the live interval as well as the resolved day: a
        // person whose history coverage does not reach this date still has a leave the screen has to show (spec §7).
        var isLeave = leave.Start is not null || (day?.IsLeave ?? false);
        var isDayOff = day?.IsDayOff ?? false;
        if (day?.Area.Value is int planned)
        {
            return new EffectiveDuty(planned, day.BreakGroup.Value, day.Area.SourceKind, isDayOff, isLeave,
                leave.Start, leave.End);
        }

        // Spec §14 with the Task 9 review ruling: the compatibility projection answers only for a person with no dated
        // identity at all — one CleanerProfilePeriod row on any date is enough to take it away. A slot dated before the
        // first period's EffectiveFrom is outside history coverage, so the resolver's own "unavailable" answer stands with a
        // null Area (spec §10) instead of borrowing today's mutable User.AreaId for a historical slot.
        if (await HasDatedIdentityAsync(db, user.Id, ct))
        {
            // The person has a dated identity, so nothing about today's account is history: an unset day stays unset and a
            // date the profile does not reach stays unavailable.
            return new EffectiveDuty(null, null, day?.Area.SourceKind, isDayOff, isLeave, leave.Start, leave.End);
        }

        // Spec §14: the compatibility Area is the whole answer for a person whose identity is only the account columns.
        return new EffectiveDuty(user.AreaId, null, user.AreaId is null ? null : EffectiveDuty.CompatibilitySource,
            isDayOff, isLeave, leave.Start, leave.End);
    }

    /// <summary>
    /// True when the person has any dated identity row at all, on any date — the one condition under which the spec §14
    /// compatibility projection may answer. Asking for the presence of a period rather than for coverage of this date is
    /// what keeps a slot dated before the first EffectiveFrom from falling through to today's account Area.
    /// </summary>
    private static Task<bool> HasDatedIdentityAsync(AppDbContext db, int userId, CancellationToken ct) =>
        db.CleanerProfilePeriods.AsNoTracking().AnyAsync(profile => profile.UserId == userId, ct);

    /// <summary>
    /// The live leave interval of the slot as the half-open range the §8 rule tests against: the earliest start and the
    /// latest end of the agreed leave rows nobody cancelled. Both null when there is no live leave.
    /// </summary>
    private static async Task<(DateTimeOffset? Start, DateTimeOffset? End)> LiveLeaveIntervalAsync(
        AppDbContext db, int userId, ShiftSlot slot, CancellationToken ct)
    {
        var rows = await db.ScheduleExceptions.AsNoTracking()
            .Where(row => row.UserId == userId && row.ShiftDate == slot.ShiftDate && row.Shift == slot.Shift
                && row.Kind == ScheduleExceptionKind.Leave && !row.IsCancelled)
            .Select(row => new { row.StartsAt, row.EndsAt })
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return (null, null);
        }

        return (new DateTimeOffset(DateTime.SpecifyKind(rows.Min(row => row.StartsAt!.Value), DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(rows.Max(row => row.EndsAt!.Value), DateTimeKind.Utc)));
    }
}
