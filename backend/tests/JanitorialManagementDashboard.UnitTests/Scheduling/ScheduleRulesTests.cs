using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>facility-0193 and facility-0210: copying previous-week days off fills only unset target choices by stable ID.</summary>
public class ScheduleRulesTests
{
    [Fact]
    public void Copy_preserves_local_values_and_only_fills_target_members()
    {
        var target = new Dictionary<int, DateOnly?> { [1] = new(2026, 11, 25), [2] = null, [3] = null };
        var source = new Dictionary<int, DateOnly?> { [1] = new(2026, 11, 16), [2] = new(2026, 11, 16), [9] = new(2026, 11, 18) };
        var actual = ScheduleRules.FillUnset(target, source, new DateOnly(2026, 11, 23));
        Assert.Equal(new DateOnly(2026, 11, 25), actual[1]);
        Assert.Equal(new DateOnly(2026, 11, 23), actual[2]);
        Assert.Null(actual[3]);
        Assert.False(actual.ContainsKey(9));
    }

    [Fact]
    public void An_empty_source_leaves_unset_target_choices_unset()
    {
        var target = new Dictionary<int, DateOnly?> { [1] = null, [2] = new(2026, 11, 25) };

        var actual = ScheduleRules.FillUnset(target, new Dictionary<int, DateOnly?>(), new DateOnly(2026, 11, 23));

        Assert.Null(actual[1]);
        Assert.Equal(new DateOnly(2026, 11, 25), actual[2]);
        Assert.Equal(2, actual.Count);
    }

    [Fact]
    public void A_person_who_left_the_target_roster_is_not_imported()
    {
        var target = new Dictionary<int, DateOnly?> { [1] = null, [2] = null };
        var source = new Dictionary<int, DateOnly?> { [1] = new(2026, 11, 16), [9] = new(2026, 11, 18) };

        var actual = ScheduleRules.FillUnset(target, source, new DateOnly(2026, 11, 23));

        Assert.Equal([1, 2], actual.Keys.Order());
        Assert.Equal(new DateOnly(2026, 11, 23), actual[1]);
        Assert.Null(actual[2]);
    }

    [Theory]
    [InlineData(23, 23)] // Monday is its own week start
    [InlineData(25, 23)] // a midweek date belongs to the Monday before it
    [InlineData(29, 23)] // Sunday closes the ISO week
    public void Week_start_is_the_iso_monday(int day, int expectedDay) =>
        Assert.Equal(new DateOnly(2026, 11, expectedDay), ScheduleRules.WeekStart(new DateOnly(2026, 11, day)));

    [Theory]
    [InlineData(16, 23)] // Monday maps to the target Monday
    [InlineData(18, 25)] // Wednesday maps to the target Wednesday
    [InlineData(22, 29)] // Sunday maps to the target Sunday
    public void A_source_date_maps_to_the_same_weekday_in_the_target_week(int sourceDay, int expectedDay)
    {
        var target = new Dictionary<int, DateOnly?> { [1] = null };
        var source = new Dictionary<int, DateOnly?> { [1] = new(2026, 11, sourceDay) };

        var actual = ScheduleRules.FillUnset(target, source, new DateOnly(2026, 11, 23));

        Assert.Equal(new DateOnly(2026, 11, expectedDay), actual[1]);
    }

    [Fact]
    public void A_target_week_date_snaps_to_its_monday_before_mapping()
    {
        var target = new Dictionary<int, DateOnly?> { [1] = null };
        var source = new Dictionary<int, DateOnly?> { [1] = new(2026, 11, 17) };

        var actual = ScheduleRules.FillUnset(target, source, new DateOnly(2026, 11, 25));

        Assert.Equal(new DateOnly(2026, 11, 24), actual[1]);
    }

    [Fact]
    public void An_ongoing_night_shift_uses_its_start_date_not_the_wall_clock_monday()
    {
        // facility-0183: the Sunday 19:00 shift still running at 02:00 Monday keeps the week of its start date.
        var slot = new ScheduleSlot(new DateOnly(2026, 11, 22), Shift.Night);
        var target = new Dictionary<int, DateOnly?> { [1] = null };
        var source = new Dictionary<int, DateOnly?> { [1] = new(2026, 11, 17) };

        var week = ScheduleRules.WeekStart(slot.ShiftDate);
        var actual = ScheduleRules.FillUnset(target, source, week);

        Assert.Equal(new DateOnly(2026, 11, 16), week);
        Assert.Equal(new DateOnly(2026, 11, 17), actual[1]);
    }
}
