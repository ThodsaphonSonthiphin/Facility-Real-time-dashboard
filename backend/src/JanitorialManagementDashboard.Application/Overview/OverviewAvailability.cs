using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Overview;

public sealed record DetailCoverage(DateTime AttendanceAfterUtc, DateOnly WorkFromShiftDate,
    DateTime? TargetCaptureFromUtc, bool IssueStateKnown, IReadOnlyList<(DateTime From, DateTime To)> Gaps)
{
    public DateTime? ReadAtUtc { get; init; }

    // The read boundary supplies this evidence for invalid revisions, including their unknown receiving policies.
    public IReadOnlyList<(DateTime From, DateTime To)> UnreadableTargetIntervals { get; init; } = [];

    public static DetailCoverage FromRetention(DateTime nowUtc, RetentionSettings settings,
        DateTime? targetCaptureFromUtc, bool issueStateKnown, IReadOnlyList<(DateTime From, DateTime To)> gaps) =>
        new(settings.RecordCutoffUtc(nowUtc), settings.WorkFromShiftDate(nowUtc), targetCaptureFromUtc, issueStateKnown, gaps)
        {
            ReadAtUtc = nowUtc,
        };
}

public sealed record Availability(string State, string[] Reasons);
public sealed record SectionAvailability(Availability Entry, Availability Work, Availability Inspection,
    Availability History, Availability Issue);

public sealed record OverviewSlotAvailability(ShiftSlot Slot, SectionAvailability Sections)
{
    public bool DetailComplete =>
        Sections.Entry.State == "complete" && Sections.Work.State == "complete"
        && Sections.Inspection.State == "complete" && Sections.History.State == "complete";

    public bool FullySupported => DetailComplete && Sections.Issue.State == "complete";
}

public static class OverviewAvailability
{
    public static SectionAvailability Calculate(ShiftSlot slot, OverviewMoment moment, TargetTimeline timeline, DetailCoverage coverage)
    {
        if (moment.Phase == "notStarted")
        {
            var notStarted = new Availability("notStarted", ["shiftNotStarted"]);
            return new(notStarted, notStarted, notStarted, notStarted, notStarted);
        }

        var start = ShiftCalendar.StartUtc(slot);
        var evaluation = moment.EvaluationUtc;
        var firstPossibleOpen = FirstPossibleOpen(slot);
        var policyUnreadable = Intersects(coverage.UnreadableTargetIntervals, firstPossibleOpen, evaluation);
        var earliestAttendance = EarliestAttendance(start, evaluation, timeline.ReceivingHistory);
        var openingKnown = earliestAttendance.HasValue && timeline.ReceivingHistory[0].EffectiveAt <= firstPossibleOpen
            && coverage.TargetCaptureFromUtc.GetValueOrDefault() <= firstPossibleOpen;
        var receivingKnown = moment.PolicyComplete && openingKnown && !policyUnreadable;
        var gaps = coverage.Gaps.Concat(timeline.StructuralGaps);
        var currentGap = Intersects(gaps, evaluation, evaluation);
        var currentUnreadable = Intersects(coverage.UnreadableTargetIntervals,
            moment.Phase == "running" ? evaluation : ShiftCalendar.EndUtc(slot), evaluation);
        var current = new List<string>();
        if (!timeline.CurrentComplete || coverage.TargetCaptureFromUtc > evaluation)
            current.Add("targetCoverageMissing");
        if (currentGap) current.Add("targetCoverageGap");
        if (currentUnreadable) current.Add("targetRevisionUnreadable");

        var entryReasons = new List<string>(current);
        if (!receivingKnown) entryReasons.Add("receivingPolicyUnknown");
        if ((openingKnown && !policyUnreadable ? earliestAttendance : firstPossibleOpen) <= coverage.AttendanceAfterUtc)
            entryReasons.Add("attendancePartlyExpired");
        // An unreadable policy could have kept receiving open after the provisional evaluation time.
        var readAt = coverage.ReadAtUtc ?? (moment.Phase == "closed" ? DateTime.MaxValue : evaluation);
        var lastPossibleAttendance = moment is { PolicyComplete: true, CloseUtc: { } close } && !currentUnreadable
            ? Earlier(evaluation, close) : Earlier(readAt, LastPossibleClose(slot));
        var entry = lastPossibleAttendance <= coverage.AttendanceAfterUtc
            ? new Availability("unavailable", ["attendanceExpired"])
            : FromReasons(entryReasons);

        var workRetained = slot.ShiftDate >= coverage.WorkFromShiftDate;
        var work = workRetained ? FromReasons(current) : new Availability("unavailable", ["workArchived"]);

        var historyReasons = new List<string>();
        if (!timeline.HistoryComplete || coverage.TargetCaptureFromUtc > start) historyReasons.Add("targetCoverageMissing");
        if (!receivingKnown) historyReasons.Add("receivingPolicyUnknown");
        if (Intersects(gaps, start, evaluation)) historyReasons.Add("targetCoverageGap");
        if (policyUnreadable) historyReasons.Add("targetRevisionUnreadable");
        var history = workRetained ? FromReasons(historyReasons) : work;

        var issueReasons = new List<string>(current);
        if (!coverage.IssueStateKnown) issueReasons.Add("issueStateUnknown");
        return new(entry, work, work, history, FromReasons(issueReasons));
    }

