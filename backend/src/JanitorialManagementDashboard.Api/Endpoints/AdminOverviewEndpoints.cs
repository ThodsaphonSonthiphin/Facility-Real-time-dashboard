using System.Globalization;
using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;

namespace JanitorialManagementDashboard.Api.Endpoints;

public static class AdminOverviewEndpoints
{
    public const int DefaultPageSize = 5;
    public const int MaxPageSize = 50;

    public static IEndpointRouteBuilder MapAdminOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/overview").RequireAuthorization(AuthSetup.AdminOnly);
        group.MapGet("", SummaryAsync);
        group.MapGet("/areas", AreasAsync);
        group.MapGet("/areas/{areaId:int}", AreaAsync);
        return app;
    }

    private static async Task<IResult> SummaryAsync(
        HttpRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider clock,
        OverviewReadQuery query,
        CancellationToken ct)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        if (!TryParseCommonQuery(request.Query, clock.GetUtcNow().UtcDateTime, out var slot, out var buildingId, out var error))
        {
            return Bad(error!);
        }

        var read = await query.ReadAsync(slot, buildingId, ct);
        if (!read.BuildingKnown)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบตึกนี้ในบริบทที่เลือก");
        }

        return Results.Ok(BuildSummaryDto(read, buildingId));
    }

    private static async Task<IResult> AreasAsync(
        HttpRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider clock,
        OverviewReadQuery query,
        CancellationToken ct)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        if (!TryParseCommonQuery(request.Query, clock.GetUtcNow().UtcDateTime, out var slot, out var buildingId, out var error)
            || !TryParseRequiredGroup(request.Query, out var group, out error)
            || !TryParsePaging(request.Query, out var page, out var pageSize, out error))
        {
            return Bad(error!);
        }

        var read = await query.ReadAsync(slot, buildingId, ct);
        if (!read.BuildingKnown)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบตึกนี้ในบริบทที่เลือก");
        }

        var summary = BuildSummaryDto(read, buildingId);
        var sectionAvailability = SectionForGroup(read.Snapshot.Availability, group!);
        var members = GatedMembers(read, read.Snapshot, read.Snapshot.Availability, group!, includeObservedPreBaseline: true);
        var observedTotal = members.Count;
        var totalPages = observedTotal == 0 ? 1 : (int)Math.Ceiling(observedTotal / (double)pageSize);
        var effectivePage = Math.Min(page, totalPages);
        var rows = members
            .Skip((effectivePage - 1) * pageSize)
            .Take(pageSize)
            .Select(area => ToAreaRowDto(area, read.Snapshot.Availability, group))
            .ToArray();

        return Results.Ok(new OverviewAreaListDto(
            summary,
            group!,
            effectivePage,
            pageSize,
            ToCount(observedTotal, sectionAvailability),
            rows));
    }

    private static async Task<IResult> AreaAsync(
        int areaId,
        HttpRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider clock,
        OverviewReadQuery query,
        CancellationToken ct)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        if (areaId <= 0)
        {
            return Bad("รหัสพื้นที่ไม่ถูกต้อง");
        }

        if (!TryParseCommonQuery(request.Query, clock.GetUtcNow().UtcDateTime, out var slot, out var buildingId, out var error)
            || !TryParseOptionalGroup(request.Query, out var group, out error)
            || !TryParseOptionalRoundKey(request.Query, out var roundKey, out error))
        {
            return Bad(error!);
        }

        var read = await query.ReadAsync(slot, buildingId, ct);
        if (!read.BuildingKnown)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบตึกนี้ในบริบทที่เลือก");
        }

        var area = read.Snapshot.Areas.FirstOrDefault(a => a.Id == areaId)
            ?? read.ObservedAreas.FirstOrDefault(a => a.Id == areaId);
        if (area is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบพื้นที่นี้ในวันที่และกะที่เลือก");
        }

        var availability = read.Snapshot.Availability;
        var points = availability.Work.State is "notStarted" or "unavailable"
            ? Array.Empty<OverviewPointDetailDto>()
            : BuildPointDetails(read, area);
        var rounds = availability.History.State is "notStarted" or "unavailable"
            ? Array.Empty<OverviewRoundResultDto>()
            : area.History.Select(ToRoundResultDto).ToArray();
        var currentRoundKeys = points
            .Where(p => p.RoundKey is not null)
            .Select(p => p.RoundKey!)
            .ToHashSet(StringComparer.Ordinal);

        if (roundKey is not null)
        {
            var matched = OverviewTimeline.FindRound(read.Snapshot.Targets, roundKey);
            var belongsToArea = matched is not null
                && matched.AreaId == area.Id
                && (buildingId is null || matched.BuildingId == buildingId.Value)
                && (rounds.Any(r => r.RoundKey == roundKey) || currentRoundKeys.Contains(roundKey));
            if (!belongsToArea)
            {
                return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบรอบที่เลือกของพื้นที่นี้");
            }
        }

        var matchesGroup = group is null
            || (SectionForGroup(availability, group).State is not ("notStarted" or "unavailable")
                && area.Groups.Contains(group));

        var filteredFacts = FilterFactsForArea(read, area, points, rounds, availability, out var areaResponsibility, out var areaEvidence);

        var context = BuildContextDto(read, buildingId);
        var areaRow = ToDetailAreaRowDto(area, availability, group);

        return Results.Ok(new OverviewAreaDetailDto(
            context,
            availability,
            areaRow,
            matchesGroup,
            areaEvidence,
            areaResponsibility,
            filteredFacts,
            points,
            rounds,
            roundKey));
    }

    private static OverviewSummaryDto BuildSummaryDto(OverviewRead read, int? buildingId)
    {
        var snapshot = read.Snapshot;
        var availability = snapshot.Availability;
        var hasTargetHistory = snapshot.Targets.Current is not null || snapshot.Targets.Rounds.Count > 0;
        var context = BuildContextDto(read, buildingId);

        var entry = BuildEntryMetric(read, snapshot, availability, includeObservedPreBaseline: !hasTargetHistory);
        var submission = BuildWorkMetric(snapshot, availability, OverviewGroupKeys.Submitted, availability.Work);
        var inspection = BuildWorkMetric(snapshot, availability, OverviewGroupKeys.Inspected, availability.Inspection);
        var groups = BuildGroupCounts(read, snapshot, availability, includeObservedPreBaseline: !hasTargetHistory);

        var buildings = new List<OverviewBuildingDto>(read.Buildings.Count);
        foreach (var building in read.Buildings)
        {
            var bSnapshot = read.BuildingSnapshots.TryGetValue(building.Id, out var bs)
                ? bs
                : snapshot with { Areas = [] };
            var bAvail = read.BuildingAvailability.TryGetValue(building.Id, out var ba)
                ? ba
                : availability;

            buildings.Add(new OverviewBuildingDto(
                building.Id,
                building.Code,
                building.Name,
                BuildEntryMetric(read, bSnapshot, bAvail, includeObservedPreBaseline: false),
                BuildWorkMetric(bSnapshot, bAvail, OverviewGroupKeys.Submitted, bAvail.Work),
                BuildWorkMetric(bSnapshot, bAvail, OverviewGroupKeys.Inspected, bAvail.Inspection),
                BuildGroupCounts(read, bSnapshot, bAvail, includeObservedPreBaseline: false)));
        }

        return new OverviewSummaryDto(
            context,
            availability,
            entry,
            submission,
            inspection,
            groups,
            buildings);
    }

    private static OverviewContextDto BuildContextDto(OverviewRead read, int? buildingId)
    {
        var slot = read.Snapshot.Slot;
        var moment = read.Snapshot.Moment;
        return new OverviewContextDto(
            slot.ShiftDate,
            slot.Shift,
            buildingId,
            moment.Phase,
            slot == ShiftCalendar.SlotAt(read.ReadAtUtc),
            Utc(moment.EvaluationUtc),
            Utc(read.ReadAtUtc),
            moment.CloseUtc is { } close ? Utc(close) : null,
            read.Snapshot.Targets.CurrentRevisionId,
            read.Range);
    }

    private static OverviewMetricDto BuildEntryMetric(
        OverviewRead read,
        OverviewSnapshot snapshot,
        SectionAvailability availability,
        bool includeObservedPreBaseline)
    {
        var enteredCount = GatedMembers(read, snapshot, availability, OverviewGroupKeys.Entered, includeObservedPreBaseline).Count;
        var numerator = ToCount(enteredCount, availability.Entry);

        var denominatorAvailability = EntryDenominatorAvailability(availability);
        int denominatorObserved;
        if (denominatorAvailability.State is "notStarted" or "unavailable")
        {
            denominatorObserved = 0;
        }
        else if (snapshot.Targets.Current is not null || snapshot.Targets.Rounds.Count > 0)
        {
            denominatorObserved = snapshot.Areas
                .Where(a => a.Groups.Contains(OverviewGroupKeys.Entered)
                    || a.Groups.Contains(OverviewGroupKeys.Covered)
                    || a.Groups.Contains(OverviewGroupKeys.Unassigned))
                .Select(a => a.Id)
                .Distinct()
                .Count();
        }
        else
        {
            denominatorObserved = includeObservedPreBaseline ? read.ObservedAreas.Count : 0;
        }

        var denominator = ToCount(denominatorObserved, denominatorAvailability);
        return new OverviewMetricDto(numerator, denominator);
    }

    private static OverviewMetricDto BuildWorkMetric(
        OverviewSnapshot snapshot,
        SectionAvailability availability,
        string numeratorGroup,
        Availability numeratorAvailability)
    {
        var numeratorCount = GatedMembers(null, snapshot, availability, numeratorGroup, includeObservedPreBaseline: false).Count;
        var numerator = ToCount(numeratorCount, numeratorAvailability);

        var denominatorAvailability = WorkDenominatorAvailability(numeratorAvailability);
        var denominatorObserved = denominatorAvailability.State is "notStarted" or "unavailable"
            ? 0
            : snapshot.Areas
                .Where(a => a.Work.DuePoints > 0)
                .Select(a => a.Id)
                .Distinct()
                .Count();
        var denominator = ToCount(denominatorObserved, denominatorAvailability);
        return new OverviewMetricDto(numerator, denominator);
    }

    private static Availability EntryDenominatorAvailability(SectionAvailability availability)
    {
        if (availability.Entry.State is "notStarted" or "unavailable")
        {
            return availability.Entry;
        }

        var reasons = availability.Entry.Reasons
            .Where(r => !string.Equals(r, "attendancePartlyExpired", StringComparison.Ordinal))
            .ToArray();
        return reasons.Length == 0
            ? new Availability("complete", [])
            : new Availability("partial", reasons);
    }

    private static Availability WorkDenominatorAvailability(Availability section)
    {
        if (section.State is "notStarted" or "unavailable")
        {
            return section;
        }

        var reasons = section.Reasons
            .Where(r => !string.Equals(r, "originsIncomplete", StringComparison.Ordinal))
            .ToArray();
        return reasons.Length == 0
            ? new Availability("complete", [])
            : new Availability("partial", reasons);
    }

    private static Dictionary<string, OverviewCount> BuildGroupCounts(
        OverviewRead read,
        OverviewSnapshot snapshot,
        SectionAvailability availability,
        bool includeObservedPreBaseline)
    {
        var counts = new Dictionary<string, OverviewCount>(OverviewGroupKeys.All.Count, StringComparer.Ordinal);
        foreach (var key in OverviewGroupKeys.All)
        {
            var section = SectionForGroup(availability, key);
            var observed = GatedMembers(read, snapshot, availability, key, includeObservedPreBaseline).Count;
            counts[key] = ToCount(observed, section);
        }

        return counts;
    }

    private static IReadOnlyList<OverviewArea> GatedMembers(
        OverviewRead? read,
        OverviewSnapshot snapshot,
        SectionAvailability availability,
        string group,
        bool includeObservedPreBaseline)
    {
        var section = SectionForGroup(availability, group);
        if (section.State is "notStarted" or "unavailable")
        {
            return [];
        }

        var members = OverviewProjection.Members(snapshot, group);
        if (!includeObservedPreBaseline || read is null || read.ObservedAreas.Count == 0
            || snapshot.Targets.Current is not null || snapshot.Targets.Rounds.Count > 0)
        {
            return members;
        }

        return read.ObservedAreas
            .Where(a => a.Groups.Contains(group))
            .OrderBy(a => a.BuildingCode, StringComparer.Ordinal)
            .ThenBy(a => a.Code, StringComparer.Ordinal)
            .ThenBy(a => a.Id)
            .ToArray();
    }

    private static OverviewCount ToCount(int observed, Availability availability) =>
        availability.State == "complete"
            ? new OverviewCount(observed, observed, availability)
            : new OverviewCount(null, observed, availability);

    private static Availability SectionForGroup(SectionAvailability availability, string group) => group switch
    {
        OverviewGroupKeys.Entered or OverviewGroupKeys.Covered
            or OverviewGroupKeys.Unassigned or OverviewGroupKeys.NotEntered => availability.Entry,
        OverviewGroupKeys.Inspected or OverviewGroupKeys.PendingInspection
            or OverviewGroupKeys.Rework => availability.Inspection,
        OverviewGroupKeys.Issue => availability.Issue,
        OverviewGroupKeys.MissedCleaning or OverviewGroupKeys.MissedInspection => availability.History,
        _ => availability.Work,
    };

    private static OverviewAreaRowDto ToAreaRowDto(OverviewArea area, SectionAvailability availability, string? group)
    {
        var reasons = OverviewGroupKeys.All
            .Where(key => SectionForGroup(availability, key).State is not ("notStarted" or "unavailable")
                && area.Groups.Contains(key))
            .ToArray();

        string[] roundKeys = availability.History.State is "notStarted" or "unavailable"
            ? []
            : group switch
            {
                OverviewGroupKeys.MissedCleaning => area.History
                    .Where(h => !h.IsPartial && h.Outcome == OverviewOutcomes.NotDone)
                    .Select(h => h.RoundKey)
                    .ToArray(),
                OverviewGroupKeys.MissedInspection => area.History
                    .Where(h => !h.IsPartial && h.Outcome == OverviewOutcomes.NotInspected)
                    .Select(h => h.RoundKey)
                    .ToArray(),
                _ => [],
            };

        return new OverviewAreaRowDto(
            area.Id,
            area.BuildingId,
            area.Code,
            area.Name,
            area.BuildingCode,
            area.BuildingName,
            area.Entry,
            area.Work,
            reasons,
            roundKeys,
            area.OriginsIncomplete);
    }

    private static OverviewAreaRowDto ToDetailAreaRowDto(OverviewArea area, SectionAvailability availability, string? group)
    {
        var row = ToAreaRowDto(area, availability, group);
        var entry = availability.Entry.State is "notStarted" or "unavailable"
            ? ScheduleEntryEvidence.Unassigned
            : row.Entry;
        var work = availability.Work.State is "notStarted" or "unavailable"
            ? new AreaWork(0, 0, 0, false, false, false, false, false, false,
                availability.Issue.State is not ("notStarted" or "unavailable") && row.Work.Issue, false)
            : row.Work;
        var originsIncomplete = availability.Work.State is not ("notStarted" or "unavailable") && row.OriginsIncomplete;
        return row with { Entry = entry, Work = work, OriginsIncomplete = originsIncomplete };
    }

    private static IReadOnlyList<OverviewPointDetailDto> BuildPointDetails(OverviewRead read, OverviewArea area)
    {
        var slot = read.Snapshot.Slot;
        var evaluationUtc = read.Snapshot.Moment.EvaluationUtc;

        var currentRoundsByPoint = read.Snapshot.Targets.Rounds
            .Where(r => r.AreaId == area.Id && r.BuildingId == area.BuildingId
                && (r.FinalizedAtUtc is null || r.FinalizedReason == "slotClosed"))
            .GroupBy(r => r.PointId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.ActiveFromUtc).ThenBy(r => r.ActivationRevisionId).Last());

        var upcomingByPoint = new Dictionary<int, Queue<TargetWindow>>();
        if (read.Snapshot.Targets.Current is { } image)
        {
            var activeBuildings = image.Buildings
                .Where(b => b.IsActive && b.CreatedAt <= evaluationUtc)
                .Select(b => b.Id)
                .ToHashSet();
            var activeAreas = image.Areas
                .Where(a => a.IsActive && a.CreatedAt <= evaluationUtc && a.Id == area.Id
                    && a.BuildingId == area.BuildingId && activeBuildings.Contains(a.BuildingId)
                    && (slot.Shift == Shift.Day || a.ShiftPattern == ShiftPattern.DayAndNight))
                .Select(a => a.Id)
                .ToHashSet();
            var activePoints = image.Points
                .Where(p => p.IsActive && p.CreatedAt <= evaluationUtc && activeAreas.Contains(p.AreaId))
                .Select(p => p.Id)
                .ToHashSet();

            foreach (var window in image.Windows
                .Where(w => w.Shift == slot.Shift && w.CreatedAt <= evaluationUtc && activePoints.Contains(w.PointId))
                .OrderBy(w => w.Start).ThenBy(w => w.Id))
            {
                if (ShiftCalendar.ToUtc(slot, window.Start) > evaluationUtc)
                {
                    if (!upcomingByPoint.TryGetValue(window.PointId, out var queue))
                    {
                        upcomingByPoint[window.PointId] = queue = new Queue<TargetWindow>();
                    }
                    queue.Enqueue(window);
                }
            }
        }

        var details = new List<OverviewPointDetailDto>(area.Points.Count);
        foreach (var point in area.Points)
        {
            read.PointMetadata.TryGetValue(point.PointId, out var meta);
            string? roundKey = null;
            TimeOnly? start = null;
            TimeOnly? end = null;

            if (point.IsDue && currentRoundsByPoint.TryGetValue(point.PointId, out var round))
            {
                roundKey = OverviewTimeline.RoundKey(slot, round);
                start = round.Start;
                end = round.End;
            }
            else if (point.IsUpcoming && upcomingByPoint.TryGetValue(point.PointId, out var queue) && queue.Count > 0)
            {
                var window = queue.Dequeue();
                start = window.Start;
                end = window.End;
            }
            else if (point.SubmissionId is { } subId)
            {
                var scan = read.Facts.Submissions.FirstOrDefault(s => s.Id == subId);
                start = scan?.RoundStart;
                end = scan?.RoundEnd;
            }

            details.Add(new OverviewPointDetailDto(
                point,
                meta?.Code,
                meta?.Name ?? $"Point {point.PointId}",
                meta?.SortOrder ?? 0,
                roundKey,
                start,
                end));
        }

        return details
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Start)
            .ThenBy(d => d.Work.PointId)
            .ToArray();
    }

    private static OverviewRoundResultDto ToRoundResultDto(OverviewRoundResult result) =>
        new(
            result.RoundKey,
            result.Round,
            result.Outcome,
            result.SubmissionId,
            result.InspectionId,
            result.IsPartial,
            result.UnresolvedSubmissions?.Select(s => new OverviewUnresolvedSubmissionDto(
                s.Id,
                s.PointId,
                Utc(s.SubmittedAt),
                s.Placement,
                s.RoundStart,
                s.RoundEnd)).ToArray());

    private static OverviewFacts FilterFactsForArea(
        OverviewRead read,
        OverviewArea area,
        IReadOnlyList<OverviewPointDetailDto> points,
        IReadOnlyList<OverviewRoundResultDto> rounds,
        SectionAvailability availability,
        out OverviewResponsibilityDto? responsibility,
        out IReadOnlyList<AreaEntryEvidence> entryEvidence)
    {
        if (availability.Entry.State is "notStarted" or "unavailable")
        {
            entryEvidence = [];
            responsibility = null;
        }
        else
        {
            entryEvidence = read.Facts.EntryEvidence
                .Where(e => e.AreaId == area.Id)
                .OrderBy(e => e.RecordedAt)
                .ToArray();
            responsibility = read.AreaResponsibilities.TryGetValue(area.Id, out var resp)
                ? new OverviewResponsibilityDto(
                    resp.UserId,
                    resp.AreaId,
                    resp.State,
                    resp.LastConfirmedAreaId,
                    resp.ConfirmedAt is { } confirmedAt ? Utc(confirmedAt) : null)
                : null;
        }

        var pointIds = new HashSet<int>();
        foreach (var p in points) pointIds.Add(p.Work.PointId);
        foreach (var r in rounds)
        {
            pointIds.Add(r.Round.PointId);
            if (r.UnresolvedSubmissions is not null)
            {
                foreach (var u in r.UnresolvedSubmissions) pointIds.Add(u.PointId);
            }
        }
        foreach (var pair in read.PointAreaMap)
        {
            if (pair.Value == area.Id && read.Facts.Submissions.Any(s => s.PointId == pair.Key && s.ShiftDate == read.Snapshot.Slot.ShiftDate && s.Shift == read.Snapshot.Slot.Shift))
            {
                pointIds.Add(pair.Key);
            }
        }

        var allowWorkFacts = availability.Work.State is not ("notStarted" or "unavailable");
        var submissions = allowWorkFacts
            ? read.Facts.Submissions.Where(s => pointIds.Contains(s.PointId)).ToArray()
            : [];
        var submissionIds = submissions.Select(s => s.Id).ToHashSet();
        var inspections = allowWorkFacts
            ? read.Facts.Inspections.Where(i => submissionIds.Contains(i.SubmissionId)).ToArray()
            : [];

        var allowEntryFacts = availability.Entry.State is not ("notStarted" or "unavailable");
        var attendance = allowEntryFacts
            ? read.Facts.Attendance.Where(a => a.AreaId == area.Id).ToArray()
            : [];
        var covers = allowEntryFacts
            ? read.Facts.Covers.Where(c => c.AreaId == area.Id).ToArray()
            : [];
        var assignedAreas = allowEntryFacts && read.Facts.AssignedAreas.Contains(area.Id)
            ? new HashSet<int> { area.Id }
            : new HashSet<int>();

        var referencedPeople = submissions.Select(s => s.UserId)
            .Concat(inspections.Select(i => i.SupervisorId))
            .Concat(attendance.Select(a => a.UserId))
            .Concat(covers.Select(c => c.UserId))
            .Concat(responsibility is not null ? [responsibility.UserId] : Array.Empty<int>())
            .ToHashSet();
        var people = read.Facts.People
            .Where(p => referencedPeople.Contains(p.Id))
            .ToArray();

        var areaOriginsComplete = !area.OriginsIncomplete && !pointIds.Any(read.UnresolvedOriginPointIds.Contains);
        var areaIssueKnown = availability.Issue.State == "complete";

        return new OverviewFacts(
            submissions,
            inspections,
            attendance,
            covers,
            people,
            entryEvidence,
            assignedAreas,
            areaOriginsComplete,
            areaIssueKnown);
    }

    private static bool TryParseCommonQuery(
        IQueryCollection query,
        DateTime nowUtc,
        out ShiftSlot slot,
        out int? buildingId,
        out string? error)
    {
        slot = ShiftCalendar.SlotAt(nowUtc);
        buildingId = null;

        if (!TrySingleQueryValue(query, "shiftDate", out var shiftDateText)
            || !TrySingleQueryValue(query, "shift", out var shiftText)
            || !TrySingleQueryValue(query, "buildingId", out var buildingIdText))
        {
            error = "พารามิเตอร์ซ้ำกัน";
            return false;
        }

        if (shiftDateText is not null || shiftText is not null)
        {
            if (shiftDateText is null || shiftText is null
                || !AdminQuery.TryReadDate(shiftDateText, out var parsedDate)
                || !AdminQuery.TryReadEnum<Shift>(shiftText, out var parsedShift))
            {
                error = "เลือกวันที่และกะให้ครบทั้งสองช่อง";
                return false;
            }

            slot = new ShiftSlot(parsedDate, parsedShift);
        }

        var thaiToday = DateOnly.FromDateTime(ThaiTime.FromUtc(nowUtc));
        if (slot.ShiftDate > thaiToday)
        {
            error = "ไม่สามารถดูวันที่ในอนาคตได้";
            return false;
        }

        if (buildingIdText is not null)
        {
            if (!TryParsePositiveInt(buildingIdText, out var parsedBuildingId))
            {
                error = "รหัสตึกไม่ถูกต้อง";
                return false;
            }

            buildingId = parsedBuildingId;
        }

        error = null;
        return true;
    }

    private static bool TryParseRequiredGroup(IQueryCollection query, out string? group, out string? error)
    {
        group = null;
        if (!TrySingleQueryValue(query, "group", out var text))
        {
            error = "พารามิเตอร์ group ซ้ำกัน";
            return false;
        }

        if (string.IsNullOrEmpty(text) || !OverviewGroupKeys.IsKnown(text))
        {
            error = "หมวดหมู่ไม่ถูกต้อง";
            return false;
        }

        group = text;
        error = null;
        return true;
    }

    private static bool TryParseOptionalGroup(IQueryCollection query, out string? group, out string? error)
    {
        group = null;
        if (!TrySingleQueryValue(query, "group", out var text))
        {
            error = "พารามิเตอร์ group ซ้ำกัน";
            return false;
        }

        if (text is not null && !OverviewGroupKeys.IsKnown(text))
        {
            error = "หมวดหมู่ไม่ถูกต้อง";
            return false;
        }

        group = text;
        error = null;
        return true;
    }

    private static bool TryParsePaging(IQueryCollection query, out int page, out int pageSize, out string? error)
    {
        page = 1;
        pageSize = DefaultPageSize;

        if (!TrySingleQueryValue(query, "page", out var pageText)
            || !TrySingleQueryValue(query, "pageSize", out var pageSizeText))
        {
            error = "พารามิเตอร์หน้าซ้ำกัน";
            return false;
        }

        if (pageText is not null)
        {
            if (!TryParsePositiveInt(pageText, out page))
            {
                error = "เลขหน้าไม่ถูกต้อง";
                return false;
            }
        }

        if (pageSizeText is not null)
        {
            if (!TryParsePositiveInt(pageSizeText, out pageSize) || pageSize > MaxPageSize)
            {
                error = "ขนาดหน้าต้องอยู่ระหว่าง 1 ถึง 50";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryParseOptionalRoundKey(IQueryCollection query, out string? roundKey, out string? error)
    {
        roundKey = null;
        if (!TrySingleQueryValue(query, "roundKey", out var text))
        {
            error = "พารามิเตอร์ roundKey ซ้ำกัน";
            return false;
        }

        if (text is null)
        {
            error = null;
            return true;
        }

        if (!IsValidRoundKeySyntax(text))
        {
            error = "รูปแบบ roundKey ไม่ถูกต้อง";
            return false;
        }

        roundKey = text;
        error = null;
        return true;
    }

    private static bool IsValidRoundKeySyntax(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var parts = key.Split(':');
        var culture = CultureInfo.InvariantCulture;
        if (parts.Length != 6
            || !DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", culture, DateTimeStyles.None, out _)
            || (parts[1] != nameof(Shift.Day) && parts[1] != nameof(Shift.Night))
            || !TryParsePositiveInt(parts[2], out _)
            || !TryParsePositiveInt(parts[3], out _)
            || !long.TryParse(parts[4], NumberStyles.None, culture, out var revisionId) || revisionId <= 0)
        {
            return false;
        }

        var times = parts[5].Split('-');
        return times.Length == 2
            && TimeOnly.TryParseExact(times[0], "HHmm", culture, DateTimeStyles.None, out _)
            && TimeOnly.TryParseExact(times[1], "HHmm", culture, DateTimeStyles.None, out _);
    }

    private static bool TrySingleQueryValue(IQueryCollection query, string key, out string? value)
    {
        value = null;
        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return true;
        }

        if (values.Count > 1)
        {
            return false;
        }

        value = values[0];
        return true;
    }

    private static bool TryParsePositiveInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
