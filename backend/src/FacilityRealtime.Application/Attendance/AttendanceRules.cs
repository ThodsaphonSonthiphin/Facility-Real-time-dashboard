using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Attendance;

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
