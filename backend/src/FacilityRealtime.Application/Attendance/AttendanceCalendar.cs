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
}
