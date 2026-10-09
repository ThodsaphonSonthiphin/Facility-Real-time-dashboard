using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0069: attendance counts for the person's own shift from 60 minutes before it to 60 minutes after it.</summary>
public class AttendanceCalendarTests
{
    private static readonly AttendanceSettings Settings = new() { OpensMinutesBeforeShift = 60, ClosesMinutesAfterShift = 60 };

    private static DateTime Thai(int day, int hour, int minute) =>
        new DateTime(2026, 10, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    private static ShiftSlot? Slot(int? day, Shift shift) => day is int d ? new ShiftSlot(new DateOnly(2026, 10, d), shift) : null;

    [Theory]
    [InlineData(8, 5, 59, null)]
    [InlineData(8, 6, 0, 8)]
    [InlineData(8, 12, 0, 8)]
    [InlineData(8, 20, 0, 8)]
    [InlineData(8, 20, 1, null)]
    public void Day_shift_window_opens_an_hour_early_and_closes_an_hour_late(int day, int hour, int minute, int? shiftDay)
    {
        Assert.Equal(Slot(shiftDay, Shift.Day), AttendanceCalendar.SlotFor(Shift.Day, Thai(day, hour, minute), Settings));
    }

    [Theory]
    [InlineData(8, 17, 59, null)]
    [InlineData(8, 18, 0, 8)]
    [InlineData(9, 2, 0, 8)]
    [InlineData(9, 8, 0, 8)]
    [InlineData(9, 8, 1, null)]
    [InlineData(9, 18, 0, 9)]
    public void Night_shift_window_belongs_to_the_date_the_shift_started(int day, int hour, int minute, int? shiftDay)
    {
        Assert.Equal(Slot(shiftDay, Shift.Night), AttendanceCalendar.SlotFor(Shift.Night, Thai(day, hour, minute), Settings));
    }

    [Fact]
    public void Zero_minutes_means_inside_the_shift_only()
    {
        var strict = new AttendanceSettings { OpensMinutesBeforeShift = 0, ClosesMinutesAfterShift = 0 };

        Assert.Null(AttendanceCalendar.SlotFor(Shift.Day, Thai(8, 6, 59), strict));
        Assert.Equal(Slot(8, Shift.Day), AttendanceCalendar.SlotFor(Shift.Day, Thai(8, 7, 0), strict));
    }

    [Fact]
    public void Opening_and_closing_minutes_come_from_settings()
    {
        var asymmetric = new AttendanceSettings { OpensMinutesBeforeShift = 30, ClosesMinutesAfterShift = 90 };

        Assert.Null(AttendanceCalendar.SlotFor(Shift.Day, Thai(8, 6, 29), asymmetric));
        Assert.Equal(Slot(8, Shift.Day), AttendanceCalendar.SlotFor(Shift.Day, Thai(8, 6, 30), asymmetric));
        Assert.Equal(Slot(8, Shift.Day), AttendanceCalendar.SlotFor(Shift.Day, Thai(8, 20, 30), asymmetric));
        Assert.Null(AttendanceCalendar.SlotFor(Shift.Day, Thai(8, 20, 31), asymmetric));
    }
}
