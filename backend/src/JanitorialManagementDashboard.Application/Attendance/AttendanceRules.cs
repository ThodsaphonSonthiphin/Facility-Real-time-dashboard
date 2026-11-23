using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Attendance;

public static class AttendanceRules
{
    /// <summary>Owner decision 2026-10-09: a Cleaner may submit from Shift-In until Shift-Out; a break does not stop it.</summary>
    public static bool IsOnDuty(IReadOnlyCollection<AttendanceEvent> recorded) =>
        recorded.Contains(AttendanceEvent.ShiftIn) && !recorded.Contains(AttendanceEvent.ShiftOut);

    private static string OrderMessage(Shift? shift) => shift == Shift.Day
        ? "เวลาต้องเรียงกัน: เข้างาน → พักเที่ยง → กลับจากพักเที่ยง → พักบ่าย → กลับจากพักบ่าย → เลิกงาน"
        : "เวลาต้องเรียงกัน: เข้างาน → พักรอบ 1 → กลับจากพักรอบ 1 → พักรอบ 2 → กลับจากพักรอบ 2 → เลิกงาน";

    public static string? CheckTimes(IReadOnlyDictionary<AttendanceEvent, DateTime> events, Shift? shift = null)
    {
        if (events.Count > 0 && !events.ContainsKey(AttendanceEvent.ShiftIn))
        {
            return "ต้องมีเวลาเข้างานก่อน";
        }

        if (events.Keys.Any(action => !AttendanceLifecycle.Order.Contains(action)))
        {
            return OrderMessage(shift);
        }

        var times = AttendanceLifecycle.Order.Where(events.ContainsKey).Select(action => events[action]).ToList();
        return times.Zip(times.Skip(1)).Any(pair => pair.Second <= pair.First) ? OrderMessage(shift) : null;
    }

    public static IReadOnlyList<AttendanceEvent> MissingAtShiftEnd(IReadOnlyCollection<AttendanceEvent> recorded)
    {
        if (!recorded.Contains(AttendanceEvent.ShiftIn))
        {
            return [];
        }

        var missing = new List<AttendanceEvent>();
        if (recorded.Contains(AttendanceEvent.Break1Out) && !recorded.Contains(AttendanceEvent.Break1In))
        {
            missing.Add(AttendanceEvent.Break1In);
        }

        if (recorded.Contains(AttendanceEvent.Break2Out) && !recorded.Contains(AttendanceEvent.Break2In))
        {
            missing.Add(AttendanceEvent.Break2In);
        }

        if (!recorded.Contains(AttendanceEvent.ShiftOut))
        {
            missing.Add(AttendanceEvent.ShiftOut);
        }

        return missing;
    }
}
