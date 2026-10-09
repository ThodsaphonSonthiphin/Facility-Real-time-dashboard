using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0040: Day 07:00-19:00, Night 19:00-07:00 Asia/Bangkok; a night shift is named by the date it started.</summary>
public class ShiftCalendarTests
{
    /// <summary>Thai wall-clock on <paramref name="day"/> October 2026, as UTC (Bangkok is UTC+7 all year).</summary>
    private static DateTime Thai(int day, int hour, int minute) =>
        new DateTime(2026, 10, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    [Theory]
    [InlineData(8, 7, 0, 8, Shift.Day)]
    [InlineData(8, 18, 59, 8, Shift.Day)]
    [InlineData(8, 19, 0, 8, Shift.Night)]
    [InlineData(8, 23, 59, 8, Shift.Night)]
    [InlineData(9, 0, 0, 8, Shift.Night)]
    [InlineData(9, 6, 59, 8, Shift.Night)]
    public void Slot_is_the_shift_running_at_that_moment(int day, int hour, int minute, int shiftDay, Shift shift)
    {
        var slot = ShiftCalendar.SlotAt(Thai(day, hour, minute));

        Assert.Equal(new ShiftSlot(new DateOnly(2026, 10, shiftDay), shift), slot);
    }

    [Theory]
    [InlineData(Shift.Day, 7, 0, 8, 7, 0)]
    [InlineData(Shift.Day, 16, 0, 8, 16, 0)]
    [InlineData(Shift.Day, 19, 0, 8, 19, 0)]
    [InlineData(Shift.Night, 19, 0, 8, 19, 0)]
    [InlineData(Shift.Night, 20, 0, 8, 20, 0)]
    [InlineData(Shift.Night, 3, 0, 9, 3, 0)]
    [InlineData(Shift.Night, 7, 0, 9, 7, 0)]
    public void Time_inside_a_shift_becomes_the_right_instant(
        Shift shift, int hour, int minute, int expectedDay, int expectedHour, int expectedMinute)
    {
        var slot = new ShiftSlot(new DateOnly(2026, 10, 8), shift);

        var utc = ShiftCalendar.ToUtc(slot, new TimeOnly(hour, minute));

        Assert.Equal(Thai(expectedDay, expectedHour, expectedMinute), utc);
    }
}
