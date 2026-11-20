using System.Globalization;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Overview;

public static class OverviewTimeline
{
    public static TargetTimeline Build(ShiftSlot slot, OverviewMoment moment, IReadOnlyList<TargetRevision> revisions)
    {
        var start = ShiftCalendar.StartUtc(slot);
        var evaluation = moment.EvaluationUtc;
        var ordered = OverviewTime.OrderRevisions(revisions, evaluation);
        var rounds = new List<RoundObligation>();
        var active = new Dictionary<int, RoundObligation>();
        var baseline = Array.FindLastIndex(ordered, r => r.EffectiveAt <= start);
        for (var i = Math.Max(baseline, 0); i < ordered.Length && start <= evaluation; i++)
        {
            var revision = ordered[i];
            var from = revision.EffectiveAt > start ? revision.EffectiveAt : start;
            var nextRevisionAt = i + 1 < ordered.Length ? ordered[i + 1].EffectiveAt : (DateTime?)null;
            SweepRevision(slot, revision, from, nextRevisionAt, evaluation, active, rounds);
        }

        foreach (var round in active.Values)
        {
            rounds.Add(moment is { Phase: "closed", CloseUtc: { } close }
                ? round with { FinalizedAtUtc = close, FinalizedReason = "slotClosed" }
                : round);
        }

        return DescribeCoverage(slot, moment, ordered) with
        {
            Rounds = rounds,
            StructuralGaps = StructuralGaps(revisions).ToArray(),
        };
    }

    internal static TargetTimeline DescribeCoverage(ShiftSlot slot, OverviewMoment moment, TargetRevision[] ordered)
    {
        var current = ordered.LastOrDefault(r => r.EffectiveAt <= moment.EvaluationUtc);
        var hasBaseline = ordered.Length > 0 && ordered[0].EffectiveAt <= ShiftCalendar.StartUtc(slot);
        var currentComplete = current is not null && moment.PolicyComplete;
        return new(current?.Image, current?.Id, [], currentComplete, currentComplete && hasBaseline)
        {
            Slot = slot,
            ReceivingHistory = ordered,
        };
    }

    internal static IEnumerable<(DateTime From, DateTime To)> StructuralGaps(IEnumerable<TargetRevision> revisions)
    {
        foreach (var revision in revisions)
        {
            if (revision.Image.CoverageGapFromUtc is { } from && from < revision.EffectiveAt)
                yield return (from, revision.EffectiveAt);
        }
    }

    private sealed record Candidate(TargetBuilding Building, TargetArea Area, TargetWindow Window,
        DateTime StartUtc, DateTime ActivationUtc);

    // A revision owns [from, nextRevisionAt). Evaluation itself is inclusive.
    private static void SweepRevision(ShiftSlot slot, TargetRevision revision, DateTime from,
        DateTime? nextRevisionAt, DateTime evaluation, Dictionary<int, RoundObligation> active,
        List<RoundObligation> rounds)
    {
        var candidates = Candidates(slot, revision.Image, from)
            .Where(c => c.ActivationUtc <= evaluation && (nextRevisionAt is null || c.ActivationUtc < nextRevisionAt))
            .OrderByDescending(c => c.StartUtc).ThenByDescending(c => c.Window.Id).ToArray();
        var byPoint = candidates.ToLookup(c => c.Window.PointId);
        var activations = candidates.ToLookup(c => c.ActivationUtc, c => c.Window.PointId);
        foreach (var at in activations.Select(g => g.Key).Append(from).Distinct().Order())
        {
            // At a revision boundary previously active windows may have disappeared.
            var points = at == from ? activations[at].Union(active.Keys).ToArray() : activations[at].Distinct();
            foreach (var pointId in points)
            {
                var eligible = byPoint[pointId].Where(c => c.ActivationUtc <= at);
                var winner = eligible.FirstOrDefault();
                if (active.TryGetValue(pointId, out var prior))
                {
                    if (winner != null && Matches(prior, winner.Window))
                    {
                        active[pointId] = prior with
                        {
                            BuildingId = winner.Building.Id, BuildingCode = winner.Building.Code, BuildingName = winner.Building.Name,
                            AreaId = winner.Area.Id, AreaCode = winner.Area.Code, AreaName = winner.Area.Name,
                        };
                        continue;
                    }

                    var reason = eligible.Any(c => Matches(prior, c.Window)) ? "superseded" : "configurationChanged";
                    rounds.Add(prior with { FinalizedAtUtc = at, FinalizedReason = reason });
                    active.Remove(pointId);
                }
                if (winner != null)
                    active[pointId] = new(pointId, winner.Area.Id, winner.Building.Id, winner.Area.Code,
                        winner.Area.Name, winner.Building.Code, winner.Building.Name, winner.Window.Id,
                        winner.Window.Start, winner.Window.End, at, null, revision.Id, null);
            }
        }
    }

