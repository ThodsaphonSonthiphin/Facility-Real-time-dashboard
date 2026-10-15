using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Inspections;

/// <summary>An active Supervisor and their beat: their own building in their own shift.</summary>
public sealed record SummarySupervisor(int Id, int BuildingId, Shift Shift);

/// <summary>A point, active or not. Counted: an active point of an active Area. WorksNight: its Area has a night shift.</summary>
public sealed record SummaryPoint(int Id, int BuildingId, bool Counted, bool WorksNight);

/// <summary>One Inspection Record; Slot is the shift of the Scan Record it judged.</summary>
public sealed record SummaryInspection(long ScanRecordId, int PointId, int SupervisorId, ShiftSlot Slot, InspectionResult Result, bool? WithinRadius);

/// <summary>One row of the Inspection Summary (wireframe SupervisorSummary). Percentages are left to the page.</summary>
public sealed record SupervisorTally(
    int SupervisorId,
    int PointsInspected,
    int PointsTotal,
    int Submitted,
    int SubmittedInspected,
    int NotInspected,
    int Inspections,
    int Reworks,
    int GpsFlagged);

/// <summary>facility-0048 rule 5: the numbers the Admin weighs a Supervisor by. The system shows them; it does not judge.</summary>
public static class InspectionSummary
{
    /// <summary>
    /// Plan 6 decision 15. Beat numbers (points, submissions, ไม่ได้ตรวจ) are about the Supervisor's own building in their
    /// own shift, whoever inspected; the inspection counts are the Supervisor's own, anywhere, covers included.
    /// Plan 6 decision 17: a submission made after the shift's end (a Cleaner's 60-minute window, filed Late or as a rework
    /// fix) can never be inspected, because a Supervisor inspects only in the clock's shift. It is left out of the beat's
    /// Submitted and SubmittedInspected, and a NotInspected round whose latest submission is such work is left out of
    /// NotInspected. PointsInspected, Inspections, Reworks and GpsFlagged are unchanged; the export still lists the round.
    /// </summary>
    public static IReadOnlyList<SupervisorTally> Tally(
        IReadOnlyList<SummarySupervisor> supervisors,
        IReadOnlyList<SummaryPoint> points,
        IReadOnlyList<RoundSubmission> submissions,
        IReadOnlyList<SummaryInspection> inspections,
        IReadOnlyList<FinishedRound> rounds)
    {
        var pointOf = points.ToDictionary(p => p.Id);
        var inspectedIds = inspections.Select(i => i.ScanRecordId).ToHashSet();
        static bool AfterShift(RoundSubmission s) => s.SubmittedAtUtc > ShiftCalendar.EndUtc(s.Slot);
        bool LatestAfterShift(FinishedRound r) =>
            submissions
                .Where(s => s.PointId == r.PointId && s.Slot == r.Slot && s.RoundStart == r.RoundStart && s.RoundEnd == r.RoundEnd)
                .OrderByDescending(s => s.SubmittedAtUtc)
                .FirstOrDefault() is { } latest && AfterShift(latest);

        return supervisors.Select(supervisor =>
        {
            bool InBeat(int pointId, Shift shift) =>
                shift == supervisor.Shift && pointOf.TryGetValue(pointId, out var point) && point.BuildingId == supervisor.BuildingId;

            var beatPoints = points
                .Where(p => p.Counted && p.BuildingId == supervisor.BuildingId && (supervisor.Shift == Shift.Day || p.WorksNight))
                .Select(p => p.Id)
                .ToHashSet();
            var own = inspections.Where(i => i.SupervisorId == supervisor.Id).ToList();
            var beatSubmissions = submissions.Where(s => InBeat(s.PointId, s.Slot.Shift) && !AfterShift(s)).ToList();

            return new SupervisorTally(
                supervisor.Id,
                own.Where(i => i.Slot.Shift == supervisor.Shift && beatPoints.Contains(i.PointId)).Select(i => i.PointId).Distinct().Count(),
                beatPoints.Count,
                beatSubmissions.Count,
                beatSubmissions.Count(s => inspectedIds.Contains(s.Id)),
                rounds.Count(r => r.Outcome == RoundOutcome.NotInspected && InBeat(r.PointId, r.Slot.Shift) && !LatestAfterShift(r)),
                own.Count,
                own.Count(i => i.Result == InspectionResult.Rework),
                own.Count(i => i.WithinRadius == false));
        }).ToList();
    }
}
