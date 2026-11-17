using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Attendance;

/// <summary>facility-0031: the four events in order. facility-0051: a forgotten Break-In stays empty, so Shift-Out may follow Break-Out.</summary>
public static class AttendanceRules
{
    public static IReadOnlyList<AttendanceEvent> NextAllowed(IReadOnlyCollection<AttendanceEvent> recorded) =>
        recorded.Contains(AttendanceEvent.ShiftOut) ? []
        : recorded.Contains(AttendanceEvent.BreakIn) ? [AttendanceEvent.ShiftOut]
        : recorded.Contains(AttendanceEvent.BreakOut) ? [AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut]
        : recorded.Contains(AttendanceEvent.ShiftIn) ? [AttendanceEvent.BreakOut, AttendanceEvent.ShiftOut]
        : [AttendanceEvent.ShiftIn];

    /// <summary>Owner decision 2026-10-09: a Cleaner may submit from Shift-In until Shift-Out; a break does not stop it.</summary>
    public static bool IsOnDuty(IReadOnlyCollection<AttendanceEvent> recorded) =>
        recorded.Contains(AttendanceEvent.ShiftIn) && !recorded.Contains(AttendanceEvent.ShiftOut);

    private static readonly AttendanceEvent[] InOrder =
        [AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut];

    /// <summary>
    /// facility-0051: what an Admin's correction may leave. Break-Out and Shift-Out need a Shift-In, a Break-In needs a
    /// Break-Out, and the times run Shift-In, Break-Out, Break-In, Shift-Out, each strictly later. Null when valid,
    /// otherwise the Thai message the Admin page shows.
    /// </summary>
    public static string? CheckTimes(IReadOnlyDictionary<AttendanceEvent, DateTime> events)
    {
        if (events.Count > 0 && !events.ContainsKey(AttendanceEvent.ShiftIn))
        {
            return "ต้องมีเวลาเข้างานก่อน";
        }

        if (events.ContainsKey(AttendanceEvent.BreakIn) && !events.ContainsKey(AttendanceEvent.BreakOut))
        {
            return "ต้องมีเวลาออกพักก่อนกลับจากพัก";
        }

        var times = InOrder.Where(e => events.ContainsKey(e)).Select(e => events[e]).ToList();
        return times.Zip(times.Skip(1)).Any(pair => pair.Second <= pair.First)
            ? "เวลาต้องเรียงกัน: เข้างาน → ออกพัก → กลับจากพัก → เลิกงาน"
            : null;
    }

    private const string NumberedOrderMessage =
        "เวลาต้องเรียงกัน: เข้างาน → ออกพัก 1 → กลับจากพัก 1 → ออกพัก 2 → กลับจากพัก 2 → เลิกงาน";

    /// <summary>
    /// Spec §8 (ADR facility-0222): <c>Actual recorded times ต้องเรียงตาม ordinal ของ lifecycle</c>. A Legacy4 slot is
    /// checked exactly as before — same rule, same Thai message — so an old correction keeps its meaning; a Numbered6 slot
    /// is checked against <see cref="AttendanceLifecycle.Order(AttendanceLifecycleVersion.Numbered6)"/>. A missing pair is
    /// allowed, because an explicit gap is exactly how a forgotten break is recorded: a Break-1-In needs no Break-1-Out.
    /// </summary>
    public static string? CheckTimes(AttendanceLifecycleVersion version, IReadOnlyDictionary<AttendanceEvent, DateTime> events) =>
        version == AttendanceLifecycleVersion.Legacy4 ? CheckTimes(events) : CheckNumberedTimes(events);

    /// <summary>
    /// The numbered six in lifecycle order: a Shift-In comes first, only numbered actions may appear, and each later-ordinal
    /// action is strictly later than the earlier-ordinal ones present.
    /// </summary>
    private static string? CheckNumberedTimes(IReadOnlyDictionary<AttendanceEvent, DateTime> events)
    {
        if (events.Count > 0 && !events.ContainsKey(AttendanceEvent.ShiftIn))
        {
            return "ต้องมีเวลาเข้างานก่อน";
        }

        var order = AttendanceLifecycle.Order(AttendanceLifecycleVersion.Numbered6);
        if (events.Keys.Any(action => !order.Contains(action)))
        {
            return NumberedOrderMessage;
        }

        var times = order.Where(events.ContainsKey).Select(action => events[action]).ToList();
        return times.Zip(times.Skip(1)).Any(pair => pair.Second <= pair.First) ? NumberedOrderMessage : null;
    }

    /// <summary>facility-0051 rule 2: what a Cleaner who came forgot to scan, once the shift is over. Nothing for one who never checked in.</summary>
    public static IReadOnlyList<AttendanceEvent> MissingAtShiftEnd(IReadOnlyCollection<AttendanceEvent> recorded)
    {
        if (!recorded.Contains(AttendanceEvent.ShiftIn))
        {
            return [];
        }

        var missing = new List<AttendanceEvent>();
        if (recorded.Contains(AttendanceEvent.BreakOut) && !recorded.Contains(AttendanceEvent.BreakIn))
        {
            missing.Add(AttendanceEvent.BreakIn);
        }

        if (!recorded.Contains(AttendanceEvent.ShiftOut))
        {
            missing.Add(AttendanceEvent.ShiftOut);
        }

        return missing;
    }
}
