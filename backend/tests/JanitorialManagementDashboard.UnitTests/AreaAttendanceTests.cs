using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests;

/// <summary>Wireframe D2 and facility-0050: an Area's state is whether someone is there, from its regular Cleaner's events.</summary>
public class AreaAttendanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_cleaner_who_came_decides_the_state_whatever_the_cover(bool hasCover)
    {
        Assert.Equal(AreaAttendanceState.Working, AreaAttendance.StateOf([AttendanceEvent.ShiftIn], hasCover));
        Assert.Equal(AreaAttendanceState.OnBreak, AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut], hasCover));
        Assert.Equal(
            AreaAttendanceState.Working,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn], hasCover));
        Assert.Equal(
            AreaAttendanceState.Done,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.ShiftOut], hasCover));
    }

    [Fact]
    public void Nobody_came_is_empty_unless_someone_covers()
    {
        Assert.Equal(AreaAttendanceState.Empty, AreaAttendance.StateOf([], hasCover: false));
        Assert.Equal(AreaAttendanceState.Covered, AreaAttendance.StateOf([], hasCover: true));
    }

    // ---- Task 9: the state word is read against the slot's lifecycle (spec §8) ----

    [Fact]
    public void A_numbered_break_reads_as_on_break_until_its_own_return()
    {
        var numbered = AttendanceLifecycleVersion.Numbered6;
        Assert.Equal(
            AreaAttendanceState.OnBreak,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out], false, numbered));
        Assert.Equal(
            AreaAttendanceState.Working,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In], false, numbered));
        Assert.Equal(
            AreaAttendanceState.OnBreak,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break2Out], false, numbered));
        // A first break that came back does not hide a second one that went out, and each return answers only its own pair.
        Assert.Equal(
            AreaAttendanceState.OnBreak,
            AreaAttendance.StateOf(
                [AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In, AttendanceEvent.Break2Out],
                false, numbered));
        // ... and a return answers only its own pair: the first break coming back does not close the second one.
        Assert.Equal(
            AreaAttendanceState.OnBreak,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1In, AttendanceEvent.Break2Out], false, numbered));
        // Neither does a second-break return close a first break that is still open.
        Assert.Equal(
            AreaAttendanceState.OnBreak,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break2In], false, numbered));
    }

    [Fact]
    public void A_legacy_slot_keeps_the_legacy_four_action_answer()
    {
        var legacy = AttendanceLifecycleVersion.Legacy4;
        Assert.Equal(AreaAttendanceState.OnBreak, AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut], true, legacy));
        Assert.Equal(
            AreaAttendanceState.Working,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn], true, legacy));
        // A numbered event is not part of the legacy four, so it never moves a legacy row: byte-identical to the old rule.
        Assert.Equal(
            AreaAttendanceState.Working,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out], true, legacy));
        // The overload without a version is exactly the Legacy4 answer, so every existing caller is unchanged.
        foreach (var events in new[]
        {
            new[] { AttendanceEvent.ShiftIn },
            new[] { AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut },
            new[] { AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut },
            Array.Empty<AttendanceEvent>(),
        })
        {
            Assert.Equal(AreaAttendance.StateOf(events, true, legacy), AreaAttendance.StateOf(events, true));
            Assert.Equal(AreaAttendance.StateOf(events, false, legacy), AreaAttendance.StateOf(events, false));
        }
    }
}
