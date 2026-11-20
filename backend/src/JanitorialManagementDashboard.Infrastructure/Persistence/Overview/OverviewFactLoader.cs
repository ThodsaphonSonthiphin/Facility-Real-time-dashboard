using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Overview;

internal sealed record OverviewUnverifiedArea(
    int AreaId,
    int BuildingId,
    string Code,
    string Name,
    string BuildingCode,
    string BuildingName);

internal sealed record OverviewLoadedFacts(
    OverviewFacts Facts,
    IReadOnlySet<int> ObservedSlotAreaIds,
    IReadOnlySet<int> ObservedSlotPointIds,
    IReadOnlySet<int> UnresolvedOriginPointIds,
    IReadOnlyDictionary<int, int> PointAreaMap,
    IReadOnlyDictionary<int, OverviewAreaResponsibility> Responsibilities,
    IReadOnlyDictionary<int, DateTime> FirstScanAtByPoint,
    IReadOnlyDictionary<int, OverviewPointMetadata> UnverifiedPointMetadata,
    IReadOnlyDictionary<int, OverviewUnverifiedArea> UnverifiedAreaMetadata,
    IReadOnlyDictionary<int, DateTime> LatestSubmissionAtByPoint);

public static class OverviewFactLoader
{
    public static async Task<OverviewFacts> LoadAsync(
        AppDbContext db,
        ShiftSlot slot,
        OverviewMoment moment,
        TargetTimeline timeline,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(timeline);

        var loaded = await LoadDetailedAsync(
            db,
            new ScheduleStore(db),
            slot,
            moment,
            timeline,
            moment.EvaluationUtc,
            new RetentionSettings(),
            ct);
        return loaded.Facts;
    }

