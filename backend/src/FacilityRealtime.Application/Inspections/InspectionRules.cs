using FacilityRealtime.Application.Rounds;

namespace FacilityRealtime.Application.Inspections;

/// <summary>Why the Supervisor's screen has no inspect button.</summary>
public enum InspectionRefusal
{
    /// <summary>The current round has no submission yet, or there is no current round (facility-0047 rule 7).</summary>
    NothingSubmitted,

    /// <summary>The latest inspection asked for rework and the Cleaner has not submitted since (facility-0029).</summary>
    AwaitingRework,
}

/// <summary>Exactly one of Submission and Refusal is set.</summary>
public sealed record InspectionTarget(SubmissionFact? Submission, InspectionRefusal? Refusal);

public static class InspectionRules
{
    /// <summary>inspections.defect is VARCHAR(1000).</summary>
    public const int MaxDefectLength = 1000;

    /// <summary>
    /// facility-0047 rules 2, 6 and 7: the Supervisor judges the latest submission of the current round — the latest round
    /// of the shift whose start has passed — and may do so any number of times, but not while a rework is still owed.
    /// </summary>
    public static InspectionTarget Find(
        bool areaWorksThisShift,
        IReadOnlyList<RoundWindow> windows,
        IReadOnlyList<SubmissionFact> submissions,
        IReadOnlyList<InspectionFact> inspections,
        DateTime nowUtc)
    {
        var round = areaWorksThisShift ? RoundRules.Current(windows, nowUtc) : null;
        var roundSubmissions = round is null ? new List<SubmissionFact>() : RoundRules.SubmissionsOf(round, submissions);
        if (roundSubmissions.Count == 0)
        {
            return new InspectionTarget(null, InspectionRefusal.NothingSubmitted);
        }

        if (RoundRules.NeedsRework(roundSubmissions, RoundRules.LatestInspection(roundSubmissions, inspections)))
        {
            return new InspectionTarget(null, InspectionRefusal.AwaitingRework);
        }

        return new InspectionTarget(roundSubmissions.OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id).Last(), null);
    }
}
