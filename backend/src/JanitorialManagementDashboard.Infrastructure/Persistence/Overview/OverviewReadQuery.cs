using System.Data;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Overview;

public sealed record OverviewRange(
    DateOnly? CompleteFrom,
    DateOnly? CompleteThrough,
    DateTime? CaptureStartedAtUtc);

public sealed record OverviewPointMetadata(
    int PointId,
    int AreaId,
    string? Code,
    string Name,
    int SortOrder);

public sealed record OverviewAreaResponsibility(
    int UserId,
    int AreaId,
    string State,
    int? LastConfirmedAreaId,
    DateTime? ConfirmedAt);

public sealed record OverviewBuildingMetadata(
    int Id,
    string Code,
    string Name);

public sealed record OverviewRead(
    OverviewSnapshot Snapshot,
    OverviewFacts Facts,
    DateTime ReadAtUtc,
    OverviewRange Range)
{
    public bool BuildingKnown { get; init; } = true;
    public bool BuildingOwnershipProven { get; init; } = true;
    public IReadOnlyList<OverviewBuildingMetadata> Buildings { get; init; } = [];
    public IReadOnlyDictionary<int, OverviewSnapshot> BuildingSnapshots { get; init; } =
        new Dictionary<int, OverviewSnapshot>();
    public IReadOnlyDictionary<int, SectionAvailability> BuildingAvailability { get; init; } =
        new Dictionary<int, SectionAvailability>();
    public IReadOnlyDictionary<int, OverviewPointMetadata> PointMetadata { get; init; } =
        new Dictionary<int, OverviewPointMetadata>();
    public IReadOnlyList<OverviewArea> ObservedAreas { get; init; } = [];
    public IReadOnlyDictionary<int, OverviewAreaResponsibility> AreaResponsibilities { get; init; } =
        new Dictionary<int, OverviewAreaResponsibility>();
    public IReadOnlySet<int> UnresolvedOriginPointIds { get; init; } = new HashSet<int>();
    public IReadOnlyDictionary<int, int> PointAreaMap { get; init; } = new Dictionary<int, int>();
}

