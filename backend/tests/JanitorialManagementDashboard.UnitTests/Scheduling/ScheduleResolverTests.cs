using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// Spec §5, §7 and §10 (ADR facility-0189, acceptance A03, A04, A17, A27): the resolver turns dated facts into one day
/// per field — a dated explicit choice first, then the applicable baseline version, then unset — and never guesses an
/// answer the records do not hold.
/// </summary>
public class ScheduleResolverTests
{
    private static readonly DateOnly HistoryStarts = new(2026, 11, 17);
    private static readonly DateOnly Monday = new(2026, 11, 16);
    private static readonly DateOnly Tuesday = new(2026, 11, 17);
    private static readonly DateOnly Wednesday = new(2026, 11, 18);

    [Fact]
    public void Unavailable_history_is_not_a_zero_or_a_guessed_assignment()
    {
        var input = ScheduleInput.Empty(new DateOnly(2026, 11, 17));
        var day = ScheduleResolver.Resolve(input, new DateOnly(2026, 11, 16));
        Assert.Equal("unavailable", day.Availability);
        Assert.Null(day.Area.Value);
    }

    [Fact]
    public void A_missing_choice_after_the_history_start_is_unset_not_unavailable()
    {
        // A27: "unavailable" is history coverage, "unset" is a choice nobody has made yet. The two never collapse.
        var input = ScheduleInput.Empty(HistoryStarts);

        var day = ScheduleResolver.Resolve(input, HistoryStarts);

        Assert.Equal("unset", day.Availability);
        Assert.Null(day.Area.Value);
        Assert.NotEqual("unavailable", day.Availability);
    }

    [Fact]
    public void A_new_Wednesday_Area_leaves_the_earlier_days_unchanged()
    {
        // A03: a later baseline version starts on Wednesday; Monday and Tuesday keep the version they had.
        var input = new ScheduleInput(7, Monday,
            [.. Version(from: Monday, until: Wednesday, areaId: 11, breakGroup: 1, revision: 1, firstCellId: 100),
             .. Version(from: Wednesday, until: null, areaId: 12, breakGroup: 2, revision: 2, firstCellId: 200)],
            new HashSet<DateOnly>(), [], [], []);

        var tuesday = ScheduleResolver.Resolve(input, Tuesday);
        var wednesday = ScheduleResolver.Resolve(input, Wednesday);

        Assert.Equal(11, tuesday.Area.Value);
        Assert.Equal("baseline", tuesday.Area.SourceKind);
        Assert.Equal("1", tuesday.Area.Revision);
        Assert.Equal(12, wednesday.Area.Value);
        Assert.Equal("baseline", wednesday.Area.SourceKind);
        Assert.Equal("2", wednesday.Area.Revision);
        // The two days come from different stored versions, not from one rewritten row.
        Assert.NotEqual(tuesday.Area.SourceId, wednesday.Area.SourceId);
    }

    [Fact]
    public void A_dated_break_group_override_survives_a_baseline_change()
    {
        // ADR facility-0189: the still-applicable dated override wins over the new default and is never deleted.
        var input = new ScheduleInput(7, Monday,
            [.. Version(from: Monday, until: Wednesday, areaId: 11, breakGroup: 1, revision: 1, firstCellId: 100),
             .. Version(from: Wednesday, until: null, areaId: 12, breakGroup: 2, revision: 2, firstCellId: 200)],
            new HashSet<DateOnly>(),
            [new DatedFact(500, Wednesday, Shift.Day, AreaId: null, BreakGroup: 9, Revision: "3")],
            [], []);

        var wednesday = ScheduleResolver.Resolve(input, Wednesday);
        var tuesday = ScheduleResolver.Resolve(input, Tuesday);

        Assert.Equal(9, wednesday.BreakGroup.Value);
        Assert.Equal("dated", wednesday.BreakGroup.SourceKind);
        Assert.Equal(500, wednesday.BreakGroup.SourceId);
        Assert.Equal(12, wednesday.Area.Value);          // the new version still supplies the Area
        Assert.Equal(1, tuesday.BreakGroup.Value);       // the earlier day is untouched
        Assert.Empty(wednesday.Problems);                // kept, not turned into a silent removal
    }

