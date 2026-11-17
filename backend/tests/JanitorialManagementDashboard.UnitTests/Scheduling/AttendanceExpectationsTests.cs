using System.Globalization;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// Spec §8 with ADR facility-0148 (the plan's timetable) and facility-0150 (the shift hours): the expected instants of the
/// actions whose time the plan knows, as UTC. They are expectation times only — ADR facility-0149 keeps recorded scans
/// from ever being refused for falling outside them — and no date is built into the product: every instant is derived from
/// the slot's own Thai date through <see cref="ShiftCalendar"/> and <see cref="ThaiTime"/>.
/// </summary>
public class AttendanceExpectationsTests
{
    private static readonly ShiftSlot DayShift = new(new DateOnly(2026, 10, 8), Shift.Day);
    private static readonly ShiftSlot NightShift = new(new DateOnly(2026, 10, 8), Shift.Night);

    /// <summary>Asia/Bangkok is UTC+7 all year, so 07:00 Thai on 8 October is 00:00Z that day.</summary>
    private static DateTime Utc(string iso) => DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    [Fact]
    public void A_day_shift_group_one_break_is_ten_to_eleven_in_the_morning()
    {
        var expected = AttendanceExpectations.ForSlot(DayShift, 1);

        Assert.Equal(Utc("2026-10-08T00:00:00Z"), expected[AttendanceEvent.ShiftIn]);
        Assert.Equal(Utc("2026-10-08T03:00:00Z"), expected[AttendanceEvent.Break1Out]);
        Assert.Equal(Utc("2026-10-08T04:00:00Z"), expected[AttendanceEvent.Break1In]);
        Assert.Equal(Utc("2026-10-08T09:00:00Z"), expected[AttendanceEvent.Break2Out]);
        Assert.Equal(Utc("2026-10-08T10:00:00Z"), expected[AttendanceEvent.Break2In]);
        Assert.Equal(Utc("2026-10-08T12:00:00Z"), expected[AttendanceEvent.ShiftOut]);
    }

    [Fact]
    public void A_day_shift_group_two_break_is_eleven_to_twelve()
    {
        var expected = AttendanceExpectations.ForSlot(DayShift, 2);

        Assert.Equal(Utc("2026-10-08T04:00:00Z"), expected[AttendanceEvent.Break1Out]);
        Assert.Equal(Utc("2026-10-08T05:00:00Z"), expected[AttendanceEvent.Break1In]);
        // Break 2 is shared by the whole shift, so it does not move with the first-break group.
        Assert.Equal(Utc("2026-10-08T09:00:00Z"), expected[AttendanceEvent.Break2Out]);
        Assert.Equal(Utc("2026-10-08T10:00:00Z"), expected[AttendanceEvent.Break2In]);
    }

    [Fact]
    public void A_night_shift_group_one_break_is_ten_to_eleven_in_the_evening()
    {
        var expected = AttendanceExpectations.ForSlot(NightShift, 1);

        Assert.Equal(Utc("2026-10-08T12:00:00Z"), expected[AttendanceEvent.ShiftIn]);
        Assert.Equal(Utc("2026-10-08T15:00:00Z"), expected[AttendanceEvent.Break1Out]);
        Assert.Equal(Utc("2026-10-08T16:00:00Z"), expected[AttendanceEvent.Break1In]);
    }

    [Fact]
    public void A_night_shift_group_two_break_ends_at_midnight_on_the_next_calendar_date()
    {
        var expected = AttendanceExpectations.ForSlot(NightShift, 2);

        Assert.Equal(Utc("2026-10-08T16:00:00Z"), expected[AttendanceEvent.Break1Out]);
        Assert.Equal(Utc("2026-10-08T17:00:00Z"), expected[AttendanceEvent.Break1In]);
    }

    [Fact]
    public void The_shared_second_break_of_a_night_shift_spills_past_midnight()
    {
        var expected = AttendanceExpectations.ForSlot(NightShift, 1);

        Assert.Equal(Utc("2026-10-08T21:00:00Z"), expected[AttendanceEvent.Break2Out]);
        Assert.Equal(Utc("2026-10-08T22:00:00Z"), expected[AttendanceEvent.Break2In]);
    }

    [Fact]
    public void A_night_shift_ends_at_seven_the_next_morning()
    {
        Assert.Equal(Utc("2026-10-09T00:00:00Z"), AttendanceExpectations.ForSlot(NightShift, 1)[AttendanceEvent.ShiftOut]);
        Assert.Equal(Utc("2026-10-08T12:00:00Z"), AttendanceExpectations.ForSlot(DayShift, 1)[AttendanceEvent.ShiftOut]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(3)]
    public void An_unknown_group_omits_the_first_break_instants_but_keeps_the_rest(int? breakGroup)
    {
        var expected = AttendanceExpectations.ForSlot(DayShift, breakGroup);

        Assert.False(expected.ContainsKey(AttendanceEvent.Break1Out));
        Assert.False(expected.ContainsKey(AttendanceEvent.Break1In));
        Assert.Equal(
            new[] { AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut, AttendanceEvent.Break2Out, AttendanceEvent.Break2In },
            expected.Keys.OrderBy(action => (int)action).ToArray());
    }

    [Fact]
    public void Every_instant_comes_from_the_slots_own_date_rather_than_a_constant()
    {
        var other = new ShiftSlot(new DateOnly(2027, 3, 1), Shift.Day);

        var expected = AttendanceExpectations.ForSlot(other, 2);

        Assert.Equal(Utc("2027-03-01T00:00:00Z"), expected[AttendanceEvent.ShiftIn]);
        Assert.Equal(Utc("2027-03-01T12:00:00Z"), expected[AttendanceEvent.ShiftOut]);
        Assert.DoesNotContain(expected.Values, instant => instant.Year == 2026);
    }

    [Fact]
    public void Every_shift_and_group_expects_the_same_four_or_six_actions()
    {
        Assert.Equal(6, AttendanceExpectations.ForSlot(DayShift, 1).Count);
        Assert.Equal(6, AttendanceExpectations.ForSlot(NightShift, 2).Count);
        Assert.Equal(4, AttendanceExpectations.ForSlot(DayShift, null).Count);
    }
}
