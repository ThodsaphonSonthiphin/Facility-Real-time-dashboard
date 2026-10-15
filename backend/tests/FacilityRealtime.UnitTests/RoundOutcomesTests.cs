using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0047 rule 5: what a finished round with a submission left in the history.</summary>
public class RoundOutcomesTests
{
    private static DateTime Thai(int hour, int minute) => new DateTime(2026, 10, 8, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    private static SubmissionFact Sent(long id, int hour, int minute) => new(id, 1, Thai(hour, minute));

    private static InspectionFact Inspected(long submissionId, InspectionResult result, int hour, int minute) => new(submissionId, result, Thai(hour, minute));

    [Fact]
    public void Submitted_and_never_inspected_is_not_inspected() =>
        Assert.Equal(RoundOutcome.NotInspected, RoundOutcomes.Of([Sent(1, 8, 10)], []));

    [Fact]
    public void A_passed_inspection_is_passed() =>
        Assert.Equal(RoundOutcome.Passed, RoundOutcomes.Of([Sent(1, 8, 10)], [Inspected(1, InspectionResult.Passed, 8, 20)]));

    [Fact]
    public void Rework_never_fixed_is_not_reworked() =>
        Assert.Equal(RoundOutcome.NotReworked, RoundOutcomes.Of([Sent(1, 8, 10)], [Inspected(1, InspectionResult.Rework, 8, 20)]));

    [Fact]
    public void A_fix_nobody_inspected_is_not_inspected() =>
        Assert.Equal(
            RoundOutcome.NotInspected,
            RoundOutcomes.Of([Sent(1, 8, 10), Sent(2, 8, 40)], [Inspected(1, InspectionResult.Rework, 8, 20)]));

    [Fact]
    public void A_fix_that_passed_is_passed() =>
        Assert.Equal(
            RoundOutcome.Passed,
            RoundOutcomes.Of(
                [Sent(1, 8, 10), Sent(2, 8, 40)],
                [Inspected(1, InspectionResult.Rework, 8, 20), Inspected(2, InspectionResult.Passed, 8, 50)]));

    [Fact]
    public void Inspections_of_other_submissions_do_not_count() =>
        Assert.Equal(RoundOutcome.NotInspected, RoundOutcomes.Of([Sent(1, 8, 10)], [Inspected(9, InspectionResult.Passed, 8, 20)]));
}
