using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>
/// Plan 6 decision 15. Building 1 has points 10 (Area works nights), 11 (day-only Area) and 12 (deactivated); building 2
/// has point 20. Supervisor 1 = building 1 Day, 2 = building 1 Night, 3 = building 2 Day.
/// </summary>
public class InspectionSummaryTests
{
    private static readonly ShiftSlot Day8 = new(new DateOnly(2026, 10, 8), Shift.Day);
    private static readonly ShiftSlot Night8 = new(new DateOnly(2026, 10, 8), Shift.Night);

    private static readonly SummaryPoint[] Points =
    [
        new(10, 1, true, true),
        new(11, 1, true, false),
        new(12, 1, false, true),
        new(20, 2, true, true),
    ];

    private static readonly SummarySupervisor[] Supervisors = [new(1, 1, Shift.Day), new(2, 1, Shift.Night), new(3, 2, Shift.Day)];

    private static RoundSubmission Sent(long id, int point, ShiftSlot slot) =>
        new(id, point, slot, new TimeOnly(slot.Shift == Shift.Day ? 8 : 20, 0), new TimeOnly(slot.Shift == Shift.Day ? 10 : 22, 0),
            ShiftCalendar.StartUtc(slot).AddHours(1).AddMinutes(10));

    private static SummaryInspection Inspected(long scanId, int point, int supervisor, ShiftSlot slot, InspectionResult result, bool? withinRadius = true) =>
        new(scanId, point, supervisor, slot, result, withinRadius);

    private static FinishedRound Round(int point, ShiftSlot slot, RoundOutcome outcome) =>
        new(point, slot, new TimeOnly(slot.Shift == Shift.Day ? 8 : 20, 0), new TimeOnly(slot.Shift == Shift.Day ? 10 : 22, 0), outcome);

    [Fact]
    public void Each_supervisor_gets_their_beat_and_their_own_inspections()
    {
        var tallies = InspectionSummary.Tally(
            Supervisors,
            Points,
            [Sent(1, 10, Day8), Sent(2, 11, Day8), Sent(3, 20, Day8), Sent(4, 10, Night8)],
            [
                Inspected(1, 10, 1, Day8, InspectionResult.Passed),
                Inspected(3, 20, 1, Day8, InspectionResult.Rework, withinRadius: false), // supervisor 1 covering building 2
                Inspected(4, 10, 2, Night8, InspectionResult.Passed),
            ],
            [Round(10, Day8, RoundOutcome.Passed), Round(11, Day8, RoundOutcome.NotInspected), Round(20, Day8, RoundOutcome.NotReworked), Round(10, Night8, RoundOutcome.Passed)]);

        AssertMetrics(new SupervisorTally(1, 1, 2, 2, 1, 1, 2, 1, 1), tallies[0]);
        AssertMetrics(new SupervisorTally(2, 1, 1, 1, 1, 0, 1, 0, 0), tallies[1]);
        AssertMetrics(new SupervisorTally(3, 0, 1, 1, 1, 0, 0, 0, 0), tallies[2]);
        Assert.Equal([11], tallies[0].UninspectedPointIds);
        Assert.Empty(tallies[1].UninspectedPointIds);
        Assert.Equal([20], tallies[2].UninspectedPointIds);
    }

    [Fact]
    public void A_deactivated_point_is_neither_counted_nor_inspected_in_the_beat()
    {
        var tallies = InspectionSummary.Tally(
            Supervisors, Points, [Sent(1, 12, Day8)], [Inspected(1, 12, 1, Day8, InspectionResult.Passed)], []);

        Assert.Equal(0, tallies[0].PointsInspected);
        Assert.Equal(2, tallies[0].PointsTotal);
        Assert.Equal(1, tallies[0].Inspections); // still one of their inspections
        Assert.Equal(1, tallies[0].Submitted);   // its history still belongs to the beat
    }

    [Fact]
    public void An_inspection_at_a_point_of_another_building_is_not_in_the_beat()
    {
        var tallies = InspectionSummary.Tally(Supervisors, [Points[0]], [], [Inspected(9, 99, 1, Day8, InspectionResult.Passed)], []);

        Assert.Equal(0, tallies[0].PointsInspected);
        Assert.Equal(1, tallies[0].Inspections);
    }

    [Fact]
    public void Points_inspected_counts_points_not_inspections()
    {
        var day9 = new ShiftSlot(new DateOnly(2026, 10, 9), Shift.Day);

        var tallies = InspectionSummary.Tally(
            Supervisors,
            Points,
            [],
            [Inspected(1, 10, 1, Day8, InspectionResult.Passed), Inspected(2, 10, 1, day9, InspectionResult.Passed)],
            []);

        Assert.Equal(1, tallies[0].PointsInspected);
        Assert.Equal(2, tallies[0].Inspections);
    }

    [Fact]
    public void Work_submitted_after_the_shift_end_can_never_be_inspected_so_it_is_left_out_of_the_beat()
    {
        var late = new RoundSubmission(5, 10, Day8, new TimeOnly(16, 0), new TimeOnly(18, 0), ShiftCalendar.EndUtc(Day8).AddMinutes(30));

        var tallies = InspectionSummary.Tally(
            [Supervisors[0]],
            Points,
            [Sent(1, 11, Day8), late],
            [Inspected(1, 11, 1, Day8, InspectionResult.Passed)],
            [Round(11, Day8, RoundOutcome.Passed), new FinishedRound(10, Day8, new TimeOnly(16, 0), new TimeOnly(18, 0), RoundOutcome.NotInspected)]);

        Assert.Equal(1, tallies[0].Submitted);          // the in-shift control still counts
        Assert.Equal(1, tallies[0].SubmittedInspected);
        Assert.Equal(0, tallies[0].NotInspected);       // the late work is what made the round ไม่ได้ตรวจ
    }