    [Fact]
    public void A_dated_Area_override_wins_on_its_own_date_only()
    {
        var input = new ScheduleInput(7, Monday,
            Version(from: Monday, until: null, areaId: 11, breakGroup: 1, revision: 1, firstCellId: 100),
            new HashSet<DateOnly>(),
            [new DatedFact(500, Wednesday, Shift.Day, AreaId: 12, BreakGroup: null, Revision: "1")],
            [], []);

        Assert.Equal(12, ScheduleResolver.Resolve(input, Wednesday).Area.Value);
        Assert.Equal("dated", ScheduleResolver.Resolve(input, Wednesday).Area.SourceKind);
        Assert.Equal(11, ScheduleResolver.Resolve(input, Tuesday).Area.Value);
    }

    [Fact]
    public void An_explicitly_unchosen_baseline_cell_is_unset()
    {
        // facility-0173: a cell that was chosen as "nothing" is unset, never a working day and never a day off.
        var input = new ScheduleInput(7, Monday,
            [new BaselineFact(700, Monday, null, Wednesday.DayOfWeek, AreaId: null, BreakGroup: null, Revision: "1")],
            new HashSet<DateOnly>(), [], [], []);

        var day = ScheduleResolver.Resolve(input, Wednesday);

        Assert.Null(day.Area.Value);
        Assert.Equal("unset", day.Area.SourceKind);
        Assert.True(day.IsUnset);
        Assert.False(day.IsDayOff);
        Assert.Equal("unset", day.Availability);
    }

    [Fact]
    public void A_saved_week_without_a_choice_is_confirmed_but_a_missing_header_is_unset()
    {
        // ADR facility-0190: an empty saved week is a confirmed "no day off"; a missing header is nobody having been asked.
        var version = Version(from: Monday, until: null, areaId: 11, breakGroup: null, revision: 1, firstCellId: 100);
        var saved = new ScheduleInput(7, Monday, version, new HashSet<DateOnly>(), [], [], []);
        var missing = new ScheduleInput(7, Monday, version, DaysOff: null, [], [], []);

        Assert.False(ScheduleResolver.Resolve(saved, Wednesday).IsUnset);
        Assert.False(ScheduleResolver.Resolve(saved, Wednesday).IsDayOff);
        Assert.True(ScheduleResolver.Resolve(missing, Wednesday).IsUnset);
        Assert.False(ScheduleResolver.Resolve(missing, Wednesday).IsDayOff);
        // A04: an unset week never becomes a working day or a day off by guessing; the Area stays the one on record.
        Assert.Equal(11, ScheduleResolver.Resolve(missing, Wednesday).Area.Value);
    }

    [Fact]
    public void A_historical_date_before_the_baseline_is_unavailable_not_the_current_Area()
    {
        // A27: before history coverage the week is unavailable; it is not a zero Area and not today's Area.
        var input = new ScheduleInput(7, Tuesday,
            Version(from: Tuesday, until: null, areaId: 11, breakGroup: null, revision: 1, firstCellId: 100),
            new HashSet<DateOnly>(), [], [], []);

        var before = ScheduleResolver.Resolve(input, Monday);
        var covered = ScheduleResolver.Resolve(input, Tuesday);

        Assert.Equal("unavailable", before.Availability);
        Assert.Null(before.Area.Value);
        Assert.Equal("unavailable", before.Area.SourceKind);
        Assert.False(before.IsUnset);
        Assert.Equal(11, covered.Area.Value);
    }

    [Fact]
    public void A_day_off_keeps_the_underlying_eligible_Area()
    {
        // Spec §7: record-first check-in still needs the Area, so IsDayOff never nulls it.
        var input = new ScheduleInput(7, Monday,
            Version(from: Monday, until: null, areaId: 11, breakGroup: 1, revision: 1, firstCellId: 100),
            new HashSet<DateOnly> { Wednesday }, [], [], []);

        var day = ScheduleResolver.Resolve(input, Wednesday);

        Assert.True(day.IsDayOff);
        Assert.Equal("day-off", day.Availability);
        Assert.Equal(11, day.Area.Value);
        Assert.Equal(1, day.BreakGroup.Value);
    }