    internal static async Task<OverviewLoadedFacts> LoadDetailedAsync(
        AppDbContext db,
        ScheduleStore store,
        ShiftSlot slot,
        OverviewMoment moment,
        TargetTimeline timeline,
        DateTime readAtUtc,
        RetentionSettings retention,
        CancellationToken ct)
    {
        var evaluationUtc = moment.EvaluationUtc;
        var pointAreaMap = new Dictionary<int, int>();
        var targetPointIds = new HashSet<int>();

        if (timeline.Current is not null)
        {
            foreach (var point in timeline.Current.Points)
            {
                targetPointIds.Add(point.Id);
                pointAreaMap[point.Id] = point.AreaId;
            }
        }

        foreach (var round in timeline.Rounds)
        {
            targetPointIds.Add(round.PointId);
            pointAreaMap.TryAdd(round.PointId, round.AreaId);
        }

        var slotScans = await db.ScanRecords.AsNoTracking()
            .Where(s => s.ShiftDate == slot.ShiftDate && s.Shift == slot.Shift && s.SubmittedAt <= evaluationUtc)
            .Select(s => new ScanProjection(
                s.Id,
                s.ServicePointId,
                s.ShiftDate,
                s.Shift,
                s.RoundOriginId,
                s.RoundWindowId,
                s.RoundStart,
                s.RoundEnd,
                s.Placement,
                s.Status,
                s.SubmittedAt,
                s.UserId,
                s.IssueTags,
                s.Note))
            .ToListAsync(ct);

        var observedSlotPointIds = slotScans.Select(s => s.ServicePointId).ToHashSet();
        var allPointIds = targetPointIds.Union(observedSlotPointIds).ToList();
        var allPointSet = allPointIds.ToHashSet();

        Dictionary<int, DateTime>? precomputedFirstScanAt = null;
        List<ScanProjection> extraIssueScans = [];
        if (evaluationUtc == readAtUtc)
        {
            var pointBounds = await db.ScanRecords.AsNoTracking()
                .Where(s => s.SubmittedAt <= readAtUtc)
                .GroupBy(s => s.ServicePointId)
                .Select(g => new
                {
                    ServicePointId = g.Key,
                    FirstSubmittedAt = g.Min(s => s.SubmittedAt),
                    MaxSubmittedAt = g.Max(s => s.SubmittedAt),
                })
                .ToListAsync(ct);

            precomputedFirstScanAt = pointBounds.ToDictionary(x => x.ServicePointId, x => Utc(x.FirstSubmittedAt));

            if (allPointSet.Count > 0)
            {
                var maxSlotAtByPoint = new Dictionary<int, DateTime>();
                foreach (var scan in slotScans)
                {
                    var submitted = Utc(scan.SubmittedAt);
                    if (!maxSlotAtByPoint.TryGetValue(scan.ServicePointId, out var existing) || submitted > existing)
                    {
                        maxSlotAtByPoint[scan.ServicePointId] = submitted;
                    }
                }

                var stalePointIds = new List<int>();
                foreach (var bound in pointBounds)
                {
                    if (!allPointSet.Contains(bound.ServicePointId)) continue;
                    var maxAt = Utc(bound.MaxSubmittedAt);
                    if (!maxSlotAtByPoint.TryGetValue(bound.ServicePointId, out var slotAt) || slotAt < maxAt)
                    {
                        stalePointIds.Add(bound.ServicePointId);
                    }
                }

                if (stalePointIds.Count > 0)
                {
                    var latestTimes = stalePointIds.Count <= 64
                        ? db.ScanRecords.AsNoTracking()
                            .Where(s => stalePointIds.Contains(s.ServicePointId) && s.SubmittedAt <= evaluationUtc)
                            .GroupBy(s => s.ServicePointId)
                            .Select(g => new { ServicePointId = g.Key, MaxSubmittedAt = g.Max(s => s.SubmittedAt) })
                        : db.ScanRecords.AsNoTracking()
                            .Where(s => s.SubmittedAt <= evaluationUtc)
                            .GroupBy(s => s.ServicePointId)
                            .Select(g => new { ServicePointId = g.Key, MaxSubmittedAt = g.Max(s => s.SubmittedAt) });

                    var staleIds = db.ScanRecords.AsNoTracking()
                        .Join(
                            latestTimes,
                            s => new { s.ServicePointId, s.SubmittedAt },
                            t => new { t.ServicePointId, SubmittedAt = t.MaxSubmittedAt },
                            (s, _) => new { s.ServicePointId, s.Id })
                        .GroupBy(x => x.ServicePointId)
                        .Select(g => g.Max(x => x.Id));

                    var slotScanIds = slotScans.Select(s => s.Id).ToHashSet();
                    var staleSet = stalePointIds.ToHashSet();
                    extraIssueScans = (await db.ScanRecords.AsNoTracking()
                        .Join(
                            staleIds,
                            s => s.Id,
                            id => id,
                            (s, _) => new ScanProjection(
                                s.Id,
                                s.ServicePointId,
                                s.ShiftDate,
                                s.Shift,
                                s.RoundOriginId,
                                s.RoundWindowId,
                                s.RoundStart,
                                s.RoundEnd,
                                s.Placement,
                                s.Status,
                                s.SubmittedAt,
                                s.UserId,
                                s.IssueTags,
                                s.Note))
                        .ToListAsync(ct))
                        .Where(s => staleSet.Contains(s.ServicePointId) && !slotScanIds.Contains(s.Id))
                        .ToList();
                }
            }
        }
        else if (allPointIds.Count > 0)
        {
            var latestTimes = db.ScanRecords.AsNoTracking()
                .Where(s => allPointIds.Contains(s.ServicePointId) && s.SubmittedAt <= evaluationUtc)
                .GroupBy(s => s.ServicePointId)
                .Select(g => new { ServicePointId = g.Key, MaxSubmittedAt = g.Max(s => s.SubmittedAt) });

            var latestIdsQuery = db.ScanRecords.AsNoTracking()
                .Join(
                    latestTimes,
                    s => new { s.ServicePointId, s.SubmittedAt },
                    t => new { t.ServicePointId, SubmittedAt = t.MaxSubmittedAt },
                    (s, _) => new { s.ServicePointId, s.Id })
                .GroupBy(x => x.ServicePointId)
                .Select(g => g.Max(x => x.Id));

            var slotScanIds = slotScans.Select(s => s.Id).ToHashSet();
            extraIssueScans = (await db.ScanRecords.AsNoTracking()
                .Join(
                    latestIdsQuery,
                    s => s.Id,
                    id => id,
                    (s, _) => new ScanProjection(
                        s.Id,
                        s.ServicePointId,
                        s.ShiftDate,
                        s.Shift,
                        s.RoundOriginId,
                        s.RoundWindowId,
                        s.RoundStart,
                        s.RoundEnd,
                        s.Placement,
                        s.Status,
                        s.SubmittedAt,
                        s.UserId,
                        s.IssueTags,
                        s.Note))
                .ToListAsync(ct))
                .Where(s => !slotScanIds.Contains(s.Id))
                .ToList();
        }

        var submissions = slotScans.Concat(extraIssueScans)
            .Select(s => new OverviewSubmission(
                s.Id,
                s.ServicePointId,
                s.ShiftDate,
                s.Shift,
                s.RoundOriginId,
                s.RoundWindowId,
                s.RoundStart,
                s.RoundEnd,
                s.Placement,
                s.Status,
                Utc(s.SubmittedAt),
                s.UserId,
                s.IssueTags,
                s.Note))
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .ToList();

        var slotInspectionRows = slotScans.Count == 0
            ? []
            : await db.InspectionRecords.AsNoTracking()
                .Where(i => i.ScanRecord!.ShiftDate == slot.ShiftDate
                    && i.ScanRecord.Shift == slot.Shift
                    && i.InspectedAt <= evaluationUtc
                    && i.ScanRecord.SubmittedAt <= evaluationUtc)
                .Select(i => new { i.Id, i.ScanRecordId, i.Result, i.InspectedAt, i.SupervisorId, i.Defect })
                .ToListAsync(ct);

        var extraScanIds = extraIssueScans.Select(s => s.Id).Distinct().ToList();
        var extraInspectionRows = extraScanIds.Count == 0
            ? []
            : await db.InspectionRecords.AsNoTracking()
                .Where(i => extraScanIds.Contains(i.ScanRecordId) && i.InspectedAt <= evaluationUtc)
                .Select(i => new { i.Id, i.ScanRecordId, i.Result, i.InspectedAt, i.SupervisorId, i.Defect })
                .ToListAsync(ct);

        var inspectionRows = extraInspectionRows.Count == 0
            ? slotInspectionRows
            : [.. slotInspectionRows, .. extraInspectionRows];
        var inspections = inspectionRows
            .Select(i => new OverviewInspection(
                i.Id,
                i.ScanRecordId,
                i.Result,
                Utc(i.InspectedAt),
                i.SupervisorId,
                i.Defect))
            .OrderBy(i => i.InspectedAt).ThenBy(i => i.Id)
            .ToList();

        var attendanceRows = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift && a.OccurredAt <= evaluationUtc)
            .OrderBy(a => a.OccurredAt).ThenBy(a => a.Id)
            .Select(a => new { a.Id, a.AreaId, a.UserId, a.EventType, a.OccurredAt, a.Source })
            .ToListAsync(ct);
        var attendance = attendanceRows
            .Select(a => new OverviewAttendance(
                a.Id,
                a.AreaId,
                a.UserId,
                a.EventType,
                Utc(a.OccurredAt),
                a.Source != AttendanceSource.Scan,
                a.Source))
            .ToList();

