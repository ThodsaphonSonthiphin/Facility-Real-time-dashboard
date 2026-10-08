using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Rounds;

/// <summary>The questions RoundPlacer and PointStatusCalculator both ask about one round.</summary>
internal static class RoundRules
{
    /// <summary>ADR facility-0047 rule 2: the latest round of the shift whose start has passed.</summary>
    public static RoundWindow? Current(IReadOnlyList<RoundWindow> windows, DateTime nowUtc) =>
        windows.Where(w => w.StartUtc <= nowUtc).MaxBy(w => w.StartUtc);

    public static RoundWindow? Next(IReadOnlyList<RoundWindow> windows, DateTime nowUtc) =>
        windows.Where(w => w.StartUtc > nowUtc).MinBy(w => w.StartUtc);

    public static List<SubmissionFact> SubmissionsOf(RoundWindow round, IReadOnlyList<SubmissionFact> submissions) =>
        submissions.Where(s => s.RoundWindowId == round.Id).ToList();

    public static InspectionFact? LatestInspection(IReadOnlyList<SubmissionFact> roundSubmissions, IReadOnlyList<InspectionFact> inspections)
    {
        var ids = roundSubmissions.Select(s => s.Id).ToHashSet();
        return inspections.Where(i => ids.Contains(i.SubmissionId)).MaxBy(i => i.InspectedAt);
    }

    /// <summary>The latest inspection said rework and nothing was submitted for the round after it.</summary>
    public static bool NeedsRework(IReadOnlyList<SubmissionFact> roundSubmissions, InspectionFact? latest) =>
        latest is { Result: InspectionResult.Rework }
        && !roundSubmissions.Any(s => s.SubmittedAt > latest.InspectedAt);
}
