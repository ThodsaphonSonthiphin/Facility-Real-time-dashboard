using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Overview;

/// <summary>
/// The pure predicates of the overview: whole-Area completion, the entry partition and one point's latest counted
/// round work. Nothing here reads a clock or a database, so the summary, the list and the detail cannot disagree.
/// </summary>
public static class OverviewRules
{
    /// <summary>
    /// A whole Area is submitted only when every due point is; inspected only when every due point is. An Area with no
    /// due point is never submitted, incomplete, pending or without a submission, so a zero denominator stays a true
    /// zero instead of a vacuous complete.
    /// </summary>
    public static AreaWork Work(IReadOnlyList<OverviewPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var due = points.Where(p => p.IsDue).ToArray();
        var submitted = due.Count(p => p.IsSubmitted);
        var inspected = due.Count(p => p.IsInspected);
        return new(due.Length, submitted, inspected,
            due.Length > 0 && submitted == due.Length,
            due.Length > 0 && inspected == due.Length,
            due.Any(p => !p.IsSubmitted),
            due.Any(p => p.IsSubmitted && !p.IsInspected),
            due.Length > 0 && submitted == 0,
            due.Any(p => p.NeedsRework), points.Any(p => p.IsIssue),
            due.Any(p => p.IsOverdue));
    }

    /// <summary>The one shared answer to an Area's entry category (Task 11's <see cref="ScheduleEntryEvidence"/>).</summary>
    public static string Entry(int areaId, IReadOnlySet<int> enteredAreas, IReadOnlySet<int> assignedAreas)
    {
        ArgumentNullException.ThrowIfNull(enteredAreas);
        ArgumentNullException.ThrowIfNull(assignedAreas);
        return ScheduleEntryEvidence.EntryOf(enteredAreas.Contains(areaId), assignedAreas.Contains(areaId));
    }

    /// <summary>
    /// The latest counted submission of one round at <paramref name="at"/>, its latest inspection at/before that
    /// instant, and the point's Issue state from its latest scan across slots and placements. Slot membership uses the
    /// recorded ShiftDate/Shift, never the wall-clock time of the submission.
    /// </summary>
    public static OverviewPoint Latest(ShiftSlot slot, RoundObligation round,
        IReadOnlyList<OverviewSubmission> scans, IReadOnlyList<OverviewInspection> inspections, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(scans);
        ArgumentNullException.ThrowIfNull(inspections);

        return LatestForPoint(
            slot,
            round,
            scans.Where(s => s.PointId == round.PointId),
            id => inspections.Where(i => i.SubmissionId == id),
            at);
    }

    internal static OverviewPoint LatestForPoint(
        ShiftSlot slot,
        RoundObligation round,
        IEnumerable<OverviewSubmission> pointScans,
        Func<long, IEnumerable<OverviewInspection>> inspectionsForSubmission,
        DateTime at)
    {
        OverviewSubmission? latest = null;
        OverviewSubmission? issue = null;
        foreach (var scan in pointScans)
        {
            if (scan.SubmittedAt > at) continue;

            if (issue is null || scan.SubmittedAt > issue.SubmittedAt || (scan.SubmittedAt == issue.SubmittedAt && scan.Id > issue.Id))
            {
                issue = scan;
            }

            if (scan.ShiftDate == slot.ShiftDate && scan.Shift == slot.Shift
                && scan.Placement != Placement.OffRound
                && (scan.OriginId ?? scan.ExistingWindowId) == round.OriginId
                && scan.RoundStart == round.Start && scan.RoundEnd == round.End
                && (latest is null || scan.SubmittedAt > latest.SubmittedAt || (scan.SubmittedAt == latest.SubmittedAt && scan.Id > latest.Id)))
            {
                latest = scan;
            }
        }

        OverviewInspection? inspection = null;
        if (latest is not null)
        {
            foreach (var candidate in inspectionsForSubmission(latest.Id))
            {
                if (candidate.InspectedAt <= at
                    && (inspection is null || candidate.InspectedAt > inspection.InspectedAt
                        || (candidate.InspectedAt == inspection.InspectedAt && candidate.Id > inspection.Id)))
                {
                    inspection = candidate;
                }
            }
        }

        var windowEnd = ShiftCalendar.ToUtc(slot, round.End);

        return new(round.PointId, true,
            latest is not null,
            latest is not null && inspection is not null,
            inspection?.Result == InspectionResult.Rework,
            issue?.Status == CleaningStatus.Issue,
            latest is null && at > windowEnd,
            false,
            latest?.Id, inspection?.Id);
    }
}