        var coverRows = await db.CoverAssignments.AsNoTracking()
            .Where(c => c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift
                && c.AreaId != null && c.AssignedAt <= evaluationUtc)
            .OrderBy(c => c.AssignedAt).ThenBy(c => c.Id)
            .Select(c => new { c.Id, AreaId = c.AreaId!.Value, c.UserId, c.AssignedAt, c.CancelledAt })
            .ToListAsync(ct);
        var covers = coverRows
            .Select(c => new OverviewCover(
                c.Id,
                c.AreaId,
                c.UserId,
                Utc(c.AssignedAt),
                c.CancelledAt is { } cancelled && Utc(cancelled) <= evaluationUtc ? Utc(cancelled) : null))
            .ToList();

        var rawEntryEvidence = await store.ReadEntryEvidenceAsync(slot, ct);
        var entryEvidence = rawEntryEvidence
            .Where(e => e.RecordedAt.UtcDateTime <= evaluationUtc)
            .OrderBy(e => e.RecordedAt)
            .ToList();

        var assignedAreas = new HashSet<int>();
        foreach (var cover in coverRows)
        {
            if (cover.CancelledAt is null || Utc(cover.CancelledAt.Value) > evaluationUtc)
            {
                assignedAreas.Add(cover.AreaId);
            }
        }

