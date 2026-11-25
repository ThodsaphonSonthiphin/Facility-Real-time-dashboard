using JanitorialManagementDashboard.Application.Rounds;
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

        var prepared = Prepare(slot, moment, targets, facts);
        var views = prepared.Views;
        var areas = buildingId is null
            ? ProjectWholeFactory(slot.Shift, views, prepared.Entered, prepared.Assigned, prepared.NextRoundAreas, prepared.AmbiguousViews)
            : ProjectForBuilding(slot.Shift, views, buildingId.Value, prepared.Entered, prepared.Assigned, prepared.NextRoundAreas, prepared.AmbiguousViews);

        return new(slot, moment, targets, availability, areas);
    }

    public static (OverviewSnapshot Whole, IReadOnlyDictionary<int, IReadOnlyList<OverviewArea>> ByBuilding) ProjectAll(
        ShiftSlot slot,
        OverviewMoment moment,
        TargetTimeline targets,
        OverviewFacts facts,
        SectionAvailability wholeAvailability)
    {
        var prepared = Prepare(slot, moment, targets, facts);
        var byBuilding = new Dictionary<int, List<OverviewArea>>();
        var groupedByArea = prepared.Views.GroupBy(v => v.AreaId);
        var wholeAreas = new List<OverviewArea>();

        foreach (var group in groupedByArea)
        {
            View? single = null;
            List<View>? many = null;
            foreach (var v in group)
            {
                if (single is null && many is null)
                {
                    single = v;
                }
                else
                {
                    if (many is null)
                    {
                        many = [single!, v];
                        single = null;
                    }
                    else
                    {
                        many.Add(v);
                    }
                }
            }

            if (single is not null)
            {
                var row = Row(
                    slot.Shift,
                    single,
                    single.History.Count <= 1 ? single.History : [.. single.History.OrderBy(h => h.Round.ActiveFromUtc).ThenBy(h => h.Round.PointId)],
                    prepared.Entered,
                    prepared.Assigned,
                    prepared.NextRoundAreas,
                    single.IsCurrentOwner,
                    prepared.AmbiguousViews.Contains((single.AreaId, single.BuildingId)));
                wholeAreas.Add(row);
                AddBuildingArea(byBuilding, single.BuildingId, row);
            }
            else
            {
                var all = many!.ToArray();
                var primary = all.FirstOrDefault(v => v.Points.Count > 0)
                    ?? all.FirstOrDefault(v => v.IsCurrentOwner)
                    ?? all.OrderByDescending(v => v.Activity).ThenByDescending(v => v.Revision).First();
                var merged = all.SelectMany(v => v.History)
                    .OrderBy(h => h.Round.ActiveFromUtc).ThenBy(h => h.Round.PointId).ToArray();
                wholeAreas.Add(Row(slot.Shift, primary, merged, prepared.Entered, prepared.Assigned, prepared.NextRoundAreas,
                    includeStructure: all.Any(v => v.IsCurrentOwner),
                    originsIncomplete: all.Any(v => prepared.AmbiguousViews.Contains((v.AreaId, v.BuildingId)))));

                foreach (var view in all)
                {
                    var row = Row(
                        slot.Shift,
                        view,
                        view.History.Count <= 1 ? view.History : [.. view.History.OrderBy(h => h.Round.ActiveFromUtc).ThenBy(h => h.Round.PointId)],
                        prepared.Entered,
                        prepared.Assigned,
                        prepared.NextRoundAreas,
                        view.IsCurrentOwner,
                        prepared.AmbiguousViews.Contains((view.AreaId, view.BuildingId)));
                    AddBuildingArea(byBuilding, view.BuildingId, row);
                }
            }
        }

        SortAreas(wholeAreas);
        var sortedByBuilding = new Dictionary<int, IReadOnlyList<OverviewArea>>(byBuilding.Count);
        foreach (var (bId, bAreas) in byBuilding)
        {
            SortAreas(bAreas);
            sortedByBuilding[bId] = bAreas;
        }

        return (new(slot, moment, targets, wholeAvailability, wholeAreas), sortedByBuilding);
    }

    private static void AddBuildingArea(Dictionary<int, List<OverviewArea>> byBuilding, int buildingId, OverviewArea row)
    {
        if (!byBuilding.TryGetValue(buildingId, out var list))
        {
            byBuilding[buildingId] = list = [];
        }
        list.Add(row);
    }

    private sealed record PreparedProjection(
        List<View> Views,
        IReadOnlySet<int> Entered,
        IReadOnlySet<int> Assigned,
        HashSet<int> NextRoundAreas,
        HashSet<(int AreaId, int BuildingId)> AmbiguousViews);

    private static PreparedProjection Prepare(
        ShiftSlot slot, OverviewMoment moment, TargetTimeline targets, OverviewFacts facts)
    {
        var origins = ResolveOrigins(facts.Submissions, slot, targets.Rounds);
        var scansByPoint = origins.Resolved.ToLookup(s => s.PointId);
        var inspectionsBySubmission = facts.Inspections.ToLookup(i => i.SubmissionId);

        var current = CurrentPoints(slot, moment, targets.Rounds, scansByPoint, inspectionsBySubmission);
        var history = History(slot, targets.Rounds, scansByPoint, inspectionsBySubmission, origins.Ambiguous);
        var image = targets.Current;
        var entered = ScheduleEntryEvidence.EnteredAreas(
            new ScheduleSlot(slot.ShiftDate, slot.Shift), facts.EntryEvidence);
        var (nextRoundAreas, upcomingWithPoints) = UpcomingData(image, slot, moment.EvaluationUtc);
        var currentPointIds = current.Select(c => c.Round.PointId).ToHashSet();
        var ambiguousViews = origins.Ambiguous
            .SelectMany(a => a.Rounds).Select(r => (r.AreaId, r.BuildingId)).ToHashSet();

        var views = Views(targets, image, current, history, currentPointIds, upcomingWithPoints, scansByPoint, slot, moment.EvaluationUtc);
        return new(views, entered, facts.AssignedAreas, nextRoundAreas, ambiguousViews);
    }

    private static List<OverviewArea> ProjectWholeFactory(
        Shift shift,
        List<View> views,
        IReadOnlySet<int> entered,
        IReadOnlySet<int> assigned,
        HashSet<int> nextRoundAreas,
        HashSet<(int AreaId, int BuildingId)> ambiguousViews)
    {
        var areas = new List<OverviewArea>();
        foreach (var group in views.GroupBy(v => v.AreaId))
        {
            var all = group.ToArray();
            var primary = all.FirstOrDefault(v => v.Points.Count > 0)
                ?? all.FirstOrDefault(v => v.IsCurrentOwner)
                ?? all.OrderByDescending(v => v.Activity).ThenByDescending(v => v.Revision).First();
            var merged = all.SelectMany(v => v.History)
                .OrderBy(h => h.Round.ActiveFromUtc).ThenBy(h => h.Round.PointId).ToArray();
            areas.Add(Row(shift, primary, merged, entered, assigned, nextRoundAreas,
                includeStructure: all.Any(v => v.IsCurrentOwner),
                originsIncomplete: all.Any(v => ambiguousViews.Contains((v.AreaId, v.BuildingId)))));
        }

        SortAreas(areas);
        return areas;
    }

    private static List<OverviewArea> ProjectForBuilding(
        Shift shift,
        List<View> views,
        int buildingId,
        IReadOnlySet<int> entered,
        IReadOnlySet<int> assigned,
        HashSet<int> nextRoundAreas,
        HashSet<(int AreaId, int BuildingId)> ambiguousViews)
    {
        var areas = new List<OverviewArea>();
        foreach (var view in views)
        {
            if (view.BuildingId != buildingId) continue;
            areas.Add(Row(shift, view, view.History
                .OrderBy(h => h.Round.ActiveFromUtc).ThenBy(h => h.Round.PointId).ToArray(),
                entered, assigned, nextRoundAreas, view.IsCurrentOwner,
                ambiguousViews.Contains((view.AreaId, view.BuildingId))));
        }

        SortAreas(areas);
        return areas;
    }

    private static void SortAreas(List<OverviewArea> areas)
    {
        areas.Sort((left, right) =>
        {
            var building = string.CompareOrdinal(left.BuildingCode, right.BuildingCode);
            if (building != 0) return building;
            var code = string.CompareOrdinal(left.Code, right.Code);
            return code != 0 ? code : left.Id.CompareTo(right.Id);
        });
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
        public List<(TimeOnly? Start, int PointId, OverviewPoint Point)> Points { get; } = [];
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
        IReadOnlyList<(TargetWindow Window, TargetPoint Point, TargetArea Area)> upcomingWithPoints,
        ILookup<int, OverviewSubmission> scansByPoint,
        ShiftSlot slot, DateTime at)
    {
        var views = new Dictionary<(int AreaId, int BuildingId), View>();
        View Get(int areaId, int buildingId)
        {
            if (!views.TryGetValue((areaId, buildingId), out var view))
                views[(areaId, buildingId)] = view = new(areaId, buildingId);
            return view;
        }

        Dictionary<int, TargetArea>? activeImageAreas = null;
        if (image is not null)
        {
            var buildings = image.Buildings.Where(b => b.IsActive && b.CreatedAt <= at).ToDictionary(b => b.Id);
            activeImageAreas = new Dictionary<int, TargetArea>();
            foreach (var area in image.Areas.Where(a => a.IsActive && a.CreatedAt <= at
                && (slot.Shift == Shift.Day || a.ShiftPattern == ShiftPattern.DayAndNight)))
            {
                if (!buildings.TryGetValue(area.BuildingId, out var building)) continue;
                activeImageAreas[area.Id] = area;
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

        var upcomingPointIds = new HashSet<int>();
        foreach (var (window, point, area) in upcomingWithPoints)
        {
            if (currentPointIds.Contains(window.PointId)) continue;
            upcomingPointIds.Add(point.Id);
            var isIssue = OverviewRules.IsIssueAt(scansByPoint[point.Id], at);
            Get(area.Id, area.BuildingId).Points.Add((window.Start, point.Id,
                new OverviewPoint(point.Id, false, false, false, false, isIssue, false, true, null, null)));
        }

        // Active points with no due or upcoming rounds in this shift still carry an unresolved Issue from their latest scan.
        if (image is not null && activeImageAreas is not null)
        {
            foreach (var point in image.Points.Where(p => p.IsActive && p.CreatedAt <= at))
            {
                if (currentPointIds.Contains(point.Id) || upcomingPointIds.Contains(point.Id)) continue;
                if (!activeImageAreas.TryGetValue(point.AreaId, out var area)) continue;
                if (!OverviewRules.IsIssueAt(scansByPoint[point.Id], at)) continue;
                Get(area.Id, area.BuildingId).Points.Add((null, point.Id,
                    new OverviewPoint(point.Id, false, false, false, false, true, false, false, null, null)));
            }
        }

        return [.. views.Values];
    }

    private static OverviewArea Row(Shift shift, View view, IReadOnlyList<OverviewRoundResult> history,
        IReadOnlySet<int> entered, IReadOnlySet<int> assigned, IReadOnlySet<int> nextRoundAreas,
        bool includeStructure, bool originsIncomplete)
    {
        var points = view.Points
            .OrderBy(p => p.Start is { } s ? RoundWindowRules.MinutesIntoShift(shift, s) : int.MaxValue)
            .ThenBy(p => p.PointId)
            .Select(p => p.Point)
            .ToArray();
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
        ILookup<int, OverviewSubmission> scansByPoint, ILookup<long, OverviewInspection> inspectionsBySubmission)
    {
        var current = rounds
            .Where(r => r.FinalizedAtUtc is null || r.FinalizedReason == "slotClosed")
            .GroupBy(r => r.PointId)
            .Select(g => g.OrderBy(r => r.ActiveFromUtc).ThenBy(r => r.ActivationRevisionId).Last());
        return [.. current.Select(round => (round,
            OverviewRules.LatestForPoint(
                slot, round, scansByPoint[round.PointId], id => inspectionsBySubmission[id], moment.EvaluationUtc)))];
    }

    private static List<OverviewRoundResult> History(ShiftSlot slot, IReadOnlyList<RoundObligation> rounds,
        ILookup<int, OverviewSubmission> scansByPoint, ILookup<long, OverviewInspection> inspectionsBySubmission,
        IReadOnlyList<AmbiguousOrigin> ambiguous)
    {
        Dictionary<RoundObligation, HashSet<OverviewSubmission>>? ambiguousByRound = null;
        if (ambiguous.Count > 0)
        {
            ambiguousByRound = new Dictionary<RoundObligation, HashSet<OverviewSubmission>>();
            foreach (var entry in ambiguous)
            {
                foreach (var candidateRound in entry.Rounds)
                {
                    if (!ambiguousByRound.TryGetValue(candidateRound, out var set))
                    {
                        ambiguousByRound[candidateRound] = set = [];
                    }
                    set.Add(entry.Scan);
                }
            }
        }

        var history = new List<OverviewRoundResult>();
        foreach (var round in rounds.Where(r => r.FinalizedAtUtc is not null)
            .OrderBy(r => r.ActiveFromUtc).ThenBy(r => r.PointId))
        {
            var point = OverviewRules.LatestForPoint(
                slot, round, scansByPoint[round.PointId], id => inspectionsBySubmission[id], round.FinalizedAtUtc!.Value);
            var unresolved = ambiguousByRound is not null && ambiguousByRound.TryGetValue(round, out var matches)
                ? matches.OrderBy(s => s.Id).ToArray()
                : [];
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
        ILookup<(int PointId, TimeOnly Start, TimeOnly End), RoundObligation>? roundsByKey = null;

        foreach (var scan in scans)
        {
            if ((scan.OriginId ?? scan.ExistingWindowId) is not null)
            {
                resolved.Add(scan);
                continue;
            }

            if (scan.ShiftDate != slot.ShiftDate || scan.Shift != slot.Shift
                || scan.RoundStart is not { } start || scan.RoundEnd is not { } end)
            {
                resolved.Add(scan);
                continue;
            }

            roundsByKey ??= rounds.ToLookup(r => (r.PointId, r.Start, r.End));
            var candidates = roundsByKey[(scan.PointId, start, end)]
                .Where(r => r.ActiveFromUtc <= scan.SubmittedAt
                    && (r.FinalizedAtUtc is null || scan.SubmittedAt <= r.FinalizedAtUtc))
                .ToArray();
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
    private static (HashSet<int> Areas, List<(TargetWindow Window, TargetPoint Point, TargetArea Area)> Windows) UpcomingData(
        TargetImage? image, ShiftSlot slot, DateTime at)
    {
        var areas = new HashSet<int>();
        var list = new List<(TargetWindow Window, TargetPoint Point, TargetArea Area)>();
        if (image is null) return (areas, list);

        var buildings = image.Buildings.Where(b => b.IsActive && b.CreatedAt <= at).ToDictionary(b => b.Id);
        var activeAreas = image.Areas.Where(a => a.IsActive && a.CreatedAt <= at).ToDictionary(a => a.Id);
        var points = image.Points.Where(p => p.IsActive && p.CreatedAt <= at).ToDictionary(p => p.Id);
        foreach (var window in image.Windows.Where(w => w.Shift == slot.Shift && w.CreatedAt <= at)
            .OrderBy(w => RoundWindowRules.MinutesIntoShift(slot.Shift, w.Start)).ThenBy(w => w.Id))
        {
            if (!points.TryGetValue(window.PointId, out var point)) continue;
            if (!activeAreas.TryGetValue(point.AreaId, out var area)) continue;
            if (!buildings.ContainsKey(area.BuildingId)) continue;
            if (slot.Shift == Shift.Night && area.ShiftPattern != ShiftPattern.DayAndNight) continue;
            if (ShiftCalendar.ToUtc(slot, window.Start) > at)
            {
                areas.Add(area.Id);
                list.Add((window, point, area));
            }
        }

        return (areas, list);
    }
}
