using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §7, §8 and §14 with ruling A: the one answer to "which Area is this person on duty in for this slot", which every
/// consuming path reads instead of comparing against the mutable <c>User.AreaId</c> projection. <see cref="AreaId"/> is the
/// effective duty Area, <see cref="BreakGroup"/> the first-break group that goes with it (null when nobody has assigned
/// one), and <see cref="Source"/> names where the answer came from: a live relief assignment, the person's resolved day
/// (its own source kind — dated, baseline, unset or unavailable), or the spec §14 compatibility projection.
/// <para>
/// <see cref="IsDayOff"/> and <see cref="IsLeave"/> are conflict markers kept apart from the Area, because an absence record
/// never deletes the underlying eligible Area: <c>สำหรับ day-off/leave check-in ให้ตรวจ Area จาก underlying baseline/dated
/// assignment ที่มีสิทธิ์อยู่ ไม่ลบทิ้งเพียงเพราะมี absence record</c>. <see cref="LeaveStart"/> and <see cref="LeaveEnd"/>
/// are the live leave interval the §8 applicability rule needs, and are null when no agreed leave covers the slot.
/// </para>
/// </summary>
public sealed record EffectiveDuty(
    int? AreaId,
    int? BreakGroup,
    string? Source,
    bool IsDayOff,
    bool IsLeave,
    DateTimeOffset? LeaveStart,
    DateTimeOffset? LeaveEnd)
{
    /// <summary>The Area came from a live relief assignment for the slot (spec §6).</summary>
    public const string ReliefSource = "relief";

    /// <summary>No dated identity exists for the date, so the account's own Area is the only statement of duty (spec §14).</summary>
    public const string CompatibilitySource = "compatibility";
}

/// <summary>
/// Ruling A: the single reader of <see cref="EffectiveDuty"/>. It does not copy the resolver or the relief rule — it asks
/// <see cref="ScheduleStore"/> for the live relief assignment and for the person's resolved day. A person with no dated
/// identity at all has no history to resolve, so their account's Area answers as the compatibility projection; a home
/// building alone grants nothing, which is why a relief with no assignment has no duty Area and is refused exactly as
/// another Area's sign is refused today.
/// </summary>
public static class EffectiveDutyReader
{
    public static async Task<EffectiveDuty> ReadAsync(
        AppDbContext db, ScheduleStore store, User user, ShiftSlot slot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Spec §6: a live assignment for this very slot is the day's duty; a cancelled row holds nothing.
        if (await store.LiveReliefAsync(user.Id, slot.ShiftDate, slot.Shift, ct) is { } relief)
        {
            return new EffectiveDuty(relief.AreaId, relief.BreakGroup, EffectiveDuty.ReliefSource,
                IsDayOff: false, IsLeave: false, LeaveStart: null, LeaveEnd: null);
        }

        // Spec §5 and §7: the resolved day, through the very resolver the week read uses, so a dated Area, a baseline Area
        // and a day off or leave with the underlying Area all answer here and nowhere else.
        var day = await store.ReadPersonDayAsync(user.Id, slot.ShiftDate, slot.Shift, ct);
        var leave = await LiveLeaveIntervalAsync(db, user.Id, slot, ct);

        // An agreed leave is a record of its own, so the marker comes from the live interval as well as the resolved day: a
        // person whose history coverage does not reach this date still has a leave the screen has to show (spec §7).
        var isLeave = leave.Start is not null || (day?.IsLeave ?? false);
        var isDayOff = day?.IsDayOff ?? false;
        if (day?.Area.Value is int planned)
        {
            return new EffectiveDuty(planned, day.BreakGroup.Value, day.Area.SourceKind, isDayOff, isLeave,
                leave.Start, leave.End);
        }

        if (await store.ReadProfileAsync(user.Id, slot.ShiftDate, ct) is not null)
        {
            // The person has a dated identity, so nothing about today's account is history: an unset day stays unset.
            return new EffectiveDuty(null, null, day?.Area.SourceKind, isDayOff, isLeave, leave.Start, leave.End);
        }

        // Spec §14: the compatibility projection answers only for a date no dated identity covers at all.
        return new EffectiveDuty(user.AreaId, null, user.AreaId is null ? null : EffectiveDuty.CompatibilitySource,
            isDayOff, isLeave, leave.Start, leave.End);
    }

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
