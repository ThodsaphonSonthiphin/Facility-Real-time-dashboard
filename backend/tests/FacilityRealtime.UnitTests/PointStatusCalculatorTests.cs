using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using InspectionResult = FacilityRealtime.Domain.Enums.InspectionResult;
using Shift = FacilityRealtime.Domain.Enums.Shift;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0047 rules 2, 4 and 5 on 8 Oct with rounds 08:00-10:00 and 16:00-18:00.</summary>
public class PointStatusCalculatorTests
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

    private static PointStatusResult Status(
        DateTime now,
        SubmissionFact[]? submissions = null,
        InspectionFact[]? inspections = null,
        bool areaWorksThisShift = true,
        IReadOnlyList<RoundWindow>? rounds = null) =>
        PointStatusCalculator.Calculate(
            areaWorksThisShift,
            rounds ?? Rounds,
            submissions ?? Array.Empty<SubmissionFact>(),
            inspections ?? Array.Empty<InspectionFact>(),
            now);

    [Fact]
    public void Area_without_this_shift_is_off_hours()
    {
        var result = Status(Thai(8, 30), areaWorksThisShift: false);

        Assert.Equal(PointStatus.OffHours, result.Status);
        Assert.Null(result.CurrentRound);
        Assert.Null(result.NextRound);
    }

    [Fact]
    public void Point_without_rounds_this_shift_is_off_hours()
    {
        Assert.Equal(PointStatus.OffHours, Status(Thai(8, 30), rounds: Array.Empty<RoundWindow>()).Status);
    }

    [Fact]
    public void Before_the_first_round_shows_the_next_one()
    {
        var result = Status(Thai(7, 30));

        Assert.Equal(PointStatus.BeforeFirstRound, result.Status);
        Assert.Null(result.CurrentRound);
        Assert.Equal(1, result.NextRound?.Id);
    }

    [Fact]
    public void Open_round_without_a_submission_is_not_yet_done()
    {
        var result = Status(Thai(8, 30));

        Assert.Equal(PointStatus.NotYetDone, result.Status);
        Assert.Equal(1, result.CurrentRound?.Id);
        Assert.Equal(2, result.NextRound?.Id);
    }

    [Fact]
    public void Round_end_itself_is_not_yet_overdue()
    {
        Assert.Equal(PointStatus.NotYetDone, Status(Thai(10, 0)).Status);
    }

    [Fact]
    public void Ended_round_without_a_submission_is_overdue()
    {
        Assert.Equal(PointStatus.Overdue, Status(Thai(10, 1)).Status);
    }

    [Fact]
    public void Submission_waits_for_inspection()
    {
        Assert.Equal(PointStatus.PendingInspection, Status(Thai(8, 30), [Sent(10, 1, 8, 20)]).Status);
    }

    [Fact]
    public void Late_submission_waits_for_inspection_too()
    {
        Assert.Equal(PointStatus.PendingInspection, Status(Thai(11, 0), [Sent(10, 1, 10, 30)]).Status);
    }

    [Fact]
    public void Passed_inspection_is_passed()
    {
        var result = Status(Thai(9, 0), [Sent(10, 1, 8, 20)], [Inspected(10, InspectionResult.Passed, 8, 40)]);

        Assert.Equal(PointStatus.Passed, result.Status);
    }

    [Fact]
    public void Rework_inspection_needs_rework()
    {
        var result = Status(Thai(9, 0), [Sent(10, 1, 8, 20)], [Inspected(10, InspectionResult.Rework, 8, 40)]);

        Assert.Equal(PointStatus.Rework, result.Status);
    }

    [Fact]
    public void Rework_stays_after_the_round_ends()
    {
        var result = Status(Thai(11, 0), [Sent(10, 1, 8, 20)], [Inspected(10, InspectionResult.Rework, 8, 40)]);

        Assert.Equal(PointStatus.Rework, result.Status);
    }

    [Fact]
    public void Resubmitted_rework_waits_for_inspection_again()
    {
        var result = Status(
            Thai(9, 0),
            [Sent(10, 1, 8, 20), Sent(11, 1, 8, 50)],
            [Inspected(10, InspectionResult.Rework, 8, 40)]);

        Assert.Equal(PointStatus.PendingInspection, result.Status);
    }

    [Fact]
    public void New_round_starts_the_card_over()
    {
        var result = Status(Thai(16, 30), [Sent(10, 1, 8, 20)], [Inspected(10, InspectionResult.Passed, 8, 40)]);

        Assert.Equal(PointStatus.NotYetDone, result.Status);
        Assert.Equal(2, result.CurrentRound?.Id);
        Assert.Null(result.NextRound);
    }

    [Fact]
    public void Off_round_submission_does_not_count()
    {
        Assert.Equal(PointStatus.NotYetDone, Status(Thai(16, 30), [Sent(12, null, 15, 30)]).Status);
    }
}
