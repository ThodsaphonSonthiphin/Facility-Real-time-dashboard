using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Overview;

/// <summary>The fixed group allowlist. Chart slices use only the disjoint staffing keys; follow-up groups may overlap.</summary>
public static class OverviewGroupKeys
{
    public const string Entered = "entered";
    public const string Covered = "covered";
    public const string Unassigned = "unassigned";
    public const string NotEntered = "notEntered";
    public const string Submitted = "submitted";
    public const string Incomplete = "incomplete";
    public const string Inspected = "inspected";
    public const string PendingInspection = "pendingInspection";
    public const string NoSubmission = "noSubmission";
    public const string Rework = "rework";
    public const string Issue = "issue";
    public const string Overdue = "overdue";
    public const string Upcoming = "upcoming";
    public const string NoRounds = "noRounds";
    public const string MissedCleaning = "missedCleaning";
    public const string MissedInspection = "missedInspection";

    /// <summary>The one allowlist Task 6 validates group parameters against.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Entered, Covered, Unassigned, NotEntered, Submitted, Incomplete, Inspected,
        PendingInspection, NoSubmission, Rework, Issue, Overdue, Upcoming, NoRounds,
        MissedCleaning, MissedInspection,
    ];

    public static bool IsKnown(string group) => All.Contains(group, StringComparer.Ordinal);
}

/// <summary>
/// Builds one projection from Task 1 targets, Task 4 availability and Task 11 entry evidence. Every summary count and
/// every group membership comes from <see cref="Members"/>, so the chart, the list and the detail cannot disagree.
/// </summary>
public static class OverviewProjection
{
    /// <summary>
    /// Projects every target Area. When <paramref name="buildingId"/> is set, ownership is applied before Areas are
    /// deduplicated by ID so an Area that moved during the slot keeps its historical building's row without letting the
    /// current labels overwrite the old owner.
    /// </summary>
    public static OverviewSnapshot Build(ShiftSlot slot, OverviewMoment moment, TargetTimeline targets,
        OverviewFacts facts, SectionAvailability availability, int? buildingId)
    {
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(availability);

        var origins = ResolveOrigins(facts.Submissions, slot, targets.Rounds);
        var scans = origins.Resolved;
        var current = CurrentPoints(slot, moment, targets.Rounds, scans, facts.Inspections);
        var history = History(slot, targets.Rounds, scans, facts.Inspections, origins.Ambiguous);
        var image = targets.Current;
        var entered = ScheduleEntryEvidence.EnteredAreas(
            new ScheduleSlot(slot.ShiftDate, slot.Shift), facts.EntryEvidence);
        var nextRoundAreas = AreasWithNextRound(image, slot, moment.EvaluationUtc);
        var currentPointIds = current.Select(c => c.Round.PointId).ToHashSet();
        // Ambiguity is scoped to the ownership view that owns a candidate round, so a moved Area can mark the affected
        // historical row without claiming the current owner's row is unproven.
        var ambiguousViews = origins.Ambiguous
            .SelectMany(a => a.Rounds).Select(r => (r.AreaId, r.BuildingId)).ToHashSet();

        var views = Views(targets, image, current, history, currentPointIds, slot, moment.EvaluationUtc);

        var areas = new List<OverviewArea>();
        if (buildingId is null)
        {
            foreach (var group in views.GroupBy(v => v.AreaId))
            {
                var all = group.ToArray();
                var primary = all.FirstOrDefault(v => v.Points.Count > 0)
                    ?? all.FirstOrDefault(v => v.IsCurrentOwner)
                    ?? all.OrderByDescending(v => v.Activity).ThenByDescending(v => v.Revision).First();
                var merged = all.SelectMany(v => v.History)
                    .OrderBy(h => h.Round.ActiveFromUtc).ThenBy(h => h.Round.PointId).ToArray();
                areas.Add(Row(primary, merged, entered, facts.AssignedAreas, nextRoundAreas,
                    includeStructure: all.Any(v => v.IsCurrentOwner),
                    originsIncomplete: all.Any(v => ambiguousViews.Contains((v.AreaId, v.BuildingId)))));
            }
        }
        else
        {
            foreach (var view in views.Where(v => v.BuildingId == buildingId.Value))
                areas.Add(Row(view, view.History
                    .OrderBy(h => h.Round.ActiveFromUtc).ThenBy(h => h.Round.PointId).ToArray(),
                    entered, facts.AssignedAreas, nextRoundAreas, view.IsCurrentOwner,
                    ambiguousViews.Contains((view.AreaId, view.BuildingId))));
        }

        areas.Sort((left, right) =>
        {
            var building = string.CompareOrdinal(left.BuildingCode, right.BuildingCode);
            if (building != 0) return building;
            var code = string.CompareOrdinal(left.Code, right.Code);
            return code != 0 ? code : left.Id.CompareTo(right.Id);
        });

        return new(slot, moment, targets, availability, areas);
    }