    private static IEnumerable<Candidate> Candidates(ShiftSlot slot, TargetImage image, DateTime intervalStart)
    {
        var buildings = image.Buildings.Where(b => b.IsActive).ToDictionary(b => b.Id);
        var areas = image.Areas
            .Where(a => a.IsActive && (slot.Shift == Shift.Day || a.ShiftPattern == ShiftPattern.DayAndNight))
            .ToDictionary(a => a.Id);
        var points = image.Points.Where(p => p.IsActive).ToDictionary(p => p.Id);
        var results = new List<Candidate>();
        foreach (var window in image.Windows)
        {
            if (window.Shift != slot.Shift) continue;
            if (!points.TryGetValue(window.PointId, out var point)) continue;
            if (!areas.TryGetValue(point.AreaId, out var area)) continue;
            if (!buildings.TryGetValue(area.BuildingId, out var building)) continue;
            var start = ShiftCalendar.ToUtc(slot, window.Start);
            var activation = MaxUtc(intervalStart, building.CreatedAt, area.CreatedAt, point.CreatedAt, window.CreatedAt, start);
            results.Add(new Candidate(building, area, window, start, activation));
        }
        return results;
    }

    private static DateTime MaxUtc(DateTime a, DateTime b, DateTime c, DateTime d, DateTime e, DateTime f)
    {
        var max = a > b ? a : b;
        if (c > max) max = c;
        if (d > max) max = d;
        if (e > max) max = e;
        return f > max ? f : max;
    }

    private static bool Matches(RoundObligation round, TargetWindow window) =>
        round.OriginId == window.Id && round.Start == window.Start && round.End == window.End;

    public static string RoundKey(ShiftSlot slot, RoundObligation round) =>
        FormattableString.Invariant($"{slot.ShiftDate:yyyy-MM-dd}:{slot.Shift}:{round.PointId}:{round.OriginId}:{round.ActivationRevisionId}:{round.Start:HHmm}-{round.End:HHmm}");

    public static RoundObligation? FindRound(TargetTimeline timeline, string key)
    {
        if (timeline.Slot is not { } slot || string.IsNullOrEmpty(key)) return null;
        var parts = key.Split(':');
        var culture = CultureInfo.InvariantCulture;
        if (parts.Length != 6 || !DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", culture, DateTimeStyles.None, out var date)
            || date != slot.ShiftDate || parts[1] != slot.Shift.ToString()
            || !int.TryParse(parts[2], NumberStyles.None, culture, out var pointId)
            || !int.TryParse(parts[3], NumberStyles.None, culture, out var originId)
            || !long.TryParse(parts[4], NumberStyles.None, culture, out var revisionId))
            return null;
        var times = parts[5].Split('-');
        if (times.Length != 2 || !TimeOnly.TryParseExact(times[0], "HHmm", culture, DateTimeStyles.None, out var start)
            || !TimeOnly.TryParseExact(times[1], "HHmm", culture, DateTimeStyles.None, out var end))
            return null;
        return timeline.Rounds.FirstOrDefault(r => r.PointId == pointId && r.OriginId == originId
            && r.ActivationRevisionId == revisionId && r.Start == start && r.End == end && RoundKey(slot, r) == key);
    }
}
