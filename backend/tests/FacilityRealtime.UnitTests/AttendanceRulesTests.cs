using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0031 order; facility-0051: a forgotten Break-In stays empty, so Shift-Out may follow Break-Out.</summary>
public class AttendanceRulesTests
{
    private static IReadOnlyList<AttendanceEvent> Next(params AttendanceEvent[] recorded) => AttendanceRules.NextAllowed(recorded);

    [Fact]
    public void Nothing_recorded_allows_only_shift_in() =>
        Assert.Equal(new[] { AttendanceEvent.ShiftIn }, Next());

    [Fact]
    public void After_shift_in_take_a_break_or_leave() =>
        Assert.Equal(new[] { AttendanceEvent.BreakOut, AttendanceEvent.ShiftOut }, Next(AttendanceEvent.ShiftIn));

    [Fact]
    public void On_a_break_come_back_or_leave() =>
        Assert.Equal(new[] { AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut }, Next(AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut));

    [Fact]
    public void After_the_break_only_leave() =>
        Assert.Equal(new[] { AttendanceEvent.ShiftOut }, Next(AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn));

    [Fact]
    public void After_shift_out_nothing() =>
        Assert.Empty(Next(AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut));

    [Fact]
    public void On_duty_from_shift_in_until_shift_out_breaks_included()
    {
        Assert.False(AttendanceRules.IsOnDuty(Array.Empty<AttendanceEvent>()));
        Assert.True(AttendanceRules.IsOnDuty(new[] { AttendanceEvent.ShiftIn }));
        Assert.True(AttendanceRules.IsOnDuty(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut }));
        Assert.False(AttendanceRules.IsOnDuty(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut }));
    }
}
