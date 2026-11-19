using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests;
public class OverviewTimeTests
{
    private static TargetRevision Policy(DateTime at, int closes, long id = 1) =>
        new(id, at, new(1, new(60, closes), [], [], [], []));

    [Fact]
    public void Day_is_still_receiving_at_inclusive_20_00_boundary()
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Day);
        var end = ShiftCalendar.EndUtc(slot);
        var history = new[] { Policy(ShiftCalendar.StartUtc(slot).AddDays(-1), 60) };
        Assert.Equal("receiving", OverviewTime.Evaluate(slot, end.AddMinutes(60), history).Phase);
        var closed = OverviewTime.Evaluate(slot, end.AddMinutes(60).AddTicks(10), history);
        Assert.Equal("closed", closed.Phase);
        Assert.Equal(end.AddMinutes(60), closed.EvaluationUtc);
    }

    [Fact]
    public void New_policy_does_not_reopen_a_closed_slot()
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Day);
        var end = ShiftCalendar.EndUtc(slot);
        var history = new[] {
            Policy(end.AddDays(-1), 60), Policy(end.AddMinutes(90), 180, 2)
        };
        var result = OverviewTime.Evaluate(slot, end.AddHours(3), history);
        Assert.Equal(end.AddHours(1), result.CloseUtc);
        Assert.Equal("closed", result.Phase);
    }

    [Fact]
    public void Early_morning_defaults_to_the_previous_night_shift()
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Night);
        var atThree = ShiftCalendar.ToUtc(slot, new(3, 0));
        Assert.Equal(slot, ShiftCalendar.SlotAt(atThree));
        var history = new[] { Policy(ShiftCalendar.StartUtc(slot), 60) };
        Assert.Equal("running", OverviewTime.Evaluate(slot, atThree, history).Phase);
        var end = ShiftCalendar.EndUtc(slot);
        Assert.Equal("running", OverviewTime.Evaluate(slot, end.AddTicks(-1), history).Phase);
        Assert.Equal("receiving", OverviewTime.Evaluate(slot, end, history).Phase);
        Assert.Equal("receiving", OverviewTime.Evaluate(slot, ShiftCalendar.ToUtc(slot, new(8, 0)), history).Phase);
    }

    [Fact]
    public void Upcoming_same_day_shift_is_not_started()
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Night);
        var now = ShiftCalendar.StartUtc(slot).AddHours(-3);
        var result = OverviewTime.Evaluate(slot, now, [Policy(now.AddDays(-1), 60)]);
        Assert.Equal("notStarted", result.Phase);
        Assert.Equal(now, result.EvaluationUtc);
    }

    [Theory]
    [InlineData(30, 120, 120)]
    [InlineData(15, 30, 30)]
    [InlineData(45, 15, 45)]
    [InlineData(60, 120, 120)]
    public void Recalculate_while_receiving_including_at_the_existing_deadline(
        int changeMinute, int newMinutes, int expectedMinute)
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Day);
        var end = ShiftCalendar.EndUtc(slot);
        var history = new[] { Policy(end.AddDays(-1), 60), Policy(end.AddMinutes(changeMinute), newMinutes, 2) };
        var close = end.AddMinutes(expectedMinute);
        var result = OverviewTime.Evaluate(slot, close, history);
        Assert.Equal(new OverviewMoment("receiving", close, close, true), result);
        Assert.Equal(new OverviewMoment("closed", close, close, true),
            OverviewTime.Evaluate(slot, close.AddTicks(1), history));
    }

    [Fact]
    public void Baseline_during_the_shift_establishes_the_close()
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Day);
        var midShift = ShiftCalendar.StartUtc(slot).AddHours(3);
        var result = OverviewTime.Evaluate(slot, midShift, [Policy(midShift, 45)]);
        Assert.Equal(new OverviewMoment("running", midShift, ShiftCalendar.EndUtc(slot).AddMinutes(45), true), result);
    }

    [Fact]
    public void Baseline_after_an_old_shift_end_cannot_prove_its_close()
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Day);
        var end = ShiftCalendar.EndUtc(slot);
        var result = OverviewTime.Evaluate(slot, end.AddDays(1), [Policy(end.AddMinutes(1), 180)]);
        Assert.Equal(new OverviewMoment("receiving", end.AddDays(1), null, false), result);
    }

    [Fact]
    public void Same_timestamp_policy_changes_use_the_highest_revision_id()
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Day);
        var end = ShiftCalendar.EndUtc(slot);
        var change = end.AddMinutes(45);
        var result = OverviewTime.Evaluate(slot, end.AddDays(1),
            [Policy(change, 120, 3), Policy(end.AddDays(-1), 60), Policy(change, 10, 2)]);
        Assert.Equal(end.AddHours(2), result.CloseUtc);
    }
}