    /// <summary>Evaluates the evidence for a point's Issue state before combining it with target coverage.</summary>
    public static bool IsIssueStateKnown(DateTime evaluationUtc, DateTime? latestRetainedSubmissionUtc,
        DateTime? verifiedPointCreatedAtUtc, DateTime annualCutoffUtc) =>
        latestRetainedSubmissionUtc <= evaluationUtc
        // Archive defers a point-month if any source was submitted after the annual cutoff.
        || (verifiedPointCreatedAtUtc > annualCutoffUtc && verifiedPointCreatedAtUtc <= evaluationUtc);

    /// <summary>Evaluates each retained date's shifts. A slot needs its own Issue evidence to be fully supported.</summary>
    public static IReadOnlyList<OverviewSlotAvailability> EvaluateRange(DateTime nowUtc, RetentionSettings settings,
        IReadOnlyList<TargetRevision> revisions, DetailCoverage coverage, IReadOnlySet<ShiftSlot>? issueKnownSlots = null)
    {
        var ordered = OverviewTime.OrderRevisions(revisions, nowUtc);
        var gaps = MergeIntervals(coverage.Gaps.Concat(OverviewTimeline.StructuralGaps(revisions)));
        var unreadable = MergeIntervals(coverage.UnreadableTargetIntervals);
        var firstDate = DateOnly.FromDateTime(ThaiTime.FromUtc(settings.RecordCutoffUtc(nowUtc)));
        var lastDate = DateOnly.FromDateTime(ThaiTime.FromUtc(nowUtc));
        var slots = new List<OverviewSlotAvailability>();
        var shifts = new[] { Shift.Day, Shift.Night };
        var firstRevision = 0;
        var afterLastRevision = 0;
        var firstGap = 0;
        var firstUnreadable = 0;
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        foreach (var shift in shifts)
        {
            var slot = new ShiftSlot(date, shift);
            var from = FirstPossibleOpen(slot);
            var to = Earlier(nowUtc, LastPossibleClose(slot));
            while (firstRevision + 1 < ordered.Length && ordered[firstRevision + 1].EffectiveAt <= from)
                firstRevision++;
            while (afterLastRevision < ordered.Length && ordered[afterLastRevision].EffectiveAt <= to)
                afterLastRevision++;

            // Only the predecessor and this slot's policy changes can affect its evaluation.
            var relevant = ordered[firstRevision..afterLastRevision];
            var moment = OverviewTime.EvaluateOrdered(slot, nowUtc, relevant);
            var timeline = OverviewTimeline.DescribeCoverage(slot, moment, relevant);
            var availability = Calculate(slot, moment, timeline, coverage with
            {
                ReadAtUtc = nowUtc,
                IssueStateKnown = issueKnownSlots?.Contains(slot) == true,
                Gaps = RelevantIntervals(gaps, ref firstGap, ShiftCalendar.StartUtc(slot), to),
                UnreadableTargetIntervals = RelevantIntervals(unreadable, ref firstUnreadable, from, to),
            });
            slots.Add(new(slot, availability));
        }
        return slots;
    }

    private static ArraySegment<(DateTime From, DateTime To)> RelevantIntervals(
        (DateTime From, DateTime To)[] intervals, ref int first, DateTime from, DateTime to)
    {
        while (first < intervals.Length && intervals[first].To <= from) first++;
        var afterLast = first;
        while (afterLast < intervals.Length && intervals[afterLast].From <= to) afterLast++;
        return new(intervals, first, afterLast - first);
    }

    private static DateTime? EarliestAttendance(DateTime start, DateTime evaluation, IReadOnlyList<TargetRevision> history)
    {
        // A rule can only accept an event between its own effective instant and the next policy change.
        for (var i = 0; i < history.Count; i++)
        {
            var revision = history[i];
            var opens = start.AddMinutes(-revision.Image.Policy.OpensMinutesBeforeShift);
            if (opens < revision.EffectiveAt) opens = revision.EffectiveAt;
            if (opens <= evaluation && (i == history.Count - 1 || opens < history[i + 1].EffectiveAt))
                return opens;
        }
        return null;
    }

    private static Availability FromReasons(List<string> reasons) =>
        reasons.Count == 0 ? new("complete", []) : new("partial", reasons.ToArray());

    private static DateTime FirstPossibleOpen(ShiftSlot slot) =>
        ShiftCalendar.StartUtc(slot).AddMinutes(-AttendanceSettings.MaxReceivingOffsetMinutes);

    private static DateTime LastPossibleClose(ShiftSlot slot) =>
        ShiftCalendar.EndUtc(slot).AddMinutes(AttendanceSettings.MaxReceivingOffsetMinutes);

    private static DateTime Earlier(DateTime first, DateTime second) => first <= second ? first : second;

    private static bool Intersects(IEnumerable<(DateTime From, DateTime To)> gaps, DateTime from, DateTime to) =>
        gaps.Any(gap => gap.From < gap.To && gap.From <= to && gap.To > from);

    private static (DateTime From, DateTime To)[] MergeIntervals(IEnumerable<(DateTime From, DateTime To)> intervals)
    {
        var merged = new List<(DateTime From, DateTime To)>();
        foreach (var interval in intervals.Where(gap => gap.From < gap.To).OrderBy(gap => gap.From))
        {
            if (merged.Count == 0 || merged[^1].To < interval.From)
                merged.Add(interval);
            else if (merged[^1].To < interval.To)
                merged[^1] = (merged[^1].From, interval.To);
        }
        return merged.ToArray();
    }
}
