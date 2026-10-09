using System.Globalization;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Rounds;

/// <summary>A Round Window as the Admin typed it, already parsed. Thai wall-clock.</summary>
public sealed record RoundWindowInput(Shift Shift, TimeOnly Start, TimeOnly End);

/// <summary>
/// ADR facility-0047 rule 1 and its 2026-10-09 amendment: what the Admin may save as a point's Round Windows.
/// Times are measured from the shift's start, so a night window may cross midnight; a window must end before the
/// shift does, because a late or rework submission after the shift change already belongs to the next shift.
/// </summary>
public static class RoundWindowRules
{
    private const int ShiftMinutes = 12 * 60;
    private const int DayMinutes = 24 * 60;

    /// <summary>"07:00" only: two-digit 24-hour time, whole minutes.</summary>
    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Minutes from the shift's start (07:00 or 19:00) to <paramref name="time"/>, wrapping past midnight: 0–1439.</summary>
    public static int MinutesIntoShift(Shift shift, TimeOnly time)
    {
        var shiftStart = shift == Shift.Day ? ShiftCalendar.DayStart : ShiftCalendar.NightStart;
        var minutes = time.Hour * 60 + time.Minute - (shiftStart.Hour * 60 + shiftStart.Minute);
        return (minutes + DayMinutes) % DayMinutes;
    }

    /// <summary>How the audit log shows a window, e.g. "DAY 07:00-09:00".</summary>
    public static string Describe(RoundWindowInput window) =>
        $"{(window.Shift == Shift.Day ? "DAY" : "NIGHT")} {Format(window.Start)}-{Format(window.End)}";

    /// <summary>Null when every window may be saved; otherwise the Thai message for the first problem found.</summary>
    public static string? Validate(IReadOnlyList<RoundWindowInput> windows, ShiftPattern pattern)
    {
        foreach (var window in windows)
        {
            if (window.Shift == Shift.Night && pattern == ShiftPattern.DayOnly)
            {
                return "Area นี้ทำเฉพาะกะเช้า ใส่ช่วงรอบกะดึกไม่ได้";
            }

            var start = MinutesIntoShift(window.Shift, window.Start);
            var end = MinutesIntoShift(window.Shift, window.End);
            if (start >= ShiftMinutes || end > ShiftMinutes)
            {
                return $"ช่วง {Text(window)} ต้องอยู่ในกะ{ShiftText(window.Shift)}";
            }

            if (end == ShiftMinutes)
            {
                return $"ช่วง {Text(window)} จบตรง {Format(window.End)} ซึ่งเป็นเวลาเปลี่ยนกะไม่ได้ ให้จบก่อนเวลานั้น";
            }

            if (end <= start)
            {
                return $"ช่วง {Text(window)}: เวลาสิ้นสุดต้องหลังเวลาเริ่ม";
            }
        }

        foreach (var shift in windows.GroupBy(w => w.Shift))
        {
            var ordered = shift.OrderBy(w => MinutesIntoShift(w.Shift, w.Start)).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (MinutesIntoShift(ordered[i].Shift, ordered[i].Start) < MinutesIntoShift(ordered[i - 1].Shift, ordered[i - 1].End))
                {
                    return $"ช่วง {Text(ordered[i - 1])} กับ {Text(ordered[i])} ซ้อนกัน";
                }
            }
        }

        return null;
    }

    private static string Text(RoundWindowInput window) => $"{Format(window.Start)}–{Format(window.End)}";

    private static string ShiftText(Shift shift) => shift == Shift.Day ? "เช้า (07:00–19:00)" : "ดึก (19:00–07:00)";
}
