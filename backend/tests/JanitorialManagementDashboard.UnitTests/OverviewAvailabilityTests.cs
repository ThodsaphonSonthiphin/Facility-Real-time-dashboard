using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests;

public class OverviewAvailabilityTests
{
    private static readonly ShiftSlot Slot = new(new(2026, 11, 16), Shift.Day);
    private static readonly DateTime Start = ShiftCalendar.StartUtc(Slot);
    private static readonly DateTime Baseline = Start.AddDays(-1);
    private static readonly DateTime ReadAt = Start.AddHours(4);
    private static readonly DetailCoverage Retained = new(Start.AddYears(-1), new(2025, 12, 1), Baseline, true, []);

    [Fact]
    public void Mid_shift_baseline_cannot_certify_prior_rounds()
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Day);
        var start = ShiftCalendar.StartUtc(slot);
        var moment = new OverviewMoment("running", start.AddHours(4), start.AddHours(13), true);
        var target = new TargetTimeline(new(1, new(60, 60), [], [], [], []), 1, [], true, false);
        var coverage = new DetailCoverage(start.AddYears(-1), new(2025, 12, 1),
            start.AddHours(3), true, []);
        var result = OverviewAvailability.Calculate(slot, moment, target, coverage);
        Assert.Equal("complete", result.Entry.State);
        Assert.Empty(result.Entry.Reasons);
        Assert.Equal("complete", result.Work.State);
        Assert.Equal("partial", result.History.State);
        Assert.Contains("targetCoverageMissing", result.History.Reasons);
        Assert.DoesNotContain("receivingPolicyUnknown", result.History.Reasons);
    }

    [Fact]
    public void Pre_shift_capture_after_first_possible_open_completes_entry_and_history()
    {
        var preShift = Start.AddMinutes(-30);
        var revisions = new[] { Revision(1, preShift) };
        var coverage = Retained with { TargetCaptureFromUtc = preShift };

        var result = Calculate(revisions, coverage);

        Assert.All(Sections(result), section =>
        {
            Assert.Equal("complete", section.State);
            Assert.Empty(section.Reasons);
        });
    }

    [Fact]
    public void Unknown_closing_policy_after_shift_end_marks_entry_and_history_partial()
    {
        var end = ShiftCalendar.EndUtc(Slot);
        var postShiftStart = end.AddMinutes(10);
        var readAt = end.AddMinutes(20);
        var revisions = new[] { Revision(1, postShiftStart) };
        var coverage = Retained with { TargetCaptureFromUtc = postShiftStart, ReadAtUtc = readAt };

        var result = Calculate(revisions, coverage, readAt);

        Assert.Equal("partial", result.Entry.State);
        Assert.Contains("receivingPolicyUnknown", result.Entry.Reasons);
        Assert.Equal("partial", result.History.State);
        Assert.Contains("receivingPolicyUnknown", result.History.Reasons);
    }

    [Fact]
    public void Unknown_preshift_opening_policy_falls_back_to_first_possible_open_for_retention_expiry()
    {
        var preShift = Start.AddMinutes(-30);
        var firstPossibleOpen = Start.AddMinutes(-360);
        var revisions = new[] { Revision(1, preShift) };
        var coverage = Retained with
        {
            TargetCaptureFromUtc = preShift,
            AttendanceAfterUtc = firstPossibleOpen,
        };

        var result = Calculate(revisions, coverage);

        Assert.Equal("partial", result.Entry.State);
        Assert.Contains("attendancePartlyExpired", result.Entry.Reasons);
        Assert.DoesNotContain("receivingPolicyUnknown", result.Entry.Reasons);
    }

    [Fact]
    public void Preshift_unreadable_interval_falls_back_to_first_possible_open_without_affecting_history()
    {
        var firstPossibleOpen = Start.AddMinutes(-360);
        var coverage = Retained with
        {
            AttendanceAfterUtc = firstPossibleOpen,
            UnreadableTargetIntervals = [(firstPossibleOpen, Start.AddMinutes(-300))],
        };

        var result = Calculate([Revision(1, Baseline)], coverage);

        Assert.Equal("partial", result.Entry.State);
        Assert.Contains("attendancePartlyExpired", result.Entry.Reasons);
        Assert.DoesNotContain("receivingPolicyUnknown", result.Entry.Reasons);
        Assert.Equal("complete", result.History.State);
        Assert.Empty(result.History.Reasons);
    }

    [Fact]
    public void Recovered_midshift_unreadable_interval_marks_history_partial_without_affecting_entry()
    {
        var coverage = Retained with
        {
            AttendanceAfterUtc = Start.AddMinutes(-360),
            UnreadableTargetIntervals = [(Start.AddHours(1), Start.AddHours(2))],
        };

        var result = Calculate([Revision(1, Baseline)], coverage);

        Assert.Equal("complete", result.Entry.State);
        Assert.Empty(result.Entry.Reasons);
        Assert.Equal("partial", result.History.State);
        Assert.Contains("targetRevisionUnreadable", result.History.Reasons);
        Assert.DoesNotContain("receivingPolicyUnknown", result.History.Reasons);
    }

    [Fact]
    public void Current_unreadable_interval_after_shift_end_marks_closing_policy_unknown()
    {
        var end = ShiftCalendar.EndUtc(Slot);
        var readAt = end.AddHours(2);
        var coverage = Retained with
        {
            ReadAtUtc = readAt,
            UnreadableTargetIntervals = [(end.AddMinutes(-10), end.AddHours(2))],
        };

        var result = Calculate([Revision(1, Baseline, closes: 0)], coverage, readAt);

        Assert.Equal("partial", result.Entry.State);
        Assert.Contains("receivingPolicyUnknown", result.Entry.Reasons);
        Assert.Contains("targetRevisionUnreadable", result.Entry.Reasons);
        Assert.Equal("partial", result.History.State);
        Assert.Contains("receivingPolicyUnknown", result.History.Reasons);
        Assert.Contains("targetRevisionUnreadable", result.History.Reasons);
    }

    [Fact]
    public void Current_unreadable_interval_while_running_keeps_closing_policy_known()
    {
        var coverage = Retained with
        {
            UnreadableTargetIntervals = [(Start.AddHours(3), Start.AddHours(5))],
        };

        var result = Calculate([Revision(1, Baseline)], coverage);

        Assert.Equal("partial", result.Entry.State);
        Assert.Contains("targetRevisionUnreadable", result.Entry.Reasons);
        Assert.DoesNotContain("receivingPolicyUnknown", result.Entry.Reasons);
    }

    [Fact]
    public void No_events_with_complete_evidence_permits_a_true_zero()
    {
        var result = Calculate([Revision(1, Baseline)], Retained);
        Assert.All(Sections(result), section =>
        {
            Assert.Equal("complete", section.State);
            Assert.Empty(section.Reasons);
        });
    }

    [Fact]
    public void A_slot_before_the_baseline_with_retained_detail_is_partial()
    {
        var result = Calculate([Revision(1, ReadAt.AddHours(1))],
            Retained with { TargetCaptureFromUtc = ReadAt.AddHours(1), IssueStateKnown = false });
        Assert.All(Sections(result), section =>
        {
            Assert.Equal("partial", section.State);
            Assert.NotEmpty(section.Reasons);
        });
    }

    [Fact]
    public void Attendance_at_the_cutoff_can_be_purged_even_within_a_retained_work_month()
    {
        var opening = Start.AddHours(-1);
        var history = new[] { Revision(1, Baseline) };

        var result = Calculate(history, Retained with { AttendanceAfterUtc = opening });
        Assert.Equal("partial", result.Entry.State);
        Assert.Equal("complete", result.Work.State);
        Assert.Equal("complete", result.Inspection.State);
        Assert.Equal("complete", Calculate(history, Retained with { AttendanceAfterUtc = opening.AddTicks(-1) }).Entry.State);
    }

    [Theory]
    [InlineData(15, "partial")]
    [InlineData(360, "unavailable")]
    public void Unreadable_policy_uses_the_latest_possible_close_for_attendance_expiry(int cutoffMinutes, string expected)
    {
        var end = ShiftCalendar.EndUtc(Slot);
        var readAt = end.AddYears(1).AddMinutes(cutoffMinutes);
        var coverage = DetailCoverage.FromRetention(readAt, new(), Baseline, false, [])
            with { UnreadableTargetIntervals = [(end.AddMinutes(-10), end.AddHours(2))] };
        var revisions = new[] { Revision(1, Baseline, closes: 0) };
        var moment = OverviewTime.Evaluate(Slot, readAt, revisions);
        Assert.Equal(end, moment.EvaluationUtc);

        var result = OverviewAvailability.Calculate(Slot, moment, OverviewTimeline.Build(Slot, moment, revisions), coverage);

        Assert.Equal(expected, result.Entry.State);
    }

    [Fact]
    public void The_archived_work_month_does_not_make_retained_entry_unavailable()
    {
        var result = Calculate([Revision(1, Baseline)], Retained with { WorkFromShiftDate = Slot.ShiftDate.AddDays(1) });
        Assert.Equal("complete", result.Entry.State);
        Assert.Equal("unavailable", result.Work.State);
        Assert.Equal("unavailable", result.Inspection.State);
        Assert.Equal("unavailable", result.History.State);
        Assert.Equal("complete", result.Issue.State);
    }

    [Fact]
    public void Future_shift_returns_not_started_for_every_section()
    {
        var result = Calculate([Revision(1, Baseline)], Retained, Start.AddMinutes(-1));
        Assert.All(Sections(result), section => Assert.Equal("notStarted", section.State));
    }

    [Fact]
    public void A_structural_gap_in_a_selected_slot_affects_history_after_certainty_resumes()
    {
        var result = Calculate([Revision(1, Baseline), Revision(2, Start.AddHours(2), gapFrom: Start.AddHours(1))], Retained);
        Assert.Equal("complete", result.Entry.State);
        Assert.Equal("complete", result.Work.State);
        Assert.Equal("complete", result.Issue.State);
        Assert.Equal("partial", result.History.State);
    }

    [Fact]
    public void Earliest_opening_uses_the_policy_that_was_in_force_when_attendance_was_accepted()
    {
        // The older policy allowed entry at -3h. The newer one narrows the window to -1h.
        var result = Calculate([Revision(1, Baseline, opens: 180), Revision(2, Start.AddHours(-2))],
            Retained with { AttendanceAfterUtc = Start.AddHours(-2) });
        Assert.Equal("partial", result.Entry.State);
    }

    [Fact]
    public void A_widened_window_cannot_accept_events_before_its_policy_takes_effect()
    {
        var result = Calculate([Revision(1, Baseline, opens: 0), Revision(2, Start.AddHours(-2), opens: 180)],
            Retained with { AttendanceAfterUtc = Start.AddHours(-2).AddTicks(-1) });
        Assert.Equal("complete", result.Entry.State);
    }

    [Fact]
    public void Unknown_issue_state_is_partial_independently_of_the_rounds()
    {
        var result = Calculate([Revision(1, Baseline)], Retained with { IssueStateKnown = false });
        Assert.Equal("partial", result.Issue.State);
        Assert.Equal("complete", result.History.State);
    }

    [Fact]
    public void A_structural_gap_reported_after_the_read_boundary_is_relevant_only_if_it_overlaps_the_slot()
    {
        var revisions = new[] { Revision(1, Baseline), Revision(2, ReadAt.AddHours(1), gapFrom: ReadAt.AddHours(-1)) };
        var during = Calculate(revisions, Retained);
        Assert.Equal("partial", during.Work.State);
        Assert.Contains("targetCoverageGap", during.Work.Reasons);

        var before = Calculate(revisions, Retained, ReadAt.AddHours(-2));
        Assert.Equal("complete", before.Work.State);
        Assert.Equal("complete", before.History.State);
    }

    [Fact]
    public void The_date_range_evaluates_each_shift_against_its_own_opening_and_gap()
    {
        var settings = new RetentionSettings();
        var now = Start.AddYears(1);
        var cutoffNight = Slot with { Shift = Shift.Night };
        var nextDay = Slot with { ShiftDate = Slot.ShiftDate.AddDays(1) };
        var revisions = new[]
        {
            Revision(1, Baseline),
            // The previous Night can receive until 08:00, one hour into Day.
            Revision(2, ShiftCalendar.StartUtc(nextDay).AddHours(2), gapFrom: ShiftCalendar.StartUtc(nextDay).AddMinutes(90)),
        };
        var coverage = DetailCoverage.FromRetention(now, settings, Baseline, true, []);
        var slots = OverviewAvailability.EvaluateRange(now, settings, revisions, coverage,
            new HashSet<ShiftSlot> { cutoffNight });

        Assert.Equal(Slot, slots[0].Slot);
        Assert.False(slots[0].DetailComplete);
        Assert.Contains("attendancePartlyExpired", slots[0].Sections.Entry.Reasons);
        Assert.Equal(cutoffNight, Assert.Single(slots, slot => slot.FullySupported).Slot);

        var dayWithGap = Assert.Single(slots, slot => slot.Slot == nextDay);
        Assert.False(dayWithGap.DetailComplete);
        Assert.Contains("targetCoverageGap", dayWithGap.Sections.History.Reasons);

        var followingNight = Assert.Single(slots, slot => slot.Slot == nextDay with { Shift = Shift.Night });
        Assert.True(followingNight.DetailComplete);
        Assert.False(followingNight.FullySupported);
        Assert.Contains("issueStateUnknown", followingNight.Sections.Issue.Reasons);

        Assert.Equal(new ShiftSlot(Slot.ShiftDate.AddYears(1), Shift.Night), slots[^1].Slot);
        Assert.Equal("notStarted", slots[^1].Sections.Entry.State);
    }

    [Fact]
    public void Missing_issue_can_only_be_certified_from_retained_or_creation_evidence()
    {
        Assert.False(OverviewAvailability.IsIssueStateKnown(ReadAt, null, null, Retained.AttendanceAfterUtc));
        Assert.False(OverviewAvailability.IsIssueStateKnown(ReadAt, ReadAt.AddTicks(1), null, Retained.AttendanceAfterUtc));
        Assert.False(OverviewAvailability.IsIssueStateKnown(ReadAt, null, Retained.AttendanceAfterUtc, Retained.AttendanceAfterUtc));
        Assert.True(OverviewAvailability.IsIssueStateKnown(ReadAt, ReadAt, null, Retained.AttendanceAfterUtc));
        Assert.True(OverviewAvailability.IsIssueStateKnown(ReadAt, null, Retained.AttendanceAfterUtc.AddTicks(1), Retained.AttendanceAfterUtc));
    }

    private static SectionAvailability Calculate(TargetRevision[] revisions, DetailCoverage coverage, DateTime? now = null)
    {
        var moment = OverviewTime.Evaluate(Slot, now ?? ReadAt, revisions);
        return OverviewAvailability.Calculate(Slot, moment, OverviewTimeline.Build(Slot, moment, revisions), coverage);
    }

    private static TargetRevision Revision(long id, DateTime at, int opens = 60, int closes = 60, DateTime? gapFrom = null) =>
        new(id, at, new(1, new(opens, closes), [], [], [], [], Baseline, gapFrom));

    private static Availability[] Sections(SectionAvailability result) =>
        [result.Entry, result.Work, result.Inspection, result.History, result.Issue];
}
