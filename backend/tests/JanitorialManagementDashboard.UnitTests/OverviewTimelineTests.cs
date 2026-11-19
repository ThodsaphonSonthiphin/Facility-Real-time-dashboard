using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests;

public class OverviewTimelineTests
{
    private static readonly ShiftSlot Day = new(new(2026, 11, 16), Shift.Day);
    private static readonly ShiftSlot Night = Day with { Shift = Shift.Night };
    private static readonly DateTime Before = ShiftCalendar.StartUtc(Day).AddDays(-1);
    private static DateTime At(ShiftSlot slot, int hour) => ShiftCalendar.ToUtc(slot, new(hour, 0));
    private static TargetWindow Window(int id, int hour, Shift shift = Shift.Day) =>
        new(id, 3, shift, new(hour, 0), new(hour, 30), Before);
    private static TargetImage Image(params TargetWindow[] windows) => new(1, new(60, 60),
        [new(1, "B1", "West", true, Before), new(2, "B2", "East", true, Before)],
        [new(10, 1, "A1", "Hall", true, ShiftPattern.DayAndNight, Before)],
        [new(3, 10, "P1", "Door", 1, true, Before)], windows);
    private static TargetTimeline Replay(ShiftSlot slot, DateTime now, params TargetRevision[] history) =>
        OverviewTimeline.Build(slot, OverviewTime.Evaluate(slot, now, history), history);

    [Fact]
    public void Afternoon_supersedes_morning_only_at_start_and_remains_current_through_close()
    {
        var image = Image(Window(4, 8));
        var history = new TargetRevision[] { new(1, Before, image), new(2, At(Day, 10),
            image with { Windows = [Window(4, 8), Window(5, 13)] }) };
        var morning = Assert.Single(Replay(Day, At(Day, 12), history).Rounds);
        Assert.Null(morning.FinalizedAtUtc);
        var atStart = Replay(Day, At(Day, 13), history);
        Assert.Equal([4, 5], atStart.Rounds.Select(r => r.OriginId));
        Assert.Equal(("superseded", At(Day, 13)), (atStart.Rounds[0].FinalizedReason, atStart.Rounds[0].FinalizedAtUtc));
        Assert.Null(atStart.Rounds[1].FinalizedAtUtc);

        var close = At(Day, 20);
        var closed = Replay(Day, close.AddDays(1), history);
        Assert.Equal(2L, closed.CurrentRevisionId);
        Assert.Equal(2, closed.Rounds.Count);
        Assert.Equal(("slotClosed", close), (closed.Rounds[1].FinalizedReason, closed.Rounds[1].FinalizedAtUtc));
        Assert.True(closed.HistoryComplete);
    }

    [Fact]
    public void Day_only_removes_night_work_but_keeps_its_original_history()
    {
        var image = Image(Window(4, 21, Shift.Night), Window(5, 2, Shift.Night));
        var changed = image with { Areas = [image.Areas[0] with { ShiftPattern = ShiftPattern.DayOnly }], Windows = [] };
        var result = Replay(Night, At(Night, 5), new(1, Before, image), new(2, At(Night, 3), changed));
        Assert.Equal([4, 5], result.Rounds.Select(r => r.OriginId));
        Assert.Equal(At(Night, 2), result.Rounds[0].FinalizedAtUtc);
        Assert.Equal(At(Night, 3), result.Rounds[1].FinalizedAtUtc);
        Assert.Equal("configurationChanged", result.Rounds[1].FinalizedReason);
        Assert.All(result.Rounds, r => Assert.Equal("West", r.BuildingName));
    }

    [Fact]
    public void Recreated_window_at_same_times_has_new_origin_and_is_never_backdated()
    {
        var result = Replay(Day, At(Day, 15), new(1, Before, Image(Window(4, 8))),
            new(2, At(Day, 12), Image(Window(5, 8))));
        Assert.Equal([4, 5], result.Rounds.Select(r => r.OriginId));
        Assert.Equal(At(Day, 12), result.Rounds[0].FinalizedAtUtc);
        Assert.Equal(At(Day, 12), result.Rounds[1].ActiveFromUtc);
    }

