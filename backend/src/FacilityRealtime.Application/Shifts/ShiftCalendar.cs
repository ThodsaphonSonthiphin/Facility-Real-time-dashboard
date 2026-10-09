using FacilityRealtime.Application.Common;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Shifts;

/// <summary>One shift: the date it started on and Day or Night. 19:00 on 7 Oct to 07:00 on 8 Oct is (2026-10-07, Night).</summary>
public sealed record ShiftSlot(DateOnly ShiftDate, Shift Shift);

/// <summary>The two fixed shifts of ADR facility-0040.</summary>
public static class ShiftCalendar
{
    public static readonly TimeOnly DayStart = new(7, 0);
    public static readonly TimeOnly NightStart = new(19, 0);

    public static ShiftSlot SlotAt(DateTime utc)
    {
        var thai = ThaiTime.FromUtc(utc);
        var date = DateOnly.FromDateTime(thai);
        var time = TimeOnly.FromDateTime(thai);

        if (time < DayStart)
        {
            return new ShiftSlot(date.AddDays(-1), Shift.Night);
        }

        return new ShiftSlot(date, time < NightStart ? Shift.Day : Shift.Night);
    }

    /// <summary>When the shift starts, as UTC: 07:00 (Day) or 19:00 (Night) Thai on ShiftDate.</summary>
    public static DateTime StartUtc(ShiftSlot slot) => ToUtc(slot, slot.Shift == Shift.Day ? DayStart : NightStart);

    /// <summary>Every shift is 12 hours long.</summary>
    public static DateTime EndUtc(ShiftSlot slot) => StartUtc(slot).AddHours(12);

    /// <summary>
    /// A Thai wall-clock time inside the shift, as UTC. In a night shift every time before 19:00
    /// is after midnight, so it falls on the day after ShiftDate.
    /// </summary>
    public static DateTime ToUtc(ShiftSlot slot, TimeOnly thaiTime)
    {
        var date = slot.Shift == Shift.Night && thaiTime < NightStart ? slot.ShiftDate.AddDays(1) : slot.ShiftDate;
        return ThaiTime.ToUtc(date.ToDateTime(thaiTime));
    }
}