    /// <summary>
    /// The only source of cardinalities. Areas are distinct by ID, so a whole-factory total never doubles an Area that
    /// was projected under more than one historical owner.
    /// </summary>
    public static IReadOnlyList<OverviewArea> Members(OverviewSnapshot snapshot, string group)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Areas.Where(a => a.Groups.Contains(group))
            .GroupBy(a => a.Id).Select(g => g.First())
            .OrderBy(a => a.BuildingCode, StringComparer.Ordinal)
            .ThenBy(a => a.Code, StringComparer.Ordinal).ThenBy(a => a.Id).ToArray();
    }

    private sealed class View(int areaId, int buildingId)
    {
        public int AreaId { get; } = areaId;
        public int BuildingId { get; } = buildingId;
        public string AreaCode { get; private set; } = "";
        public string AreaName { get; private set; } = "";
        public string BuildingCode { get; private set; } = "";
        public string BuildingName { get; private set; } = "";
        public DateTime Activity { get; private set; } = DateTime.MinValue;
        public long Revision { get; private set; }
        public bool IsCurrentOwner { get; set; }
        public List<(TimeOnly Start, int PointId, OverviewPoint Point)> Points { get; } = [];
        public List<OverviewRoundResult> History { get; } = [];

        public void Touch(string areaCode, string areaName, string buildingCode, string buildingName,
            DateTime activity, long revision)
        {
            if (activity < Activity || (activity == Activity && revision < Revision)) return;
            AreaCode = areaCode;
            AreaName = areaName;
            BuildingCode = buildingCode;
            BuildingName = buildingName;
            Activity = activity;
            Revision = revision;
        }
    }

    private static List<View> Views(TargetTimeline targets, TargetImage? image,
        IReadOnlyList<(RoundObligation Round, OverviewPoint Point)> current,
        IReadOnlyList<OverviewRoundResult> history, IReadOnlySet<int> currentPointIds,
        ShiftSlot slot, DateTime at)
    {
        var views = new Dictionary<(int AreaId, int BuildingId), View>();
        View Get(int areaId, int buildingId)
        {
            if (!views.TryGetValue((areaId, buildingId), out var view))
                views[(areaId, buildingId)] = view = new(areaId, buildingId);
            return view;
        }

        if (image is not null)
        {
            var buildings = image.Buildings.Where(b => b.IsActive && b.CreatedAt <= at).ToDictionary(b => b.Id);
            foreach (var area in image.Areas.Where(a => a.IsActive && a.CreatedAt <= at))
            {
                if (!buildings.TryGetValue(area.BuildingId, out var building)) continue;
                var view = Get(area.Id, area.BuildingId);
                view.IsCurrentOwner = true;
                view.Touch(area.Code, area.Name, building.Code, building.Name, DateTime.MinValue, 0);
            }
        }

        foreach (var round in targets.Rounds)
        {
            var view = Get(round.AreaId, round.BuildingId);
            view.Touch(round.AreaCode, round.AreaName, round.BuildingCode, round.BuildingName,
                round.ActiveFromUtc, round.ActivationRevisionId);
        }

        foreach (var (round, point) in current)
            Get(round.AreaId, round.BuildingId).Points.Add((round.Start, round.PointId, point));

        foreach (var result in history)
            Get(result.Round.AreaId, result.Round.BuildingId).History.Add(result);

        if (image is not null)
        {
            foreach (var window in UpcomingWindows(image, slot, at))
            {
                if (currentPointIds.Contains(window.PointId)) continue;
                var point = image.Points.First(p => p.Id == window.PointId);
                var area = image.Areas.First(a => a.Id == point.AreaId);
                Get(area.Id, area.BuildingId).Points.Add((window.Start, point.Id,
                    new OverviewPoint(point.Id, false, false, false, false, false, false, true, null, null)));
            }
        }

        return [.. views.Values];
    }

    private static OverviewArea Row(View view, IReadOnlyList<OverviewRoundResult> history,
        IReadOnlySet<int> entered, IReadOnlySet<int> assigned, IReadOnlySet<int> nextRoundAreas,
        bool includeStructure, bool originsIncomplete)
    {
        var points = view.Points.OrderBy(p => p.Start).ThenBy(p => p.PointId).Select(p => p.Point).ToArray();
        var work = OverviewRules.Work(points);
        var entry = OverviewRules.Entry(view.AreaId, entered, assigned);
        var upcoming = includeStructure && work.DuePoints == 0 && nextRoundAreas.Contains(view.AreaId);
        var noRounds = includeStructure && work.DuePoints == 0 && !upcoming;
        return new(view.AreaId, view.BuildingId, view.AreaCode, view.AreaName,
            view.BuildingCode, view.BuildingName, entry, work, points, history,
            Groups(entry, work, history, upcoming, noRounds, includeStructure), originsIncomplete);
    }

    private static HashSet<string> Groups(string entry, AreaWork work,
        IReadOnlyList<OverviewRoundResult> history, bool upcoming, bool noRounds, bool includeStructure)
    {
        var groups = new HashSet<string>(StringComparer.Ordinal);
        // Staffing and structural groups describe required current structures only; an observed historical owner still
        // carries its work and missed-round groups without inflating the entry or no-round denominators.
        if (includeStructure)
        {
            groups.Add(entry);
            if (entry != OverviewGroupKeys.Entered) groups.Add(OverviewGroupKeys.NotEntered);
        }
        if (work.Submitted) groups.Add(OverviewGroupKeys.Submitted);
        if (work.Inspected) groups.Add(OverviewGroupKeys.Inspected);
        if (work.Incomplete) groups.Add(OverviewGroupKeys.Incomplete);
        if (work.PendingInspection) groups.Add(OverviewGroupKeys.PendingInspection);
        if (work.NoSubmission) groups.Add(OverviewGroupKeys.NoSubmission);
        if (work.Rework) groups.Add(OverviewGroupKeys.Rework);
        if (work.Issue) groups.Add(OverviewGroupKeys.Issue);
        if (work.Overdue) groups.Add(OverviewGroupKeys.Overdue);
        if (upcoming) groups.Add(OverviewGroupKeys.Upcoming);
        if (noRounds) groups.Add(OverviewGroupKeys.NoRounds);
        // An ambiguous round is not a proven miss: the user sees the partial marker and the retained candidate scans.
        if (history.Any(h => !h.IsPartial && h.Outcome == OverviewOutcomes.NotDone))
            groups.Add(OverviewGroupKeys.MissedCleaning);
        // Rework unfinished is not an inspection miss: the Supervisor did inspect it.
        if (history.Any(h => !h.IsPartial && h.Outcome == OverviewOutcomes.NotInspected))
            groups.Add(OverviewGroupKeys.MissedInspection);
        return groups;
    }

    /// <summary>
    /// Current work is the latest episode of each point. A closed slot keeps its final round as current work while also
    /// listing it in history; a receiving slot's still-editable final round is history-silent.
    /// </summary>
    private static List<(RoundObligation Round, OverviewPoint Point)> CurrentPoints(ShiftSlot slot,
        OverviewMoment moment, IReadOnlyList<RoundObligation> rounds,
        IReadOnlyList<OverviewSubmission> scans, IReadOnlyList<OverviewInspection> inspections)
    {
        var current = rounds
            .Where(r => r.FinalizedAtUtc is null || r.FinalizedReason == "slotClosed")
            .GroupBy(r => r.PointId)
            .Select(g => g.OrderBy(r => r.ActiveFromUtc).ThenBy(r => r.ActivationRevisionId).Last());
        return [.. current.Select(round => (round,
            OverviewRules.Latest(slot, round, scans, inspections, moment.EvaluationUtc)))];
    }

    private static List<OverviewRoundResult> History(ShiftSlot slot, IReadOnlyList<RoundObligation> rounds,
        IReadOnlyList<OverviewSubmission> scans, IReadOnlyList<OverviewInspection> inspections,
        IReadOnlyList<AmbiguousOrigin> ambiguous)
    {
        var history = new List<OverviewRoundResult>();
        foreach (var round in rounds.Where(r => r.FinalizedAtUtc is not null)
            .OrderBy(r => r.ActiveFromUtc).ThenBy(r => r.PointId))
        {
            var point = OverviewRules.Latest(slot, round, scans, inspections, round.FinalizedAtUtc!.Value);
            var unresolved = ambiguous
                .Where(a => a.Rounds.Contains(round))
                .Select(a => a.Scan).DistinctBy(s => s.Id).ToArray();
            var outcome = unresolved.Length > 0 && !point.IsSubmitted ? OverviewOutcomes.Unresolved
                : !point.IsSubmitted ? OverviewOutcomes.NotDone
                : !point.IsInspected ? OverviewOutcomes.NotInspected
                : point.NeedsRework ? OverviewOutcomes.NotReworked
                : OverviewOutcomes.Passed;
            history.Add(new(OverviewTimeline.RoundKey(slot, round), round, outcome,
                point.SubmissionId, point.InspectionId, unresolved.Length > 0,
                unresolved.Length > 0 ? unresolved : null));
        }
        return history;
    }

    /// <summary>A legacy scan that could belong to more than one round, together with every candidate round.</summary>
    private sealed record AmbiguousOrigin(OverviewSubmission Scan, IReadOnlyList<RoundObligation> Rounds);

    /// <summary>
    /// A legacy scan with neither a copied origin nor its window link joins a round only when the point, slot, copied
    /// times and active intervals leave exactly one origin. A scan with several candidate origins is retained as an
    /// <see cref="AmbiguousOrigin"/> so the affected rounds become partial instead of silently reading as missed.
    /// </summary>
    private static (List<OverviewSubmission> Resolved, List<AmbiguousOrigin> Ambiguous) ResolveOrigins(
        IReadOnlyList<OverviewSubmission> scans, ShiftSlot slot, IReadOnlyList<RoundObligation> rounds)
    {
        var resolved = new List<OverviewSubmission>(scans.Count);
        var ambiguous = new List<AmbiguousOrigin>();
        foreach (var scan in scans)
        {
            if ((scan.OriginId ?? scan.ExistingWindowId) is not null)
            {
                resolved.Add(scan);
                continue;
            }

            var candidates = scan.ShiftDate == slot.ShiftDate && scan.Shift == slot.Shift
                && scan.RoundStart is { } start && scan.RoundEnd is { } end
                ? rounds.Where(r => r.PointId == scan.PointId && r.Start == start && r.End == end
                        && r.ActiveFromUtc <= scan.SubmittedAt
                        && (r.FinalizedAtUtc is null || scan.SubmittedAt <= r.FinalizedAtUtc))
                    .ToArray()
                : [];
            var origins = candidates.Select(r => r.OriginId).Distinct().ToArray();
            if (origins.Length == 1)
            {
                resolved.Add(scan with { OriginId = origins[0] });
                continue;
            }
            if (origins.Length > 1) ambiguous.Add(new(scan, candidates));
            resolved.Add(scan);
        }
        return (resolved, ambiguous);
    }

    /// <summary>The eligible windows whose start has not passed: a next configured round for an Area with no due point.</summary>
    private static HashSet<int> AreasWithNextRound(TargetImage? image, ShiftSlot slot, DateTime at)
    {
        var areas = new HashSet<int>();
        foreach (var window in UpcomingWindows(image, slot, at))
        {
            if (image!.Points.FirstOrDefault(p => p.Id == window.PointId) is { } point)
                areas.Add(point.AreaId);
        }
        return areas;
    }

    private static IEnumerable<TargetWindow> UpcomingWindows(TargetImage? image, ShiftSlot slot, DateTime at)
    {
        if (image is null) yield break;
        var buildings = image.Buildings.Where(b => b.IsActive && b.CreatedAt <= at).ToDictionary(b => b.Id);
        var areas = image.Areas.Where(a => a.IsActive && a.CreatedAt <= at).ToDictionary(a => a.Id);
        var points = image.Points.Where(p => p.IsActive && p.CreatedAt <= at).ToDictionary(p => p.Id);
        foreach (var window in image.Windows.Where(w => w.Shift == slot.Shift && w.CreatedAt <= at)
            .OrderBy(w => w.Start).ThenBy(w => w.Id))
        {
            if (!points.TryGetValue(window.PointId, out var point)) continue;
            if (!areas.TryGetValue(point.AreaId, out var area)) continue;
            if (!buildings.ContainsKey(area.BuildingId)) continue;
            if (slot.Shift == Shift.Night && area.ShiftPattern != ShiftPattern.DayAndNight) continue;
            if (ShiftCalendar.ToUtc(slot, window.Start) > at) yield return window;
        }
    }
}
