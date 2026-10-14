using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0047 rules 2, 6 and 7 on 8 Oct with rounds 08:00-10:00 (id 1) and 16:00-18:00 (id 2).</summary>
public class InspectionRulesTests
{
    private static readonly ShiftSlot Day8 = new(new DateOnly(2026, 10, 8), Shift.Day);

    private static readonly IReadOnlyList<RoundWindow> Rounds =
    [
        RoundWindow.For(1, new TimeOnly(8, 0), new TimeOnly(10, 0), Day8),
        RoundWindow.For(2, new TimeOnly(16, 0), new TimeOnly(18, 0), Day8),
    ];

    private static DateTime Thai(int hour, int minute) =>
        new DateTime(2026, 10, 8, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    private static SubmissionFact Sent(long id, int? roundId, int hour, int minute) => new(id, roundId, Thai(hour, minute));

    private static InspectionFact Inspected(long submissionId, InspectionResult result, int hour, int minute) =>
        new(submissionId, result, Thai(hour, minute));

    private static InspectionTarget Find(
        DateTime now, SubmissionFact[]? submissions = null, InspectionFact[]? inspections = null, bool areaWorksThisShift = true) =>
        InspectionRules.Find(areaWorksThisShift, Rounds, submissions ?? [], inspections ?? [], now);

    [Fact]
    public void Nothing_to_inspect_before_the_first_round()
    {
        var target = Find(Thai(7, 30));

        Assert.Null(target.Submission);
        Assert.Equal(InspectionRefusal.NothingSubmitted, target.Refusal);
    }

    [Fact]
    public void Nothing_to_inspect_while_the_current_round_has_no_submission() =>
        Assert.Equal(InspectionRefusal.NothingSubmitted, Find(Thai(8, 30)).Refusal);

    [Fact]
    public void An_earlier_rounds_submission_is_not_inspected() =>
        Assert.Equal(InspectionRefusal.NothingSubmitted, Find(Thai(16, 30), [Sent(1, 1, 8, 10)]).Refusal);

    [Fact]
    public void An_off_round_submission_is_not_inspected() =>
        Assert.Equal(InspectionRefusal.NothingSubmitted, Find(Thai(8, 30), [Sent(1, null, 8, 10)]).Refusal);

    [Fact]
    public void An_area_without_this_shift_has_nothing_to_inspect() =>
        Assert.Equal(InspectionRefusal.NothingSubmitted, Find(Thai(8, 30), [Sent(1, 1, 8, 10)], areaWorksThisShift: false).Refusal);

    [Fact]
    public void The_latest_submission_of_the_current_round_is_inspected()
    {
        var target = Find(Thai(9, 0), [Sent(1, 1, 8, 10), Sent(2, 1, 8, 40)]);

        Assert.Equal(2, target.Submission!.Id);
        Assert.Null(target.Refusal);
    }

    [Fact]
    public void A_round_stays_inspectable_after_its_end_until_the_next_round_starts() =>
        Assert.Equal(1, Find(Thai(15, 59), [Sent(1, 1, 8, 10)]).Submission!.Id);

    [Fact]
    public void Rework_waits_for_the_cleaner()
    {
        var target = Find(Thai(9, 0), [Sent(1, 1, 8, 10)], [Inspected(1, InspectionResult.Rework, 8, 20)]);

        Assert.Null(target.Submission);
        Assert.Equal(InspectionRefusal.AwaitingRework, target.Refusal);
    }

    [Fact]
    public void The_resubmission_after_rework_is_inspected() =>
        Assert.Equal(
            2,
            Find(Thai(9, 0), [Sent(1, 1, 8, 10), Sent(2, 1, 8, 40)], [Inspected(1, InspectionResult.Rework, 8, 20)]).Submission!.Id);

    [Fact]
    public void A_passed_round_can_be_inspected_again()
    {
        var target = Find(Thai(9, 0), [Sent(1, 1, 8, 10)], [Inspected(1, InspectionResult.Passed, 8, 20)]);

        Assert.Equal(1, target.Submission!.Id);
        Assert.Null(target.Refusal);
    }
}
