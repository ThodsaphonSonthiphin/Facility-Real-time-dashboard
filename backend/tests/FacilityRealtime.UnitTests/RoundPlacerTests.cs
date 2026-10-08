using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0047 rule 3, using its own example: rounds 07:00-09:00 and 16:00-18:00 on 8 Oct.</summary>
public class RoundPlacerTests
{
    private static readonly ShiftSlot Day8 = new(new DateOnly(2026, 10, 8), Shift.Day);

    private static readonly IReadOnlyList<RoundWindow> Rounds =
    [
        RoundWindow.For(1, new TimeOnly(7, 0), new TimeOnly(9, 0), Day8),
        RoundWindow.For(2, new TimeOnly(16, 0), new TimeOnly(18, 0), Day8),
    ];

    private static DateTime Thai(int day, int hour, int minute, int second = 0) =>
        new DateTime(2026, 10, day, hour, minute, second, DateTimeKind.Utc).AddHours(-7);

    private static RoundPlacement Place(
        DateTime at,
        SubmissionFact[]? submissions = null,
        InspectionFact[]? inspections = null,
        IReadOnlyList<RoundWindow>? rounds = null) =>
        RoundPlacer.Place(
            rounds ?? Rounds,
            submissions ?? Array.Empty<SubmissionFact>(),
            inspections ?? Array.Empty<InspectionFact>(),
            at);

    [Fact]
    public void Inside_the_round_is_on_time()
    {
        var placed = Place(Thai(8, 8, 10));

        Assert.Equal(Placement.OnTime, placed.Placement);
        Assert.Equal(1, placed.Round?.Id);
        Assert.Null(placed.LateMinutes);
    }

    [Theory]
    [InlineData(7, 0)]
    [InlineData(9, 0)]
    public void Both_ends_of_the_round_are_on_time(int hour, int minute)
    {
        var placed = Place(Thai(8, hour, minute));

        Assert.Equal(Placement.OnTime, placed.Placement);
        Assert.Equal(1, placed.Round?.Id);
    }

    [Fact]
    public void Before_any_round_has_started_is_off_round()
    {
        var placed = Place(Thai(8, 6, 59));

        Assert.Equal(Placement.OffRound, placed.Placement);
        Assert.Null(placed.Round);
    }

    [Fact]
    public void After_an_empty_round_is_late_for_that_round()
    {
        var placed = Place(Thai(8, 9, 40));

        Assert.Equal(Placement.Late, placed.Placement);
        Assert.Equal(1, placed.Round?.Id);
        Assert.Equal(40, placed.LateMinutes);
    }

    [Fact]
    public void Part_of_a_minute_late_counts_as_one_minute()
    {
        var placed = Place(Thai(8, 9, 0, second: 30));

        Assert.Equal(Placement.Late, placed.Placement);
        Assert.Equal(1, placed.LateMinutes);
    }

    [Fact]
    public void After_a_submitted_round_is_off_round()
    {
        var placed = Place(Thai(8, 15, 30), [new SubmissionFact(10, 1, Thai(8, 8, 10))]);

        Assert.Equal(Placement.OffRound, placed.Placement);
        Assert.Null(placed.Round);
    }

    [Fact]
    public void After_a_round_sent_back_for_rework_is_its_rework()
    {
        var placed = Place(
            Thai(8, 11, 0),
            [new SubmissionFact(10, 1, Thai(8, 8, 10))],
            [new InspectionFact(10, InspectionResult.Rework, Thai(8, 10, 0))]);

        Assert.Equal(Placement.Rework, placed.Placement);
        Assert.Equal(1, placed.Round?.Id);
    }

    [Fact]
    public void Rework_already_resubmitted_makes_the_next_one_off_round()
    {
        var placed = Place(
            Thai(8, 11, 0),
            [new SubmissionFact(10, 1, Thai(8, 8, 10)), new SubmissionFact(11, 1, Thai(8, 10, 30))],
            [new InspectionFact(10, InspectionResult.Rework, Thai(8, 10, 0))]);

        Assert.Equal(Placement.OffRound, placed.Placement);
    }

    [Fact]
    public void Inside_the_round_after_a_rework_inspection_is_rework()
    {
        var placed = Place(
            Thai(8, 8, 30),
            [new SubmissionFact(10, 1, Thai(8, 8, 10))],
            [new InspectionFact(10, InspectionResult.Rework, Thai(8, 8, 20))]);

        Assert.Equal(Placement.Rework, placed.Placement);
        Assert.Equal(1, placed.Round?.Id);
    }

    [Fact]
    public void Inside_the_round_after_a_pass_is_on_time()
    {
        var placed = Place(
            Thai(8, 8, 30),
            [new SubmissionFact(10, 1, Thai(8, 8, 10))],
            [new InspectionFact(10, InspectionResult.Passed, Thai(8, 8, 20))]);

        Assert.Equal(Placement.OnTime, placed.Placement);
    }

    [Fact]
    public void Latest_started_round_is_the_one_that_counts()
    {
        var placed = Place(Thai(8, 16, 30));

        Assert.Equal(Placement.OnTime, placed.Placement);
        Assert.Equal(2, placed.Round?.Id);
    }

    [Fact]
    public void Night_round_across_midnight_ends_on_the_next_day()
    {
        var night8 = new ShiftSlot(new DateOnly(2026, 10, 8), Shift.Night);
        IReadOnlyList<RoundWindow> rounds = [RoundWindow.For(3, new TimeOnly(23, 0), new TimeOnly(1, 0), night8)];

        var onTime = Place(Thai(9, 0, 30), rounds: rounds);
        var late = Place(Thai(9, 1, 20), rounds: rounds);

        Assert.Equal(Placement.OnTime, onTime.Placement);
        Assert.Equal(Placement.Late, late.Placement);
        Assert.Equal(20, late.LateMinutes);
    }
}
