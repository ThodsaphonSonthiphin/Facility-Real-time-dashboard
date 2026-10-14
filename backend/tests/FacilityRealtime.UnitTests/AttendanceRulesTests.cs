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

    private static readonly DateTime Start = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

    private static Dictionary<AttendanceEvent, DateTime> Times(params (AttendanceEvent Event, int Minute)[] events) =>
        events.ToDictionary(e => e.Event, e => Start.AddMinutes(e.Minute));

    [Fact]
    public void A_full_shift_in_order_is_valid() =>
        Assert.Null(AttendanceRules.CheckTimes(Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.BreakOut, 300), (AttendanceEvent.BreakIn, 360), (AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void A_forgotten_break_in_may_stay_empty() =>
        Assert.Null(AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 0), (AttendanceEvent.BreakOut, 300), (AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void No_events_is_valid() => Assert.Null(AttendanceRules.CheckTimes(Times()));

    [Fact]
    public void Every_event_needs_a_shift_in() =>
        Assert.Contains("เข้างานก่อน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void A_break_in_needs_a_break_out() =>
        Assert.Contains("ออกพักก่อน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 0), (AttendanceEvent.BreakIn, 360))));

    [Fact]
    public void Times_must_run_in_order() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 60), (AttendanceEvent.BreakOut, 30))));

    [Fact]
    public void Two_events_at_the_same_minute_are_out_of_order() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 0), (AttendanceEvent.ShiftOut, 0))));

    [Fact]
    public void Shift_out_must_be_last() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes(Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.BreakOut, 300), (AttendanceEvent.BreakIn, 360), (AttendanceEvent.ShiftOut, 330))));

    [Fact]
    public void Missing_events_once_the_shift_is_over()
    {
        Assert.Empty(AttendanceRules.MissingAtShiftEnd([]));
        Assert.Equal(new[] { AttendanceEvent.ShiftOut }, AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn]));
        Assert.Equal(
            new[] { AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut },
            AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut]));
        Assert.Equal(
            new[] { AttendanceEvent.BreakIn },
            AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.ShiftOut]));
        Assert.Empty(AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut]));
        Assert.Empty(AttendanceRules.MissingAtShiftEnd(
            [AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut]));
    }
}
