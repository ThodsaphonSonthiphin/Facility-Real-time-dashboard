using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Rounds;

/// <summary>facility-0047 rule 5: what a finished round that had a submission left in the history.</summary>
public enum RoundOutcome
{
    /// <summary>ตรวจผ่าน: the latest inspection passed and nothing was submitted after it.</summary>
    Passed,

    /// <summary>ไม่ได้ตรวจ: submitted, and the latest submission was never inspected.</summary>
    NotInspected,

    /// <summary>ไม่ได้แก้: the Supervisor asked for rework and nothing was submitted after it.</summary>
    NotReworked,
}

public static class RoundOutcomes
{
    /// <summary><paramref name="roundSubmissions"/>: the round's OnTime, Late and Rework submissions, at least one.</summary>
    public static RoundOutcome Of(IReadOnlyList<SubmissionFact> roundSubmissions, IReadOnlyList<InspectionFact> inspections)
    {
        var latest = RoundRules.LatestInspection(roundSubmissions, inspections);
        return latest is null ? RoundOutcome.NotInspected
            : RoundRules.NeedsRework(roundSubmissions, latest) ? RoundOutcome.NotReworked
            : roundSubmissions.Any(s => s.SubmittedAt > latest.InspectedAt) ? RoundOutcome.NotInspected
            : RoundOutcome.Passed;
    }
}
