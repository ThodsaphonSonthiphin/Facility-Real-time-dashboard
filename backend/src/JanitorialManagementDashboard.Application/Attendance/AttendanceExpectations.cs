using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Attendance;

/// <summary>
/// Spec §8 with ADR facility-0148 (the timetable the plan holds) and ADR facility-0150 (the shift hours): the expected
/// instant of every action whose time the plan knows, as UTC, for one slot. Break 1 is staggered in two Supervisor-assigned
/// groups; break 2 is taken by the whole shift together before OT, so it does not move with the group. These are expectation
/// times only: ADR facility-0149 keeps a recorded scan from ever being refused for falling outside them, so this table is
/// read to say what is missing or covered by leave — never to reject an actual time.
/// <para>
/// No date is built in. Every instant is derived from the slot's own Thai date through <see cref="ShiftCalendar"/>, which
/// already knows that a night shift's times before 19:00 (a 04:00 break, a 07:00 end) fall on the day after
/// <see cref="ShiftSlot.ShiftDate"/>.
/// </para>
/// </summary>
public static class AttendanceExpectations
{
    private static readonly TimeOnly NightSecondBreakOut = new(4, 0);
    private static readonly TimeOnly NightSecondBreakIn = new(5, 0);
    private static readonly TimeOnly DaySecondBreakOut = new(16, 0);
    private static readonly TimeOnly DaySecondBreakIn = new(17, 0);

    /// <summary>ADR facility-0148: the first break's two staggered hours, per shift and group, as Thai wall-clock.</summary>
    private static readonly TimeOnly[] DayFirstBreakOut = [new(10, 0), new(11, 0)];
    private static readonly TimeOnly[] DayFirstBreakIn = [new(11, 0), new(12, 0)];

    /// <summary>Night group 2 returns at midnight, which is the next calendar date of the slot.</summary>
    private static readonly TimeOnly[] NightFirstBreakOut = [new(22, 0), new(23, 0)];
    private static readonly TimeOnly[] NightFirstBreakIn = [new(23, 0), new(0, 0)];

    /// <summary>
    /// The expected instants of <paramref name="slot"/>. Shift-In and Shift-Out (ADR facility-0150) and both ends of the
    /// shared second break are always answered; the first break's two instants are answered only when the person's group is
    /// known (1 or 2), because a relief with no assignment and a person with no group yet have no staggered hour to plan
    /// against — the action is then simply "no known instant" rather than a guessed one.
    /// </summary>
    public static IReadOnlyDictionary<AttendanceEvent, DateTime> ForSlot(ShiftSlot slot, int? breakGroup)
    {
        var day = slot.Shift == Shift.Day;
        var expected = new Dictionary<AttendanceEvent, DateTime>
        {
            [AttendanceEvent.ShiftIn] = ShiftCalendar.StartUtc(slot),
            [AttendanceEvent.ShiftOut] = ShiftCalendar.EndUtc(slot),
            [AttendanceEvent.Break2Out] = ShiftCalendar.ToUtc(slot, day ? DaySecondBreakOut : NightSecondBreakOut),
            [AttendanceEvent.Break2In] = ShiftCalendar.ToUtc(slot, day ? DaySecondBreakIn : NightSecondBreakIn),
        };

        if (breakGroup is 1 or 2)
        {
            var index = breakGroup.Value - 1;
            expected[AttendanceEvent.Break1Out] =
                ShiftCalendar.ToUtc(slot, (day ? DayFirstBreakOut : NightFirstBreakOut)[index]);
            expected[AttendanceEvent.Break1In] =
                ShiftCalendar.ToUtc(slot, (day ? DayFirstBreakIn : NightFirstBreakIn)[index]);
        }

        return expected;
    }
}