    [Fact]
    public void A_leave_keeps_the_underlying_eligible_Area()
    {
        // Spec §7: an agreed full-shift leave is an absence state, not a closed Area.
        var input = new ScheduleInput(7, Monday,
            Version(from: Monday, until: null, areaId: 11, breakGroup: 1, revision: 1, firstCellId: 100),
            new HashSet<DateOnly>(), [],
            [new LeaveFact(900, Wednesday, Shift.Day,
                Start: new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero),
                End: new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero),
                Cancelled: false, Revision: "1")],
            []);

        var day = ScheduleResolver.Resolve(input, Wednesday);

        Assert.True(day.IsLeave);
        Assert.Equal("leave", day.Availability);
        Assert.Equal(11, day.Area.Value);
    }

    [Fact]
    public void A_partial_leave_marks_the_leave_and_leaves_the_day_available()
    {
        // Spec §7: a half day off keeps the plan and the eligibility; only the agreed interval is recorded.
        var input = new ScheduleInput(7, Monday,
            Version(from: Monday, until: null, areaId: 11, breakGroup: null, revision: 1, firstCellId: 100),
            new HashSet<DateOnly>(), [],
            [new LeaveFact(901, Wednesday, Shift.Day,
                Start: new DateTimeOffset(2026, 11, 18, 6, 0, 0, TimeSpan.Zero),
                End: new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero),
                Cancelled: false, Revision: "1")],
            []);

        var day = ScheduleResolver.Resolve(input, Wednesday);

        Assert.True(day.IsLeave);
        Assert.Equal("available", day.Availability);
        Assert.Equal(11, day.Area.Value);
    }

    [Fact]
    public void A_cancelled_leave_is_history_and_not_an_absence_state()
    {
        var input = new ScheduleInput(7, Monday,
            Version(from: Monday, until: null, areaId: 11, breakGroup: null, revision: 1, firstCellId: 100),
            new HashSet<DateOnly>(), [],
            [new LeaveFact(902, Wednesday, Shift.Day,
                Start: new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero),
                End: new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero),
                Cancelled: true, Revision: "2")],
            []);

        var day = ScheduleResolver.Resolve(input, Wednesday);

        Assert.False(day.IsLeave);
        Assert.Equal("available", day.Availability);
    }

    [Fact]
    public void A_dated_break_group_without_an_Area_is_a_reported_conflict()
    {
        // Spec §5: the applicable dated group survives, but its clash with the day's state is shown for someone to fix.
        var input = new ScheduleInput(7, Monday,
            [new BaselineFact(700, Monday, null, Wednesday.DayOfWeek, AreaId: null, BreakGroup: null, Revision: "1")],
            new HashSet<DateOnly>(),
            [new DatedFact(500, Wednesday, Shift.Day, AreaId: null, BreakGroup: 9, Revision: "1")],
            [], []);

        var day = ScheduleResolver.Resolve(input, Wednesday);

        Assert.Equal(9, day.BreakGroup.Value);
        Assert.Equal("dated", day.BreakGroup.SourceKind);
        Assert.Null(day.Area.Value);
        var problem = Assert.Single(day.Problems);
        Assert.Equal("dated-group-without-area", problem.Code);
        Assert.Equal(Wednesday, problem.Date);
    }

    /// <summary>One baseline version: the same Area and group on every weekday, with its own cell ids and revision.</summary>
    private static List<BaselineFact> Version(
        DateOnly from, DateOnly? until, int? areaId, int? breakGroup, long revision, int firstCellId)
    {
        var cells = new List<BaselineFact>();
        var id = firstCellId;
        foreach (var weekday in Enum.GetValues<DayOfWeek>())
        {
            cells.Add(new BaselineFact(id++, from, until, weekday, areaId, breakGroup, revision.ToString(), false));
        }

        return cells;
    }
}
