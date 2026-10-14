using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;

namespace FacilityRealtime.Application.Export;

/// <summary>The sets the Admin ticks on wireframe D8. The inspection set comes with the Supervisor plan.</summary>
public enum ExportSet
{
    Attendance,
    Submissions,
}

/// <summary>facility-0053: Excel by date range instead of report pages.</summary>
public static class ExportRules
{
    /// <summary>The longest range one file covers, in shift dates.</summary>
    public const int MaxDays = 31;

    /// <summary>
    /// facility-0047 rule 5 "ไม่ได้ทำ": the rounds of one point in one shift with no OnTime, Late or Rework submission.
    /// Only once the attendance window has closed (<paramref name="closesMinutesAfterShift"/> after the shift ends, inclusive),
    /// because a Late Submission is still accepted until then. A window created after its round started did not exist
    /// for that round. Returned in the order the shift meets them.
    /// </summary>
    public static IReadOnlyList<RoundWindow> MissedRounds(
        IEnumerable<(RoundWindow Window, DateTime CreatedAtUtc)> windows, IReadOnlySet<int> doneWindowIds, ShiftSlot slot, DateTime nowUtc,
        int closesMinutesAfterShift) =>
        ShiftCalendar.EndUtc(slot).AddMinutes(closesMinutesAfterShift) >= nowUtc
            ? []
            : windows
                .Where(w => w.CreatedAtUtc <= w.Window.StartUtc && !doneWindowIds.Contains(w.Window.Id))
                .Select(w => w.Window)
                .OrderBy(w => w.StartUtc)
                .ToList();
}
