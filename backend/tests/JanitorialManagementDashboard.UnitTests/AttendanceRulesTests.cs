using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests;

/// <summary>ADR facility-0031 order; facility-0051: a forgotten Break-In stays empty, so Shift-Out may follow Break-Out.</summary>
public class AttendanceRulesTests
{

    [Fact]
    public void On_duty_from_shift_in_until_shift_out_breaks_included()
    {
        Assert.False(AttendanceRules.IsOnDuty(Array.Empty<AttendanceEvent>()));
        Assert.True(AttendanceRules.IsOnDuty(new[] { AttendanceEvent.ShiftIn }));
        Assert.True(AttendanceRules.IsOnDuty(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out }));
        Assert.False(AttendanceRules.IsOnDuty(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut }));
    }

    private static readonly DateTime Start = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

    private static Dictionary<AttendanceEvent, DateTime> Times(params (AttendanceEvent Event, int Minute)[] events) =>
        events.ToDictionary(e => e.Event, e => Start.AddMinutes(e.Minute));

    [Fact]
    public void A_full_shift_in_order_is_valid() =>
        Assert.Null(AttendanceRules.CheckTimes(Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.Break1Out, 300), (AttendanceEvent.Break1In, 360), (AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void A_forgotten_break_in_may_stay_empty() =>
        Assert.Null(AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 0), (AttendanceEvent.Break1Out, 300), (AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void No_events_is_valid() => Assert.Null(AttendanceRules.CheckTimes(Times()));

    [Fact]
    public void Every_event_needs_a_shift_in() =>
        Assert.Contains("เข้างานก่อน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void Times_must_run_in_order() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 60), (AttendanceEvent.Break1Out, 30))));

    [Fact]
    public void Two_events_at_the_same_minute_are_out_of_order() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 0), (AttendanceEvent.ShiftOut, 0))));

    [Fact]
    public void Shift_out_must_be_last() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes(Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.Break1Out, 300), (AttendanceEvent.Break1In, 360), (AttendanceEvent.ShiftOut, 330))));

    // ---- the same order check for a known lifecycle version ----

    [Fact]
    public void A_numbered_slot_in_full_lifecycle_order_is_valid() =>
        Assert.Null(AttendanceRules.CheckTimes( Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.Break1Out, 120), (AttendanceEvent.Break1In, 180),
            (AttendanceEvent.Break2Out, 240), (AttendanceEvent.Break2In, 300), (AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void An_earlier_ordinal_event_recorded_after_a_later_ordinal_one_is_rejected() =>
        // Break-1-In sits at ordinal 2 but at minute 300, after Break-2-Out at ordinal 3 and minute 240: out of order.
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes( Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.Break1Out, 120), (AttendanceEvent.Break1In, 300),
            (AttendanceEvent.Break2Out, 240), (AttendanceEvent.Break2In, 360), (AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void A_numbered_shift_out_before_the_last_break_is_rejected() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes( Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.Break2In, 300), (AttendanceEvent.ShiftOut, 240))));

    [Fact]
    public void A_numbered_break_needs_a_shift_in() =>
        Assert.Contains("เข้างานก่อน", AttendanceRules.CheckTimes(
             Times((AttendanceEvent.Break1Out, 120))));

    [Fact]
    public void A_forgotten_numbered_break_is_an_allowed_explicit_gap() =>
        // Spec §8: an explicit gap is exactly how a forgotten break is represented, so the missing pair time is never required.
        Assert.Null(AttendanceRules.CheckTimes( Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.Break1In, 180), (AttendanceEvent.Break2Out, 240),
            (AttendanceEvent.Break2In, 300), (AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void No_numbered_events_is_valid() =>
        Assert.Null(AttendanceRules.CheckTimes( Times()));

    [Fact]
    public void Missing_events_once_the_shift_is_over()
    {
        Assert.Empty(AttendanceRules.MissingAtShiftEnd([]));
        Assert.Equal(new[] { AttendanceEvent.ShiftOut }, AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn]));
        Assert.Equal(
            new[] { AttendanceEvent.Break1In, AttendanceEvent.ShiftOut },
            AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out]));
        Assert.Equal(
            new[] { AttendanceEvent.Break1In },
            AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.ShiftOut]));
        Assert.Empty(AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut]));
        Assert.Empty(AttendanceRules.MissingAtShiftEnd(
            [AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In, AttendanceEvent.ShiftOut]));
    }
}
