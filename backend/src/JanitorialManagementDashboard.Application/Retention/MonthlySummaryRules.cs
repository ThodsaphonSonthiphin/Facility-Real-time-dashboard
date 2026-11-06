using JanitorialManagementDashboard.Application.Rounds;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Retention;

public sealed record RetentionRound(DateOnly ShiftDate, Shift Shift, TimeOnly Start, TimeOnly End);
public sealed record RetentionSubmission(long Id, DateOnly ShiftDate, Shift Shift, TimeOnly? Start, TimeOnly? End,
    DateTime SubmittedAt, Placement Placement, CleaningStatus Status);
public sealed record RetentionInspection(long ScanRecordId, InspectionResult Result, DateTime InspectedAt);

public static class MonthlySummaryRules
{
    public static PointMonthlySummary Build(int pointId, DateOnly month, IReadOnlyList<RetentionSubmission> submissions,
        IReadOnlyList<RetentionInspection> inspections, IEnumerable<RetentionRound> expected)
    {
        var rounds = submissions.Where(s => s.Placement != Placement.OffRound && s.Start != null && s.End != null)
            .GroupBy(s => new RetentionRound(s.ShiftDate, s.Shift, s.Start!.Value, s.End!.Value)).ToList();
        var all = expected.Concat(rounds.Select(r => r.Key)).Distinct().ToList();
        var first = rounds.Select(r => r.OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id).First()).ToList();
        var bySubmission = inspections.ToLookup(i => i.ScanRecordId);
        return new()
        {
            ServicePointId = pointId, Month = month, RoundsTotal = all.Count,
            SubmittedOnTime = first.Count(s => s.Placement == Placement.OnTime),
            SubmittedLate = first.Count(s => s.Placement == Placement.Late),
            NotDone = all.Count - rounds.Count,
            InspectedPass = inspections.Count(i => i.Result == InspectionResult.Passed),
            InspectedFail = inspections.Count(i => i.Result == InspectionResult.Rework),
            NotInspected = rounds.Count(r => RoundOutcomes.Of(
                r.Select(s => new SubmissionFact(s.Id, null, s.SubmittedAt)).ToList(),
                r.SelectMany(s => bySubmission[s.Id]).Select(i => new InspectionFact(i.ScanRecordId, i.Result, i.InspectedAt)).ToList()) == RoundOutcome.NotInspected),
            IssuesReported = submissions.Count(s => s.Status == CleaningStatus.Issue),
        };
    }
}