    [Fact]
    public void Editing_and_restoring_times_preserves_three_distinct_activation_keys()
    {
        var image = Image(Window(4, 8));
        var result = Replay(Day, At(Day, 15), new(1, Before, image),
            new(2, At(Day, 10), Image(Window(4, 9))), new(3, At(Day, 12), image));
        Assert.Equal([1L, 2L, 3L], result.Rounds.Select(r => r.ActivationRevisionId));
        var keys = result.Rounds.Select(r => OverviewTimeline.RoundKey(Day, r)).ToArray();
        Assert.Equal(3, keys.Distinct().Count());
        Assert.Equal(result.Rounds, keys.Select(k => OverviewTimeline.FindRound(result, k)));
        var first = keys[0];
        Assert.Equal("2026-11-16:Day:3:4:1:0800-0830", first);
        foreach (var invalid in new[] { first.Replace("11-16", "11-15"), first.Replace(":Day:", ":Night:"),
            first.Replace("0800", "800"), first.Replace(":4:", ":04:"), first.Replace(":1:", ":999:") })
            Assert.Null(OverviewTimeline.FindRound(result, invalid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rename_or_building_move_reuses_current_work_and_preserves_finalized_ownership(bool move)
    {
        var image = Image(Window(4, 8), Window(5, 11));
        var changed = image with { Areas = [image.Areas[0] with { Name = "Main hall", BuildingId = move ? 2 : 1 }] };
        var before = Replay(Day, At(Day, 12), new TargetRevision(1, Before, image));
        var submittedRoundKey = OverviewTimeline.RoundKey(Day, before.Rounds[1]);
        var result = Replay(Day, At(Day, 15), new(1, Before, image), new(2, At(Day, 13), changed));
        Assert.Equal(2, result.Rounds.Count);
        var morning = result.Rounds[0];
        Assert.Equal(("Hall", "West"), (morning.AreaName, morning.BuildingName));
        var current = Assert.IsType<RoundObligation>(OverviewTimeline.FindRound(result, submittedRoundKey));
        Assert.Equal(("Main hall", move ? "East" : "West"), (current.AreaName, current.BuildingName));
        Assert.Equal(At(Day, 11), current.ActiveFromUtc);
        Assert.Null(current.FinalizedAtUtc);
    }

    [Theory]
    [InlineData("window")]
    [InlineData("point")]
    [InlineData("area")]
    [InlineData("building")]
    public void Latest_creation_boundary_controls_activation_within_a_revision(string element)
    {
        var noon = At(Day, 12);
        var image = Image(Window(4, 8));
        var delayed = element switch
        {
            "window" => image with { Windows = [image.Windows[0] with { CreatedAt = noon }] },
            "point" => image with { Points = [image.Points[0] with { CreatedAt = noon }] },
            "area" => image with { Areas = [image.Areas[0] with { CreatedAt = noon }] },
            _ => image with { Buildings = [image.Buildings[0] with { CreatedAt = noon }] },
        };
        var round = Assert.Single(Replay(Day, At(Day, 16), new TargetRevision(1, Before, delayed)).Rounds);
        Assert.Equal(noon, round.ActiveFromUtc);
    }

    [Fact]
    public void Midshift_baseline_supports_current_targets_but_not_whole_shift_history()
    {
        var at = At(Day, 11);
        var result = Replay(Day, At(Day, 14), new TargetRevision(1, at, Image(Window(4, 8))));
        Assert.True(result.CurrentComplete);
        Assert.False(result.HistoryComplete);
        Assert.Equal(at, Assert.Single(result.Rounds).ActiveFromUtc);
    }

    [Fact]
    public void Only_last_revision_at_an_instant_can_activate_work_and_future_images_are_ignored()
    {
        var change = At(Day, 9);
        var result = Replay(Day, At(Day, 15), new(3, change, Image(Window(6, 8))),
            new(1, Before, Image(Window(4, 8))), new(2, change, Image(Window(5, 8))),
            new(4, At(Day, 16), Image(Window(7, 8))));
        Assert.Equal([4, 6], result.Rounds.Select(r => r.OriginId));
        Assert.Equal(3L, result.CurrentRevisionId);
    }

    [Fact]
    public void Future_shift_has_no_obligations_even_when_its_first_window_is_known()
    {
        Assert.Empty(Replay(Day, At(Day, 6), new TargetRevision(1, Before, Image(Window(4, 7)))).Rounds);
    }

    [Theory]
    [InlineData("nightPattern")]
    [InlineData("building")]
    [InlineData("area")]
    [InlineData("point")]
    public void Losing_eligibility_finalizes_work_and_restoration_creates_a_new_activation(string element)
    {
        var image = Image(Window(4, 21, Shift.Night));
        var disabled = element switch
        {
            "nightPattern" => image with { Areas = [image.Areas[0] with { ShiftPattern = ShiftPattern.DayOnly }] },
            "building" => image with { Buildings = [image.Buildings[0] with { IsActive = false }] },
            "area" => image with { Areas = [image.Areas[0] with { IsActive = false }] },
            _ => image with { Points = [image.Points[0] with { IsActive = false }] },
        };
        var result = Replay(Night, At(Night, 3), new(1, Before, image), new(2, At(Night, 23), disabled),
            new(3, At(Night, 1), image));
        Assert.Collection(result.Rounds,
            prior => Assert.Equal(("configurationChanged", At(Night, 23)), (prior.FinalizedReason, prior.FinalizedAtUtc)),
            current => Assert.Equal((3L, At(Night, 1)), (current.ActivationRevisionId, current.ActiveFromUtc)));
    }

    [Fact]
    public void Receiving_period_round_stays_with_its_slot_and_later_edits_cannot_rewrite_closed_work()
    {
        var image = Image(Window(4, 8));
        var result = Replay(Day, At(Day, 20).AddDays(1), new(1, Before, image),
            new(2, At(Day, 19).AddMinutes(30), Image(Window(5, 10))), new(3, At(Day, 21), image));
        Assert.Equal(2L, result.CurrentRevisionId);
        var last = Assert.Single(result.Rounds, r => r.FinalizedReason == "slotClosed");
        Assert.Equal(At(Day, 19).AddMinutes(30), last.ActiveFromUtc);
        Assert.Equal(At(Day, 20), last.FinalizedAtUtc);
        Assert.Same(last, OverviewTimeline.FindRound(result, OverviewTimeline.RoundKey(Day, last)));
        Assert.Null(OverviewTimeline.FindRound(result, OverviewTimeline.RoundKey(Night, last)));
    }

    [Fact]
    public void Missing_policy_at_end_prevents_known_later_targets_being_marked_complete()
    {
        var result = Replay(Day, At(Day, 20).AddDays(1),
            new TargetRevision(1, At(Day, 20), Image(Window(4, 8))));
        Assert.False(result.CurrentComplete);
        Assert.False(result.HistoryComplete);
    }
}
