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

        var latest = scans
            .Where(s => s.PointId == round.PointId
                && s.ShiftDate == slot.ShiftDate && s.Shift == slot.Shift
                && s.Placement != Placement.OffRound && s.SubmittedAt <= at
                && (s.OriginId ?? s.ExistingWindowId) == round.OriginId
                && s.RoundStart == round.Start && s.RoundEnd == round.End)
            .OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id)
            .FirstOrDefault();
        var inspection = latest is null ? null : inspections
            .Where(i => i.SubmissionId == latest.Id && i.InspectedAt <= at)
            .OrderByDescending(i => i.InspectedAt).ThenByDescending(i => i.Id)
            .FirstOrDefault();
        // Issue is a point card tag, not round progress: it follows the point's newest scan in any slot or placement.
        var issue = scans
            .Where(s => s.PointId == round.PointId && s.SubmittedAt <= at)
            .OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id)
            .FirstOrDefault();
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
