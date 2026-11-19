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
        Assert.Equal("complete", result.Work.State);
        Assert.Equal("partial", result.History.State);
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
        var coverage = DetailCoverage.FromRetention(now, settings, Baseline, false, []);
        var slots = OverviewAvailability.FullySupportedSlots(now, settings, revisions, coverage);

        Assert.Equal(cutoffNight, slots[0]);
        Assert.DoesNotContain(nextDay, slots);
        Assert.Contains(nextDay with { Shift = Shift.Night }, slots);
        Assert.Equal(Slot.ShiftDate.AddYears(1), slots[^1].ShiftDate);
        Assert.Equal(Shift.Day, slots[^1].Shift);
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

    private static TargetRevision Revision(long id, DateTime at, int opens = 60, DateTime? gapFrom = null) =>
        new(id, at, new(1, new(opens, 60), [], [], [], [], Baseline, gapFrom));

    private static Availability[] Sections(SectionAvailability result) =>
        [result.Entry, result.Work, result.Inspection, result.History, result.Issue];
}
