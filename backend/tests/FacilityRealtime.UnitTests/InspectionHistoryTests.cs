using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>Plan 6 decision 13: outcomes of rounds with a submission, once the shift's attendance window has closed.</summary>
public class InspectionHistoryTests
{
    private const int Closes = 60;
    private static readonly ShiftSlot Day8 = new(new DateOnly(2026, 10, 8), Shift.Day);
    private static readonly ShiftSlot Night8 = new(new DateOnly(2026, 10, 8), Shift.Night);
    private static readonly DateTime AfterBoth = ShiftCalendar.EndUtc(Night8).AddMinutes(Closes + 1);

    /// <summary>A submission in the two-hour round starting at <paramref name="startHour"/>, <paramref name="minutesIn"/> minutes in.</summary>
    private static RoundSubmission Sent(long id, int point, ShiftSlot slot, int startHour, int minutesIn) =>
        new(id, point, slot, new TimeOnly(startHour, 0), new TimeOnly(startHour + 2, 0), ShiftCalendar.ToUtc(slot, new TimeOnly(startHour, minutesIn)));

    private static InspectionFact Inspected(long submissionId, InspectionResult result, ShiftSlot slot, int hour, int minute) =>
        new(submissionId, result, ShiftCalendar.ToUtc(slot, new TimeOnly(hour, minute)), result == InspectionResult.Rework ? "ยังสกปรก" : null);

    [Fact]
    public void Each_round_gets_its_outcome_in_the_order_of_the_shifts()
    {
        var rounds = InspectionHistory.FinishedRounds(
            [Sent(1, 10, Day8, 16, 10), Sent(2, 10, Day8, 7, 10), Sent(3, 11, Day8, 7, 20), Sent(4, 10, Night8, 20, 5)],
            [Inspected(2, InspectionResult.Rework, Day8, 7, 30), Inspected(3, InspectionResult.Passed, Day8, 7, 40)],
            AfterBoth,
            Closes);

        Assert.Equal(
            new[]
            {
                (10, Shift.Day, 7, RoundOutcome.NotReworked),
                (11, Shift.Day, 7, RoundOutcome.Passed),
                (10, Shift.Day, 16, RoundOutcome.NotInspected),
                (10, Shift.Night, 20, RoundOutcome.NotInspected),
            },
            rounds.Select(r => (r.PointId, r.Slot.Shift, r.RoundStart.Hour, r.Outcome)));
    }

    [Fact]
    public void Several_submissions_of_one_round_are_one_round()
    {
        var rounds = InspectionHistory.FinishedRounds(
            [Sent(1, 10, Day8, 7, 10), Sent(2, 10, Day8, 7, 40)],
            [Inspected(1, InspectionResult.Rework, Day8, 7, 20), Inspected(2, InspectionResult.Passed, Day8, 7, 50)],
            AfterBoth,
            Closes);

        Assert.Equal(RoundOutcome.Passed, Assert.Single(rounds).Outcome);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(Closes, false)]
    [InlineData(Closes + 1, true)]
    public void A_shift_still_taking_late_work_has_no_outcome_yet(int minutesAfterTheShift, bool counted)
    {
        var rounds = InspectionHistory.FinishedRounds(
            [Sent(1, 10, Day8, 7, 10)], [], ShiftCalendar.EndUtc(Day8).AddMinutes(minutesAfterTheShift), Closes);

        Assert.Equal(counted ? 1 : 0, rounds.Count);
    }
}