public sealed class OverviewReadQuery(
    AppDbContext db,
    ScheduleStore store,
    RetentionSettings retention,
    TimeProvider clock,
    ILogger<OverviewReadQuery> logger)
{
    public async Task<OverviewRead> ReadAsync(ShiftSlot slot, int? buildingId, CancellationToken ct, int? detailAreaId = null)
    {
        ArgumentNullException.ThrowIfNull(slot);

        var isolation = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? IsolationLevel.Serializable
            : IsolationLevel.RepeatableRead;
        await using var tx = await db.Database.BeginTransactionAsync(isolation, ct);

        var readAt = DateTime.SpecifyKind(clock.GetUtcNow().UtcDateTime, DateTimeKind.Utc);
        var firstRangeDate = DateOnly.FromDateTime(ThaiTime.FromUtc(retention.RecordCutoffUtc(readAt)));
        var rangeOpen = ShiftCalendar.StartUtc(new ShiftSlot(firstRangeDate, Shift.Day))
            .AddMinutes(-AttendanceSettings.MaxReceivingOffsetMinutes);
        var slotOpen = ShiftCalendar.StartUtc(slot)
            .AddMinutes(-AttendanceSettings.MaxReceivingOffsetMinutes);
        var needFrom = rangeOpen < slotOpen ? rangeOpen : slotOpen;

        var predecessorAt = await db.OverviewTargetRevisions.AsNoTracking()
            .Where(r => r.EffectiveAt <= needFrom)
            .OrderByDescending(r => r.EffectiveAt).ThenByDescending(r => r.Id)
            .Select(r => (DateTime?)r.EffectiveAt)
            .FirstOrDefaultAsync(ct);

        var (revisions, unreadableIntervals, structuralGaps) = await OverviewTargetRecorder.LoadAndDecodeForReadAsync(
            db, predecessorAt, readAt, logger, ct);
        var lastPossibleClose = ShiftCalendar.EndUtc(slot).AddMinutes(AttendanceSettings.MaxReceivingOffsetMinutes);
        var slotRevisions = revisions.Where(r => r.EffectiveAt <= lastPossibleClose).ToArray();
        var moment = OverviewTime.Evaluate(slot, readAt, slotRevisions);
        var timeline = OverviewTimeline.Build(slot, moment, slotRevisions);
        var loaded = await OverviewFactLoader.LoadDetailedAsync(db, store, slot, moment, timeline, readAt, retention, ct);

        var captureStartedAt = revisions
            .Select(r => r.Image.CaptureStartedAtUtc)
            .Where(x => x.HasValue)
            .Min();
        var targetCaptureFrom = captureStartedAt
            ?? (revisions.Length > 0 ? revisions[0].EffectiveAt
                : unreadableIntervals.Length > 0 ? unreadableIntervals[0].From
                : DateTime.MaxValue);

        var annualCutoffUtc = retention.RecordCutoffUtc(readAt);
        var issueKnownSlots = ComputeIssueKnownSlots(
            readAt, firstRangeDate, revisions, loaded.FirstScanAtByPoint, annualCutoffUtc);

        var hasTargetHistory = timeline.Current is not null || timeline.Rounds.Count > 0;
        var buildingKnown = true;
        var buildingOwnershipProven = true;
        OverviewBuildingMetadata? preBaselineBuilding = null;

        if (buildingId is int requestedBuildingId)
        {
            if (hasTargetHistory)
            {
                buildingKnown = IsBuildingKnownInContext(requestedBuildingId, slot, moment, timeline, slotRevisions);
            }
            else
            {
                var row = await db.Buildings.AsNoTracking()
                    .Where(b => b.Id == requestedBuildingId)
                    .Select(b => new OverviewBuildingMetadata(b.Id, b.Code, b.Name))
                    .FirstOrDefaultAsync(ct);
                buildingKnown = row is not null;
                buildingOwnershipProven = false;
                preBaselineBuilding = row;
            }
        }

        var scopedIssueKnown = hasTargetHistory
            ? OverviewFactLoader.ComputeIssueStateKnown(
                slot, moment, timeline, loaded.LatestSubmissionAtByPoint, annualCutoffUtc, buildingId)
            : loaded.Facts.IssueStateKnown;

        var slotCoverage = DetailCoverage.FromRetention(
            readAt, retention, targetCaptureFrom, scopedIssueKnown, structuralGaps)
            with { UnreadableTargetIntervals = unreadableIntervals };
        var rawAvailability = OverviewAvailability.Calculate(slot, moment, timeline, slotCoverage);

        var rangeCoverage = DetailCoverage.FromRetention(
            readAt, retention, targetCaptureFrom, true, structuralGaps)
            with { UnreadableTargetIntervals = unreadableIntervals };
        var (rangeStart, rangeEnd) = OverviewAvailability.CalculateSupportedBounds(
            readAt, retention, revisions, rangeCoverage, issueKnownSlots);
        var range = new OverviewRange(rangeStart, rangeEnd, captureStartedAt);

        OverviewSnapshot snapshot;
        SectionAvailability effectiveAvailability;
        IReadOnlyList<OverviewBuildingMetadata> buildingList;
        var buildingSnapshots = new Dictionary<int, OverviewSnapshot>();
        var buildingAvailability = new Dictionary<int, SectionAvailability>();
        var areaBuildings = BuildAreaBuildingLookup(timeline, slotRevisions);
        var unresolvedBuildings = ResolveUnresolvedBuildings(loaded.UnresolvedOriginPointIds, loaded.PointAreaMap, areaBuildings, out var hasUnmappedUnresolved);

        if (buildingId is null)
        {
            var (rawWhole, areasByBuilding) = OverviewProjection.ProjectAll(slot, moment, timeline, loaded.Facts, rawAvailability);
            effectiveAvailability = ApplyReadGating(
                rawAvailability,
                hasOriginsIncomplete: rawWhole.Areas.Any(a => a.OriginsIncomplete)
                    || loaded.UnresolvedOriginPointIds.Count > 0,
                buildingOwnershipProven);
            snapshot = rawWhole with { Availability = effectiveAvailability };
            buildingList = CollectBuildings(buildingId, hasTargetHistory, preBaselineBuilding, timeline, snapshot);

            foreach (var b in buildingList)
            {
                var bIssueKnown = hasTargetHistory
                    && OverviewFactLoader.ComputeIssueStateKnown(
                        slot, moment, timeline, loaded.LatestSubmissionAtByPoint, annualCutoffUtc, b.Id);
                var bRawAvailability = OverviewAvailability.Calculate(
                    slot, moment, timeline, slotCoverage with { IssueStateKnown = bIssueKnown });
                var bAreas = areasByBuilding.GetValueOrDefault(b.Id) ?? [];
                var bAvailability = ApplyReadGating(
                    bRawAvailability,
                    hasOriginsIncomplete: bAreas.Any(a => a.OriginsIncomplete)
                        || hasUnmappedUnresolved
                        || unresolvedBuildings.Contains(b.Id),
                    buildingOwnershipProven);
                buildingSnapshots[b.Id] = new OverviewSnapshot(slot, moment, timeline, bAvailability, bAreas);
                buildingAvailability[b.Id] = bAvailability;
            }
        }
        else
        {
            var rawSnapshot = OverviewProjection.Build(slot, moment, timeline, loaded.Facts, rawAvailability, buildingId);
            effectiveAvailability = ApplyReadGating(
                rawAvailability,
                hasOriginsIncomplete: rawSnapshot.Areas.Any(a => a.OriginsIncomplete)
                    || hasUnmappedUnresolved || unresolvedBuildings.Contains(buildingId.Value),
                buildingOwnershipProven);
            snapshot = rawSnapshot with { Availability = effectiveAvailability };
            buildingList = CollectBuildings(buildingId, hasTargetHistory, preBaselineBuilding, timeline, snapshot);

            foreach (var b in buildingList)
            {
                if (buildingId == b.Id)
                {
                    buildingSnapshots[b.Id] = snapshot;
                    buildingAvailability[b.Id] = effectiveAvailability;
                    continue;
                }

                var bIssueKnown = hasTargetHistory
                    && OverviewFactLoader.ComputeIssueStateKnown(
                        slot, moment, timeline, loaded.LatestSubmissionAtByPoint, annualCutoffUtc, b.Id);
                var bRawAvailability = OverviewAvailability.Calculate(
                    slot, moment, timeline, slotCoverage with { IssueStateKnown = bIssueKnown });
                var bRawSnapshot = OverviewProjection.Build(slot, moment, timeline, loaded.Facts, bRawAvailability, b.Id);
                var bAvailability = ApplyReadGating(
                    bRawAvailability,
                    hasOriginsIncomplete: bRawSnapshot.Areas.Any(a => a.OriginsIncomplete)
                        || hasUnmappedUnresolved
                        || unresolvedBuildings.Contains(b.Id),
                    buildingOwnershipProven);
                buildingSnapshots[b.Id] = bRawSnapshot with { Availability = bAvailability };
                buildingAvailability[b.Id] = bAvailability;
            }
        }

        var observedAreas = BuildObservedAreas(
            slot, moment, snapshot, loaded, effectiveAvailability, buildingId, hasTargetHistory, areaBuildings);

        var pointMetadata = BuildPointMetadata(timeline, loaded.UnverifiedPointMetadata);

        var facts = loaded.Facts;
        if (detailAreaId is int areaId && buildingKnown
            && effectiveAvailability.Entry.State is not ("notStarted" or "unavailable")
            && (snapshot.Areas.Any(a => a.Id == areaId) || observedAreas.Any(a => a.Id == areaId)))
        {
            facts = await LoadAttendanceDetailsAsync(facts, slot, areaId, moment.EvaluationUtc, readAt, ct);
        }

        await tx.CommitAsync(ct);

        return new OverviewRead(snapshot, facts, readAt, range)
        {
            BuildingKnown = buildingKnown,
            BuildingOwnershipProven = buildingOwnershipProven,
            Buildings = buildingList,
            BuildingSnapshots = buildingSnapshots,
            BuildingAvailability = buildingAvailability,
            PointMetadata = pointMetadata,
            ObservedAreas = observedAreas,
            AreaResponsibilities = loaded.Responsibilities,
            UnresolvedOriginPointIds = loaded.UnresolvedOriginPointIds,
            PointAreaMap = loaded.PointAreaMap,
        };
    }

    private async Task<OverviewFacts> LoadAttendanceDetailsAsync(
        OverviewFacts facts, ShiftSlot slot, int areaId, DateTime evaluationUtc, DateTime readAtUtc, CancellationToken ct)
    {
        var attendance = facts.Attendance.Where(a => a.AreaId == areaId).ToArray();
        var editedIds = attendance.Where(a => a.Edited).Select(a => a.Id).ToArray();
        if (editedIds.Length > 0)
        {
            // The event retains the source of an addition even when Admin later edits its actual time.
            var audits = await db.AuditLog.AsNoTracking()
                .Where(a => a.EntityType == "shift_attendances" && a.EntityId != null
                    && editedIds.Contains(a.EntityId.Value) && a.OccurredAt <= readAtUtc
                    && (a.Action == "ATTENDANCE_ADD" || a.Action == "ATTENDANCE_EDIT"))
                .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
                .Select(a => new { Id = a.EntityId!.Value, a.Action, a.Reason, a.OccurredAt })
                .ToListAsync(ct);
            var byEvent = audits.ToLookup(a => a.Id);
            attendance = attendance.Select(fact =>
            {
                var history = byEvent[fact.Id];
                var latest = history.FirstOrDefault();
                var added = history.LastOrDefault(a => a.Action == "ATTENDANCE_ADD");
                var edited = history.FirstOrDefault(a => a.Action == "ATTENDANCE_EDIT");
                return fact with
                {
                    Reason = latest?.Reason,
                    CorrectedAt = latest is null ? null : Utc(latest.OccurredAt),
                    Addition = added is null ? null : new OverviewAttendanceChange(added.Reason, Utc(added.OccurredAt)),
                    Edit = edited is null ? null : new OverviewAttendanceChange(edited.Reason, Utc(edited.OccurredAt)),
                };
            }).ToArray();
        }

        var storedGaps = await db.AttendanceGaps.AsNoTracking()
            .Where(g => g.ShiftDate == slot.ShiftDate && g.Shift == slot.Shift && g.AreaId == areaId
                && g.Status == AttendanceGapStatus.Skipped && (g.RecordedAt ?? g.CreatedAt) <= evaluationUtc)
            .OrderBy(g => g.Id)
            .Select(g => new { g.Id, g.UserId, g.EventType, g.Reason, g.RecordedAt })
            .ToListAsync(ct);
        var gaps = storedGaps
            .Where(g => !attendance.Any(a => a.UserId == g.UserId && a.Event == g.EventType))
            .Select(g => new OverviewAttendanceGap(g.Id, areaId, g.UserId, g.EventType, g.Reason,
                g.RecordedAt is { } recordedAt ? Utc(recordedAt) : null))
            .ToArray();

        // Leave is a person-slot plan fact without an Area ID. Show it only alongside an actual or gap in this Area.
        var peopleWithActions = attendance.Select(a => a.UserId).Concat(gaps.Select(g => g.UserId)).Distinct().ToArray();
        var storedLeaves = peopleWithActions.Length == 0
            ? []
            : await db.ScheduleExceptions.AsNoTracking()
                .Where(l => l.ShiftDate == slot.ShiftDate && l.Shift == slot.Shift
                    && l.Kind == ScheduleExceptionKind.Leave && peopleWithActions.Contains(l.UserId)
                    && l.CreatedAt <= evaluationUtc
                    && (!l.IsCancelled || l.CancelledAt > evaluationUtc) && l.StartsAt != null && l.EndsAt != null)
                .OrderBy(l => l.StartsAt).ThenBy(l => l.Id)
                .Select(l => new { l.Id, l.UserId, StartsAt = l.StartsAt!.Value, EndsAt = l.EndsAt!.Value })
                .ToListAsync(ct);
        var leaves = storedLeaves.Select(l => new OverviewAttendanceLeave(l.Id, l.UserId, Utc(l.StartsAt), Utc(l.EndsAt))).ToArray();

        var leaveEffects = await LoadLeaveEffectsAsync(slot, leaves, attendance, facts.Attendance, ct);
        var gapPeople = gaps.Select(g => g.UserId).Except(facts.People.Select(p => p.Id)).ToArray();
        var additionalPeople = gapPeople.Length == 0
            ? []
            : await db.Users.AsNoTracking().Where(u => gapPeople.Contains(u.Id))
                .Select(u => new OverviewPerson(u.Id, u.DisplayName)).ToListAsync(ct);
        return facts with
        {
            Attendance = attendance,
            AttendanceGaps = gaps,
            AttendanceLeaves = leaves,
            AttendanceLeaveEffects = leaveEffects,
            People = facts.People.Concat(additionalPeople).ToArray(),
        };
    }

    private async Task<IReadOnlyList<OverviewAttendanceLeaveEffect>> LoadLeaveEffectsAsync(
        ShiftSlot slot, IReadOnlyList<OverviewAttendanceLeave> leaves, IReadOnlyList<OverviewAttendance> areaEvents,
        IReadOnlyList<OverviewAttendance> slotEvents, CancellationToken ct)
    {
        if (leaves.Count == 0) return [];

        var effects = new List<OverviewAttendanceLeaveEffect>();
        var eventsByPerson = areaEvents.ToLookup(row => row.UserId);
        var recorded = slotEvents.Select(a => (a.UserId, a.Event)).ToHashSet();
        foreach (var intervals in leaves.GroupBy(row => row.UserId))
        {
            var personId = intervals.Key;
            var events = eventsByPerson[personId];
            var checkIn = events.FirstOrDefault(row => row.Event == AttendanceEvent.ShiftIn);
            if (checkIn is not null && intervals.Any(l => checkIn.OccurredAt >= l.StartsAt && checkIn.OccurredAt < l.EndsAt))
            {
                effects.Add(new OverviewAttendanceLeaveEffect(personId, AttendanceEvent.ShiftIn, "check-in-conflict"));
            }

            var departure = events.FirstOrDefault(row => row.Event == AttendanceEvent.ShiftOut);
            if (departure is null
                || !intervals.Any(l => l.EndsAt > departure.OccurredAt)) continue;

            var relief = await store.LiveReliefAsync(personId, slot.ShiftDate, slot.Shift, ct);
            var breakGroup = relief?.BreakGroup
                ?? (await store.ReadPersonDayAsync(personId, slot.ShiftDate, slot.Shift, ct))?.BreakGroup.Value;
            var expectations = AttendanceExpectations.ForSlot(slot, breakGroup);
            foreach (var action in AttendanceLifecycle.Order.Where(AttendanceLifecycle.IsSkippable))
            {
                if (recorded.Contains((personId, action)) || !expectations.TryGetValue(action, out var expected)) continue;
                if (intervals.Any(l => AttendanceApplicability.NotApplicable(expected, departure.OccurredAt, l.StartsAt, l.EndsAt)))
                {
                    effects.Add(new OverviewAttendanceLeaveEffect(personId, action, "exempt"));
                }
            }
        }

        return effects;
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static bool IsBuildingKnownInContext(
        int buildingId,
        ShiftSlot slot,
        OverviewMoment moment,
        TargetTimeline timeline,
        TargetRevision[] revisions)
    {
        if (timeline.Current?.Buildings.Any(b => b.Id == buildingId && b.IsActive && b.CreatedAt <= moment.EvaluationUtc) == true)
        {
            return true;
        }

        if (timeline.Rounds.Any(r => r.BuildingId == buildingId))
        {
            return true;
        }

        if (moment.Phase == "notStarted")
        {
            return false;
        }

        var slotStart = ShiftCalendar.StartUtc(slot);
        var baselineAt = revisions.LastOrDefault(x => x.EffectiveAt <= slotStart)?.EffectiveAt;
        return revisions.Any(r => r.EffectiveAt <= moment.EvaluationUtc
            && (r.EffectiveAt >= slotStart || (baselineAt.HasValue && r.EffectiveAt == baselineAt.Value))
            && r.Image.Buildings.Any(b => b.Id == buildingId && b.IsActive && b.CreatedAt <= moment.EvaluationUtc));
    }

    private static HashSet<ShiftSlot> ComputeIssueKnownSlots(
        DateTime readAt,
        DateOnly firstDate,
        TargetRevision[] revisions,
        IReadOnlyDictionary<int, DateTime> firstScanAtByPoint,
        DateTime annualCutoffUtc)
    {
        var known = new HashSet<ShiftSlot>();
        if (revisions.Length == 0)
        {
            return known;
        }

        var lastDate = DateOnly.FromDateTime(ThaiTime.FromUtc(readAt));
        var startDate = DateOnly.FromDateTime(ThaiTime.FromUtc(revisions[0].EffectiveAt)).AddDays(-1);
        if (startDate > firstDate)
        {
            firstDate = startDate;
        }

        var shifts = new[] { Shift.Day, Shift.Night };
        var firstRevision = 0;
        var afterLastRevision = 0;

        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        foreach (var shift in shifts)
        {
            var slot = new ShiftSlot(date, shift);
            var from = ShiftCalendar.StartUtc(slot).AddMinutes(-AttendanceSettings.MaxReceivingOffsetMinutes);
            var end = ShiftCalendar.EndUtc(slot).AddMinutes(AttendanceSettings.MaxReceivingOffsetMinutes);
            var to = readAt <= end ? readAt : end;
            while (firstRevision + 1 < revisions.Length && revisions[firstRevision + 1].EffectiveAt <= from)
                firstRevision++;
            while (afterLastRevision < revisions.Length && revisions[afterLastRevision].EffectiveAt <= to)
                afterLastRevision++;

            var relevant = revisions[firstRevision..afterLastRevision];
            var moment = OverviewTime.EvaluateOrdered(slot, readAt, relevant);
            if (moment.Phase == "notStarted")
            {
                known.Add(slot);
                continue;
            }

            if (relevant.Length == 0 || !moment.PolicyComplete)
            {
                continue;
            }

            if (relevant.Length == 1)
            {
                if (OverviewFactLoader.ComputeIssueStateKnownForImage(
                    slot, moment, relevant[0].Image, [], firstScanAtByPoint, annualCutoffUtc, null))
                {
                    known.Add(slot);
                }
                continue;
            }

            var timeline = OverviewTimeline.Build(slot, moment, relevant);
            if (OverviewFactLoader.ComputeIssueStateKnown(slot, moment, timeline, firstScanAtByPoint, annualCutoffUtc, null))
            {
                known.Add(slot);
            }
        }

        return known;
    }

    private static Dictionary<int, int> BuildAreaBuildingLookup(TargetTimeline timeline, TargetRevision[] revisions)
    {
        var map = new Dictionary<int, int>();
        foreach (var revision in revisions)
        {
            foreach (var area in revision.Image.Areas)
            {
                map[area.Id] = area.BuildingId;
            }
        }

        foreach (var round in timeline.Rounds)
        {
            map[round.AreaId] = round.BuildingId;
        }

        return map;
    }

    private static HashSet<int> ResolveUnresolvedBuildings(
        IReadOnlySet<int> unresolvedPointIds,
        IReadOnlyDictionary<int, int> pointAreaMap,
        IReadOnlyDictionary<int, int> areaBuildings,
        out bool hasUnmapped)
    {
        hasUnmapped = false;
        var result = new HashSet<int>();
        foreach (var pointId in unresolvedPointIds)
        {
            if (pointAreaMap.TryGetValue(pointId, out var areaId)
                && areaBuildings.TryGetValue(areaId, out var buildingId))
            {
                result.Add(buildingId);
            }
            else
            {
                hasUnmapped = true;
            }
        }

        return result;
    }

    private static SectionAvailability ApplyReadGating(
        SectionAvailability availability,
        bool hasOriginsIncomplete,
        bool buildingOwnershipProven)
    {
        var entry = buildingOwnershipProven ? availability.Entry : WithReason(availability.Entry, "buildingOwnershipUnproven");
        var work = availability.Work;
        var inspection = availability.Inspection;
        var history = availability.History;
        var issue = buildingOwnershipProven ? availability.Issue : WithReason(availability.Issue, "buildingOwnershipUnproven");

        if (!buildingOwnershipProven)
        {
            work = WithReason(work, "buildingOwnershipUnproven");
            inspection = WithReason(inspection, "buildingOwnershipUnproven");
            history = WithReason(history, "buildingOwnershipUnproven");
        }

        if (hasOriginsIncomplete)
        {
            work = WithReason(work, "originsIncomplete");
            inspection = WithReason(inspection, "originsIncomplete");
            history = WithReason(history, "originsIncomplete");
        }

        return new SectionAvailability(entry, work, inspection, history, issue);
    }

    private static Availability WithReason(Availability source, string reason)
    {
        if (source.State is "unavailable" or "notStarted")
        {
            return source;
        }

        if (source.Reasons.Contains(reason, StringComparer.Ordinal))
        {
            return source;
        }

        return new Availability("partial", [.. source.Reasons, reason]);
    }

    private static IReadOnlyList<OverviewBuildingMetadata> CollectBuildings(
        int? buildingId,
        bool hasTargetHistory,
        OverviewBuildingMetadata? preBaselineBuilding,
        TargetTimeline timeline,
        OverviewSnapshot snapshot)
    {
        if (!hasTargetHistory)
        {
            return preBaselineBuilding is null ? [] : [preBaselineBuilding];
        }

        var map = new Dictionary<int, OverviewBuildingMetadata>();
        if (timeline.Current is not null)
        {
            foreach (var b in timeline.Current.Buildings)
            {
                if (b.IsActive && b.CreatedAt <= snapshot.Moment.EvaluationUtc && (buildingId is null || b.Id == buildingId.Value))
                {
                    map[b.Id] = new OverviewBuildingMetadata(b.Id, b.Code, b.Name);
                }
            }
        }

        foreach (var round in timeline.Rounds)
        {
            if (buildingId is null || round.BuildingId == buildingId.Value)
            {
                map.TryAdd(round.BuildingId, new OverviewBuildingMetadata(round.BuildingId, round.BuildingCode, round.BuildingName));
            }
        }

        foreach (var area in snapshot.Areas)
        {
            if (buildingId is null || area.BuildingId == buildingId.Value)
            {
                map.TryAdd(area.BuildingId, new OverviewBuildingMetadata(area.BuildingId, area.BuildingCode, area.BuildingName));
            }
        }

        return map.Values
            .OrderBy(b => b.Code, StringComparer.Ordinal)
            .ThenBy(b => b.Id)
            .ToArray();
    }

    private static IReadOnlyList<OverviewArea> BuildObservedAreas(
        ShiftSlot slot,
        OverviewMoment moment,
        OverviewSnapshot snapshot,
        OverviewLoadedFacts loaded,
        SectionAvailability availability,
        int? buildingId,
        bool hasTargetHistory,
        IReadOnlyDictionary<int, int> areaBuildings)
    {
        if (moment.Phase == "notStarted")
        {
            return [];
        }

        var canObserveEntry = availability.Entry.State is "partial" or "complete";
        var canObserveWork = availability.Work.State is "partial" or "complete";
        if (!canObserveEntry && !canObserveWork)
        {
            return [];
        }

        var existingAreaIds = snapshot.Areas.Select(a => a.Id).ToHashSet();
        var enteredAreas = Application.Scheduling.ScheduleEntryEvidence.EnteredAreas(
            new Application.Scheduling.ScheduleSlot(slot.ShiftDate, slot.Shift),
            loaded.Facts.EntryEvidence);
        var observed = new List<OverviewArea>();

        foreach (var areaId in loaded.ObservedSlotAreaIds.OrderBy(id => id))
        {
            if (existingAreaIds.Contains(areaId))
            {
                continue;
            }

            if (buildingId is int requestedBuilding)
            {
                // Never treat unverified current DB BuildingId as historical proof before target baseline.
                if (!hasTargetHistory || !areaBuildings.TryGetValue(areaId, out var provenBuilding) || provenBuilding != requestedBuilding)
                {
                    continue;
                }
            }

            var meta = loaded.UnverifiedAreaMetadata.GetValueOrDefault(areaId);
            var areaBuildingId = meta?.BuildingId ?? (areaBuildings.TryGetValue(areaId, out var knownBid) ? knownBid : 0);
            var areaCode = meta?.Code ?? $"AREA-{areaId}";
            var areaName = meta?.Name ?? areaCode;
            var buildingCode = meta?.BuildingCode ?? string.Empty;
            var buildingName = meta?.BuildingName ?? string.Empty;

            var groups = new HashSet<string>(StringComparer.Ordinal);
            var entry = Application.Scheduling.ScheduleEntryEvidence.Unassigned;
            if (canObserveEntry)
            {
                entry = OverviewRules.Entry(areaId, enteredAreas, loaded.Facts.AssignedAreas);
                // When target history exists, only active target structures count toward entry groups;
                // a deactivated Area with 0 rounds remains inspectable in detail without inflating complete metrics.
                if (!hasTargetHistory)
                {
                    groups.Add(entry);
                    if (entry != OverviewGroupKeys.Entered)
                    {
                        groups.Add(OverviewGroupKeys.NotEntered);
                    }
                }
            }

            var points = new List<OverviewPoint>();
            var hasUnresolvedOrigin = false;
            if (canObserveWork)
            {
                var areaPointIds = loaded.ObservedSlotPointIds
                    .Where(pid => loaded.PointAreaMap.TryGetValue(pid, out var aid) && aid == areaId)
                    .OrderBy(pid => loaded.UnverifiedPointMetadata.TryGetValue(pid, out var pm) ? pm.SortOrder : int.MaxValue)
                    .ThenBy(pid => pid)
                    .ToArray();

                foreach (var pointId in areaPointIds)
                {
                    if (loaded.UnresolvedOriginPointIds.Contains(pointId))
                    {
                        hasUnresolvedOrigin = true;
                    }

                    var latestCounted = loaded.Facts.Submissions
                        .Where(s => s.PointId == pointId && s.ShiftDate == slot.ShiftDate && s.Shift == slot.Shift
                            && s.Placement != Placement.OffRound && s.SubmittedAt <= moment.EvaluationUtc)
                        .OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id)
                        .FirstOrDefault();
                    var latestInspection = latestCounted is null
                        ? null
                        : loaded.Facts.Inspections
                            .Where(i => i.SubmissionId == latestCounted.Id && i.InspectedAt <= moment.EvaluationUtc)
                            .OrderByDescending(i => i.InspectedAt).ThenByDescending(i => i.Id)
                            .FirstOrDefault();
                    var latestAny = loaded.Facts.Submissions
                        .Where(s => s.PointId == pointId && s.SubmittedAt <= moment.EvaluationUtc)
                        .OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id)
                        .FirstOrDefault();

                    points.Add(new OverviewPoint(
                        pointId,
                        IsDue: false,
                        IsSubmitted: latestCounted is not null,
                        IsInspected: latestCounted is not null && latestInspection is not null,
                        NeedsRework: latestInspection?.Result == InspectionResult.Rework,
                        IsIssue: latestAny?.Status == CleaningStatus.Issue,
                        IsOverdue: false,
                        IsUpcoming: false,
                        latestCounted?.Id,
                        latestInspection?.Id));
                }

                if (!hasTargetHistory)
                {
                    if (points.Any(p => p.IsIssue) && availability.Issue.State != "unavailable")
                    {
                        groups.Add(OverviewGroupKeys.Issue);
                    }

                    if (points.Any(p => p.NeedsRework))
                    {
                        groups.Add(OverviewGroupKeys.Rework);
                    }

                    if (points.Any(p => p.IsSubmitted && !p.IsInspected))
                    {
                        groups.Add(OverviewGroupKeys.PendingInspection);
                    }
                }
            }

            var work = new AreaWork(
                DuePoints: 0,
                SubmittedPoints: 0,
                InspectedPoints: 0,
                Submitted: false,
                Inspected: false,
                Incomplete: false,
                PendingInspection: points.Any(p => p.IsSubmitted && !p.IsInspected),
                NoSubmission: false,
                Rework: points.Any(p => p.NeedsRework),
                Issue: points.Any(p => p.IsIssue),
                Overdue: false);

            observed.Add(new OverviewArea(
                areaId,
                areaBuildingId,
                areaCode,
                areaName,
                buildingCode,
                buildingName,
                entry,
                work,
                points,
                [],
                groups,
                OriginsIncomplete: hasUnresolvedOrigin));
        }

        return observed
            .OrderBy(a => a.BuildingCode, StringComparer.Ordinal)
            .ThenBy(a => a.Code, StringComparer.Ordinal)
            .ThenBy(a => a.Id)
            .ToArray();
    }

    private static IReadOnlyDictionary<int, OverviewPointMetadata> BuildPointMetadata(
        TargetTimeline timeline,
        IReadOnlyDictionary<int, OverviewPointMetadata> unverified)
    {
        var map = new Dictionary<int, OverviewPointMetadata>(unverified);
        if (timeline.Current is not null)
        {
            foreach (var p in timeline.Current.Points)
            {
                map[p.Id] = new OverviewPointMetadata(p.Id, p.AreaId, p.Code, p.Name, p.SortOrder);
            }
        }

        return map;
    }
}