        var reliefRows = await db.ReliefAssignments.AsNoTracking()
            .Where(r => r.ShiftDate == slot.ShiftDate && r.Shift == slot.Shift && r.CreatedAt <= evaluationUtc)
            .Select(r => new { r.AreaId, r.IsCancelled, r.CancelledAt })
            .ToListAsync(ct);
        var activeReliefAreaIds = new HashSet<int>();
        foreach (var relief in reliefRows)
        {
            if (!relief.IsCancelled || (relief.CancelledAt is { } cancelledAt && Utc(cancelledAt) > evaluationUtc))
            {
                assignedAreas.Add(relief.AreaId);
                activeReliefAreaIds.Add(relief.AreaId);
            }
        }

        var responsibilities = await LoadResponsibilitiesAsync(db, slot, evaluationUtc, assignedAreas, ct);

        var personIds = submissions.Select(s => s.UserId)
            .Concat(inspections.Select(i => i.SupervisorId))
            .Concat(attendance.Select(a => a.UserId))
            .Concat(covers.Select(c => c.UserId))
            .Concat(responsibilities.Values.Select(r => r.UserId))
            .Distinct()
            .ToList();

        List<OverviewPerson> people;
        if (personIds.Count == 0)
        {
            people = [];
        }
        else if (personIds.Count <= 64)
        {
            people = await db.Users.AsNoTracking()
                .Where(u => personIds.Contains(u.Id))
                .OrderBy(u => u.Id)
                .Select(u => new OverviewPerson(u.Id, u.DisplayName))
                .ToListAsync(ct);
        }
        else
        {
            var personSet = personIds.ToHashSet();
            people = (await db.Users.AsNoTracking()
                .OrderBy(u => u.Id)
                .Select(u => new OverviewPerson(u.Id, u.DisplayName))
                .ToListAsync(ct))
                .Where(u => personSet.Contains(u.Id))
                .ToList();
        }

        var unresolvedOriginPointIds = new HashSet<int>();
        foreach (var scan in slotScans)
        {
            if (scan.Placement == Placement.OffRound || (scan.RoundOriginId ?? scan.RoundWindowId) is not null)
            {
                continue;
            }

            var submittedAt = Utc(scan.SubmittedAt);
            var origins = scan.RoundStart is { } start && scan.RoundEnd is { } end
                ? timeline.Rounds
                    .Where(r => r.PointId == scan.ServicePointId && r.Start == start && r.End == end
                        && r.ActiveFromUtc <= submittedAt
                        && (r.FinalizedAtUtc is null || submittedAt <= r.FinalizedAtUtc.Value))
                    .Select(r => r.OriginId)
                    .Distinct()
                    .Take(2)
                    .ToArray()
                : [];
            if (origins.Length != 1)
            {
                unresolvedOriginPointIds.Add(scan.ServicePointId);
            }
        }

        var originsComplete = unresolvedOriginPointIds.Count == 0;

        var timelinePointIds = timeline.Current?.Points.Select(p => p.Id).ToHashSet() ?? [];
        var missingPointIds = allPointIds.Where(id => !timelinePointIds.Contains(id)).Distinct().ToList();
        var unverifiedPoints = missingPointIds.Count == 0
            ? new Dictionary<int, OverviewPointMetadata>()
            : (await (from p in db.ServicePoints.AsNoTracking()
                      join sign in db.Signs.AsNoTracking() on p.Id equals sign.ServicePointId into signs
                      from sign in signs.DefaultIfEmpty()
                      where missingPointIds.Contains(p.Id)
                      select new OverviewPointMetadata(
                          p.Id,
                          p.AreaId,
                          sign == null ? null : sign.Code,
                          p.Name,
                          p.SortOrder))
                .ToListAsync(ct))
                .ToDictionary(p => p.PointId);

