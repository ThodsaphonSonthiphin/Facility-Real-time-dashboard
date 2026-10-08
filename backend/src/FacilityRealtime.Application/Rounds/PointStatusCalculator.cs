using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Rounds;

/// <summary>CONTEXT.md, Point Status. Declared in priority order: when two apply, the earlier one wins.</summary>
public enum PointStatus
{
    /// <summary>ต้องแก้ไข: the latest inspection said rework and nothing was submitted after it.</summary>
    Rework,

    /// <summary>เลยรอบ: the round's end has passed and nothing was submitted for it.</summary>
    Overdue,

    /// <summary>รอตรวจ: something was submitted after the latest inspection (or there is none).</summary>
    PendingInspection,

    /// <summary>ตรวจผ่าน.</summary>
    Passed,

    /// <summary>ยังไม่ทำ: inside the round, nothing submitted yet.</summary>
    NotYetDone,

    /// <summary>ยังไม่ถึงรอบ: the shift has started but its first round has not.</summary>
    BeforeFirstRound,

    /// <summary>นอกเวลา: the Area does not work this shift (or is deactivated), or the point has no round this shift.</summary>
    OffHours,
}

public sealed record PointStatusResult(PointStatus Status, RoundWindow? CurrentRound, RoundWindow? NextRound);

/// <summary>ADR facility-0047 rules 2, 4 and 5. Issue is not a status (facility-0046); callers show it as a separate tag.</summary>
public static class PointStatusCalculator
{
    public static PointStatusResult Calculate(
        bool areaWorksThisShift,
        IReadOnlyList<RoundWindow> windows,
        IReadOnlyList<SubmissionFact> submissions,
        IReadOnlyList<InspectionFact> inspections,
        DateTime nowUtc)
    {
        if (!areaWorksThisShift || windows.Count == 0)
        {
            return new PointStatusResult(PointStatus.OffHours, null, null);
        }

        var current = RoundRules.Current(windows, nowUtc);
        var next = RoundRules.Next(windows, nowUtc);
        if (current is null)
        {
            return new PointStatusResult(PointStatus.BeforeFirstRound, null, next);
        }

        // Rule 5: only this round's submissions count, so a new round always starts the card over
        var roundSubmissions = RoundRules.SubmissionsOf(current, submissions);
        var latest = RoundRules.LatestInspection(roundSubmissions, inspections);

        var status =
            RoundRules.NeedsRework(roundSubmissions, latest) ? PointStatus.Rework
            : roundSubmissions.Count == 0 && nowUtc > current.EndUtc ? PointStatus.Overdue
            : roundSubmissions.Any(s => latest is null || s.SubmittedAt > latest.InspectedAt) ? PointStatus.PendingInspection
            : latest is { Result: InspectionResult.Passed } ? PointStatus.Passed
            : PointStatus.NotYetDone;

        return new PointStatusResult(status, current, next);
    }
}
