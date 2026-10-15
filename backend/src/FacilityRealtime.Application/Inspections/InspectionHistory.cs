using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;

namespace FacilityRealtime.Application.Inspections;

/// <summary>A Scan Record placed in a round (OnTime, Late or Rework), with the round's start and end as copied onto it when saved.</summary>
public sealed record RoundSubmission(long Id, int PointId, ShiftSlot Slot, TimeOnly RoundStart, TimeOnly RoundEnd, DateTime SubmittedAtUtc);

/// <summary>One round of one point in one shift that had a submission, and what it left in the history.</summary>
public sealed record FinishedRound(int PointId, ShiftSlot Slot, TimeOnly RoundStart, TimeOnly RoundEnd, RoundOutcome Outcome);

public static class InspectionHistory
{
    /// <summary>
    /// Plan 6 decision 13: the outcome of every round with a submission, only in shifts whose attendance window has closed —
    /// the same rule as "ไม่ได้ทำ" in ExportRules.MissedRounds — in the order the shifts met them. A round is its point,
    /// shift and the start and end copied onto its submissions, so a window deleted since keeps its history.
    /// </summary>
    public static IReadOnlyList<FinishedRound> FinishedRounds(
        IReadOnlyList<RoundSubmission> submissions, IReadOnlyList<InspectionFact> inspections, DateTime nowUtc, int closesMinutesAfterShift)
    {
        var bySubmission = inspections.ToLookup(i => i.SubmissionId);
        return submissions
            .Where(s => ShiftCalendar.EndUtc(s.Slot).AddMinutes(closesMinutesAfterShift) < nowUtc)
            .GroupBy(s => (s.PointId, s.Slot, s.RoundStart, s.RoundEnd))
            .Select(round =>
            {
                var facts = round.Select(s => new SubmissionFact(s.Id, null, s.SubmittedAtUtc)).ToList();
                var roundInspections = round.SelectMany(s => bySubmission[s.Id]).ToList();
                return new FinishedRound(
                    round.Key.PointId, round.Key.Slot, round.Key.RoundStart, round.Key.RoundEnd, RoundOutcomes.Of(facts, roundInspections));
            })
            .OrderBy(r => r.Slot.ShiftDate)
            .ThenBy(r => r.Slot.Shift)
            .ThenBy(r => ShiftCalendar.ToUtc(r.Slot, r.RoundStart))
            .ThenBy(r => r.PointId)
            .ToList();
    }
}
