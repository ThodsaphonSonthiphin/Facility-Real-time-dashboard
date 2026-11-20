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
        Assert.Equal(AreaAttendanceState.OnBreak, AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out], hasCover));
        Assert.Equal(
            AreaAttendanceState.Working,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In], hasCover));
        Assert.Equal(
            AreaAttendanceState.Done,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.ShiftOut], hasCover));
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
        Assert.Equal(
            AreaAttendanceState.OnBreak,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out], false));
        Assert.Equal(
            AreaAttendanceState.Working,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In], false));
        Assert.Equal(
            AreaAttendanceState.OnBreak,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break2Out], false));
        // A first break that came back does not hide a second one that went out, and each return answers only its own pair.
        Assert.Equal(
            AreaAttendanceState.OnBreak,
            AreaAttendance.StateOf(
                [AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In, AttendanceEvent.Break2Out],
                false));
        // ... and a return answers only its own pair: the first break coming back does not close the second one.
        Assert.Equal(
            AreaAttendanceState.OnBreak,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1In, AttendanceEvent.Break2Out], false));
        // The current state uses the latest break action, even if an earlier return was skipped.
        Assert.Equal(
            AreaAttendanceState.Working,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break2In], false));
    }

}
