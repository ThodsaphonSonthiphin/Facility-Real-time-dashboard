using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Rounds;

/// <summary>Round null means Off-Round. LateMinutes is set only for a Late Submission.</summary>
public sealed record RoundPlacement(RoundWindow? Round, Placement Placement, int? LateMinutes);

/// <summary>ADR facility-0047 rule 3, the flowchart in docs/design/database.html. No grace period.</summary>
public static class RoundPlacer
{
    public static RoundPlacement Place(
        IReadOnlyList<RoundWindow> windows,
        IReadOnlyList<SubmissionFact> submissions,
        IReadOnlyList<InspectionFact> inspections,
        DateTime submittedAtUtc)
    {
        var round = RoundRules.Current(windows, submittedAtUtc);
        if (round is null)
        {
            return new RoundPlacement(null, Placement.OffRound, null);
        }

        var roundSubmissions = RoundRules.SubmissionsOf(round, submissions);
        var needsRework = RoundRules.NeedsRework(roundSubmissions, RoundRules.LatestInspection(roundSubmissions, inspections));

        if (submittedAtUtc <= round.EndUtc)
        {
            return new RoundPlacement(round, needsRework ? Placement.Rework : Placement.OnTime, null);
        }

        if (roundSubmissions.Count == 0)
        {
            var lateMinutes = (int)Math.Ceiling((submittedAtUtc - round.EndUtc).TotalMinutes);
            return new RoundPlacement(round, Placement.Late, lateMinutes);
        }

        return needsRework
            ? new RoundPlacement(round, Placement.Rework, null)
            : new RoundPlacement(null, Placement.OffRound, null);
    }
}