        foreach (var point in unverifiedPoints.Values)
        {
            pointAreaMap.TryAdd(point.PointId, point.AreaId);
        }

        var observedAreaIds = new HashSet<int>();
        foreach (var a in attendance) observedAreaIds.Add(a.AreaId);
        foreach (var e in entryEvidence) observedAreaIds.Add(e.AreaId);
        foreach (var c in coverRows)
        {
            if (c.CancelledAt is null || Utc(c.CancelledAt.Value) > evaluationUtc)
            {
                observedAreaIds.Add(c.AreaId);
            }
        }
        foreach (var areaId in activeReliefAreaIds) observedAreaIds.Add(areaId);
        foreach (var resp in responsibilities.Values) observedAreaIds.Add(resp.AreaId);
        foreach (var pointId in observedSlotPointIds)
        {
            if (pointAreaMap.TryGetValue(pointId, out var areaId))
            {
                observedAreaIds.Add(areaId);
            }
        }

        var knownTimelineAreaIds = new HashSet<int>();
        if (timeline.Current is not null)
        {
            foreach (var area in timeline.Current.Areas)
            {
                knownTimelineAreaIds.Add(area.Id);
            }
        }
        foreach (var round in timeline.Rounds)
        {
            knownTimelineAreaIds.Add(round.AreaId);
        }

        var missingAreaIds = observedAreaIds.Where(id => !knownTimelineAreaIds.Contains(id)).ToList();
        var unverifiedAreas = missingAreaIds.Count == 0
            ? new Dictionary<int, OverviewUnverifiedArea>()
            : (await db.Areas.AsNoTracking()
                .Where(a => missingAreaIds.Contains(a.Id))
                .Select(a => new OverviewUnverifiedArea(
                    a.Id,
                    a.BuildingId,
                    a.Code,
                    a.Name,
                    a.Building!.Code,
                    a.Building.Name))
                .ToListAsync(ct))
                .ToDictionary(a => a.AreaId);

        var firstScanAtByPoint = precomputedFirstScanAt
            ?? (await db.ScanRecords.AsNoTracking()
                .Where(s => s.SubmittedAt <= readAtUtc)
                .GroupBy(s => s.ServicePointId)
                .Select(g => new { ServicePointId = g.Key, FirstSubmittedAt = g.Min(s => s.SubmittedAt) })
                .ToListAsync(ct))
                .ToDictionary(x => x.ServicePointId, x => Utc(x.FirstSubmittedAt));

        var latestSubmissionAtByPoint = submissions
            .GroupBy(s => s.PointId)
            .ToDictionary(g => g.Key, g => g.Max(s => s.SubmittedAt));

        var annualCutoffUtc = retention.RecordCutoffUtc(readAtUtc);
        var issueStateKnown = ComputeIssueStateKnown(
            slot, moment, timeline, latestSubmissionAtByPoint, annualCutoffUtc, buildingId: null);

        var facts = new OverviewFacts(
            submissions,
            inspections,
            attendance,
            covers,
            people,
            entryEvidence,
            assignedAreas,
            originsComplete,
            issueStateKnown);

