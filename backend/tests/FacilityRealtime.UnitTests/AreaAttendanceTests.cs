using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

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
}
