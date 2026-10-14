using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Attendance;

/// <summary>facility-0069: attendance and Scan Records count for the person's own shift, not the shift the clock is in.</summary>
public static class AttendanceCalendar
{
    /// <summary>
    /// The slot of <paramref name="regularShift"/> whose window — from OpensMinutesBeforeShift before its start
    /// to ClosesMinutesAfterShift after its end, both inclusive — contains <paramref name="nowUtc"/>; null outside every window.
    /// </summary>
    public static ShiftSlot? SlotFor(Shift regularShift, DateTime nowUtc, AttendanceSettings settings)
    {
        var thaiDate = DateOnly.FromDateTime(ThaiTime.FromUtc(nowUtc));
        foreach (var date in new[] { thaiDate.AddDays(-1), thaiDate, thaiDate.AddDays(1) })
        {
            var slot = new ShiftSlot(date, regularShift);
            var opens = ShiftCalendar.StartUtc(slot).AddMinutes(-settings.OpensMinutesBeforeShift);
            var closes = ShiftCalendar.EndUtc(slot).AddMinutes(settings.ClosesMinutesAfterShift);
            if (nowUtc >= opens && nowUtc <= closes)
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>
    /// facility-0051: the moment an Admin means by a Thai wall-clock <paramref name="thaiTime"/> typed for <paramref name="slot"/>,
    /// as UTC: the one inside the slot's attendance window, or null when there is none. A Day slot's 06:30 is before
    /// the shift starts; a Night slot's 07:20 is the next morning. The window opens at most 6 hours early (Program.cs),
    /// so the moment is on ShiftDate or the day after.
    /// </summary>
    public static DateTime? PlaceTime(ShiftSlot slot, TimeOnly thaiTime, AttendanceSettings settings)
    {
        var opens = ShiftCalendar.StartUtc(slot).AddMinutes(-settings.OpensMinutesBeforeShift);
        var closes = ShiftCalendar.EndUtc(slot).AddMinutes(settings.ClosesMinutesAfterShift);
        foreach (var date in new[] { slot.ShiftDate, slot.ShiftDate.AddDays(1) })
        {
            var utc = ThaiTime.ToUtc(date.ToDateTime(thaiTime));
            if (utc >= opens && utc <= closes)
            {
                return utc;
            }
        }

        return null;
    }
}