        return new OverviewLoadedFacts(
            facts,
            observedAreaIds,
            observedSlotPointIds,
            unresolvedOriginPointIds,
            pointAreaMap,
            responsibilities,
            firstScanAtByPoint,
            unverifiedPoints,
            unverifiedAreas,
            latestSubmissionAtByPoint);
    }

    internal static bool ComputeIssueStateKnown(
        ShiftSlot slot,
        OverviewMoment moment,
        TargetTimeline timeline,
        IReadOnlyDictionary<int, DateTime> latestSubmissionAtByPoint,
        DateTime annualCutoffUtc,
        int? buildingId)
    {
        if (moment.Phase == "notStarted")
        {
            return true;
        }

        if (timeline.Current is null || !timeline.CurrentComplete)
        {
            return false;
        }

        return ComputeIssueStateKnownForImage(
            slot,
            moment,
            timeline.Current,
            timeline.Rounds,
            latestSubmissionAtByPoint,
            annualCutoffUtc,
            buildingId);
    }

    internal static bool ComputeIssueStateKnownForImage(
        ShiftSlot slot,
        OverviewMoment moment,
        TargetImage image,
        IReadOnlyList<RoundObligation> rounds,
        IReadOnlyDictionary<int, DateTime> latestSubmissionAtByPoint,
        DateTime annualCutoffUtc,
        int? buildingId)
    {
        if (moment.Phase == "notStarted")
        {
            return true;
        }

        if (!moment.PolicyComplete)
        {
            return false;
        }

        var activeBuildings = image.Buildings
            .Where(b => b.IsActive && b.CreatedAt <= moment.EvaluationUtc && (buildingId is null || b.Id == buildingId.Value))
            .Select(b => b.Id)
            .ToHashSet();
        var activeAreas = image.Areas
            .Where(a => a.IsActive && a.CreatedAt <= moment.EvaluationUtc && activeBuildings.Contains(a.BuildingId)
                && (slot.Shift == Shift.Day || a.ShiftPattern == ShiftPattern.DayAndNight))
            .Select(a => a.Id)
            .ToHashSet();

        var checkedPointIds = new HashSet<int>();
        foreach (var point in image.Points)
        {
            if (point.IsActive && point.CreatedAt <= moment.EvaluationUtc && activeAreas.Contains(point.AreaId))
            {
                checkedPointIds.Add(point.Id);
                latestSubmissionAtByPoint.TryGetValue(point.Id, out var latest);
                var latestOrNull = latest == default ? (DateTime?)null : latest;
                if (!OverviewAvailability.IsIssueStateKnown(moment.EvaluationUtc, latestOrNull, point.CreatedAt, annualCutoffUtc))
                {
                    return false;
                }
            }
        }

        // Historical rounds of points that became inactive or belonged to moved/deactivated areas also carry Issue checks.
        foreach (var round in rounds)
        {
            if (buildingId is int scoped && round.BuildingId != scoped)
            {
                continue;
            }
            if (!checkedPointIds.Add(round.PointId))
            {
                continue;
            }

            var createdAt = image.Points.FirstOrDefault(p => p.Id == round.PointId)?.CreatedAt;
            latestSubmissionAtByPoint.TryGetValue(round.PointId, out var latest);
            var latestOrNull = latest == default ? (DateTime?)null : latest;
            if (!OverviewAvailability.IsIssueStateKnown(moment.EvaluationUtc, latestOrNull, createdAt, annualCutoffUtc))
            {
                return false;
            }
        }

        return true;
    }

    private const string TransferConfirmed = "confirmed";
    private const string TransferInstructed = "instructed";

    private sealed record TemporalTransferRow(
        int Id,
        int PersonId,
        int ToAreaId,
        DateTime CreatedAt,
        DateTime EffectiveAt,
        bool IsCancelled,
        DateTime? CancelledAt);

    private static async Task<Dictionary<int, OverviewAreaResponsibility>> LoadResponsibilitiesAsync(
        AppDbContext db,
        ShiftSlot slot,
        DateTime evaluationUtc,
        HashSet<int> assignedAreas,
        CancellationToken ct)
    {
        var instructions = await db.AreaTransfers.AsNoTracking()
            .Where(row => row.ShiftDate == slot.ShiftDate
                && row.Shift == slot.Shift
                && row.CreatedAt <= evaluationUtc)
            .OrderBy(row => row.Id)
            .Select(row => new TemporalTransferRow(
                row.Id,
                row.PersonId,
                row.ToAreaId,
                row.CreatedAt,
                row.EffectiveAt,
                row.IsCancelled,
                row.CancelledAt))
            .ToListAsync(ct);

        if (instructions.Count == 0)
        {
            return new Dictionary<int, OverviewAreaResponsibility>();
        }

        var ids = instructions.Select(row => row.Id).ToList();
        var confirmations = (await db.AreaTransferConfirmations.AsNoTracking()
            .Where(row => ids.Contains(row.TransferId) && row.OccurredAt <= evaluationUtc)
            .OrderByDescending(row => row.Id)
            .Select(row => new { row.TransferId, row.OccurredAt })
            .ToListAsync(ct))
            .GroupBy(row => row.TransferId)
            .ToDictionary(group => group.Key, group => Utc(group.First().OccurredAt));

        return BuildResponsibilities(instructions, confirmations, evaluationUtc, assignedAreas);
    }

    private static Dictionary<int, OverviewAreaResponsibility> BuildResponsibilities(
        IReadOnlyList<TemporalTransferRow> instructions,
        IReadOnlyDictionary<int, DateTime> confirmations,
        DateTime evaluationUtc,
        HashSet<int> assignedAreas)
    {
        static bool IsCancelledAt(TemporalTransferRow row, DateTime at) =>
            row.IsCancelled && (row.CancelledAt is null || Utc(row.CancelledAt.Value) <= at);

        var candidates = new List<(OverviewAreaResponsibility Responsibility, int Priority, DateTime SortAt, int TransferId)>();

        foreach (var group in instructions.GroupBy(row => row.PersonId))
        {
            var chain = group.OrderBy(row => row.Id).ToList();
            var latest = chain[^1];

            (int TransferId, int AreaId, DateTime ConfirmedAt)? lastConfirmed = null;
            foreach (var row in chain)
            {
                if (!IsCancelledAt(row, evaluationUtc) && confirmations.TryGetValue(row.Id, out var confirmedAt))
                {
                    lastConfirmed = (row.Id, row.ToAreaId, confirmedAt);
                }
            }

            var latestCancelled = IsCancelledAt(latest, evaluationUtc);
            var effectiveAt = Utc(latest.EffectiveAt);

            if (!latestCancelled && confirmations.TryGetValue(latest.Id, out var latestConfirmedAt))
            {
                candidates.Add((
                    new OverviewAreaResponsibility(
                        latest.PersonId,
                        latest.ToAreaId,
                        TransferConfirmed,
                        lastConfirmed!.Value.AreaId,
                        latestConfirmedAt),
                    2,
                    latestConfirmedAt,
                    latest.Id));
            }
            else if (!latestCancelled && effectiveAt <= evaluationUtc)
            {
                assignedAreas.Add(latest.ToAreaId);
                candidates.Add((
                    new OverviewAreaResponsibility(
                        latest.PersonId,
                        latest.ToAreaId,
                        TransferInstructed,
                        lastConfirmed?.AreaId,
                        null),
                    1,
                    effectiveAt,
                    latest.Id));
            }
            else if (lastConfirmed is { } prior)
            {
                candidates.Add((
                    new OverviewAreaResponsibility(
                        latest.PersonId,
                        prior.AreaId,
                        TransferConfirmed,
                        prior.AreaId,
                        prior.ConfirmedAt),
                    2,
                    prior.ConfirmedAt,
                    prior.TransferId));
            }
        }

        var result = new Dictionary<int, OverviewAreaResponsibility>();
        foreach (var group in candidates.GroupBy(c => c.Responsibility.AreaId))
        {
            result[group.Key] = group
                .OrderByDescending(c => c.Priority)
                .ThenByDescending(c => c.SortAt)
                .ThenByDescending(c => c.TransferId)
                .First()
                .Responsibility;
        }

        return result;
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private sealed record ScanProjection(
        long Id,
        int ServicePointId,
        DateOnly ShiftDate,
        Shift Shift,
        int? RoundOriginId,
        int? RoundWindowId,
        TimeOnly? RoundStart,
        TimeOnly? RoundEnd,
        Placement Placement,
        CleaningStatus Status,
        DateTime SubmittedAt,
        int UserId,
        string? IssueTags,
        string? Note);
}