    /// <summary>Round 08:00-10:00 of Day8, sent <paramref name="minutes"/> minutes after 08:00 Thai time.</summary>
    private static RoundSubmission SentAt(long id, int point, int minutes) =>
        new(id, point, Day8, new TimeOnly(8, 0), new TimeOnly(10, 0), ShiftCalendar.StartUtc(Day8).AddHours(1).AddMinutes(minutes));

    [Fact]
    public void A_submission_sent_over_before_anyone_inspected_it_is_left_out_because_it_can_never_be_inspected()
    {
        // the Cleaner sends twice in the open round; the Supervisor is offered only the latest, so only it is inspected
        var tallies = InspectionSummary.Tally(
            [Supervisors[0]],
            Points,
            [SentAt(1, 10, 20), SentAt(2, 10, 40)],
            [Inspected(2, 10, 1, Day8, InspectionResult.Passed)],
            [Round(10, Day8, RoundOutcome.Passed)]);

        Assert.Equal(1, tallies[0].Submitted);
        Assert.Equal(1, tallies[0].SubmittedInspected);
        Assert.Equal(1, tallies[0].Inspections);
    }

    [Fact]
    public void Sent_over_twice_with_nothing_inspected_counts_the_round_once_and_not_inspected()
    {
        var tallies = InspectionSummary.Tally(
            [Supervisors[0]],
            Points,
            [SentAt(1, 10, 20), SentAt(2, 10, 40)],
            [],
            [Round(10, Day8, RoundOutcome.NotInspected)]);

        Assert.Equal(1, tallies[0].Submitted);
        Assert.Equal(0, tallies[0].SubmittedInspected);
        Assert.Equal(1, tallies[0].NotInspected);
    }

    [Fact]
    public void A_submission_that_was_inspected_is_counted_even_when_a_rework_fix_followed_it()
    {
        var tallies = InspectionSummary.Tally(
            [Supervisors[0]],
            Points,
            [SentAt(1, 10, 20), SentAt(2, 10, 50)],
            [Inspected(1, 10, 1, Day8, InspectionResult.Rework), Inspected(2, 10, 1, Day8, InspectionResult.Passed)],
            [Round(10, Day8, RoundOutcome.Passed)]);

        Assert.Equal(2, tallies[0].Submitted);
        Assert.Equal(2, tallies[0].SubmittedInspected);
    }

    [Fact]
    public void Two_submissions_at_the_same_instant_leave_out_the_one_with_the_lower_id()
    {
        var tallies = InspectionSummary.Tally(
            [Supervisors[0]],
            Points,
            [SentAt(1, 10, 20), SentAt(2, 10, 20)],
            [Inspected(2, 10, 1, Day8, InspectionResult.Passed)],
            [Round(10, Day8, RoundOutcome.Passed)]);

        Assert.Equal(1, tallies[0].Submitted);
        Assert.Equal(1, tallies[0].SubmittedInspected);
    }
    private static void AssertMetrics(SupervisorTally expected, SupervisorTally actual) =>
        Assert.Equal(
            (expected.SupervisorId, expected.PointsInspected, expected.PointsTotal, expected.Submitted, expected.SubmittedInspected,
                expected.NotInspected, expected.Inspections, expected.Reworks, expected.GpsFlagged),
            (actual.SupervisorId, actual.PointsInspected, actual.PointsTotal, actual.Submitted, actual.SubmittedInspected,
                actual.NotInspected, actual.Inspections, actual.Reworks, actual.GpsFlagged));

    [Fact]
    public void Uninspected_list_is_exactly_the_active_own_shift_beat_minus_the_supervisors_own_inspections()
    {
        var tallies = InspectionSummary.Tally(
            [.. Supervisors, new(4, 3, Shift.Day)],
            [.. Points, new(13, 1, false, true)], // inactive Area has the same Counted=false rule
            [],
            [
                Inspected(1, 10, 2, Day8, InspectionResult.Passed), // another Supervisor does not clear supervisor 1's beat
                Inspected(2, 11, 1, Night8, InspectionResult.Passed), // own inspection in a different shift does not clear it
                Inspected(3, 20, 1, Day8, InspectionResult.Passed), // cover does not clear own beat
                Inspected(4, 12, 1, Day8, InspectionResult.Passed), // inactive point stays out
                Inspected(5, 13, 1, Day8, InspectionResult.Passed), // inactive Area stays out
            ],
            []);

        Assert.Equal([10, 11], tallies[0].UninspectedPointIds.Order());
        Assert.Equal([10], tallies[1].UninspectedPointIds); // Day-only 11 excluded for Night
        Assert.Equal([20], tallies[2].UninspectedPointIds);
        Assert.Empty(tallies[3].UninspectedPointIds); // no active points in this building
        Assert.All(tallies, tally => Assert.Equal(tally.PointsTotal - tally.PointsInspected, tally.UninspectedPointIds.Count));
        AssertMetrics(new SupervisorTally(1, 0, 2, 0, 0, 0, 4, 0, 0), tallies[0]);
        AssertMetrics(new SupervisorTally(2, 0, 1, 0, 0, 0, 1, 0, 0), tallies[1]);
        AssertMetrics(new SupervisorTally(4, 0, 0, 0, 0, 0, 0, 0, 0), tallies[3]);
    }

}
