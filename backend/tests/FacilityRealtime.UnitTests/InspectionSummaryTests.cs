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

        Assert.Equal(new SupervisorTally(1, 1, 2, 2, 1, 1, 2, 1, 1), tallies[0]);
        Assert.Equal(new SupervisorTally(2, 1, 1, 1, 1, 0, 1, 0, 0), tallies[1]);
        Assert.Equal(new SupervisorTally(3, 0, 1, 1, 1, 0, 0, 0, 0), tallies[2]);
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
}
