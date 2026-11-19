using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests;

public class OverviewProjectionTests
{
    private static readonly ShiftSlot Day = new(new(2026, 11, 16), Shift.Day);
    private static readonly DateTime Baseline = ShiftCalendar.StartUtc(Day).AddDays(-1);
    private static readonly Availability Complete = new("complete", []);
    private static readonly SectionAvailability Sections =
        new(Complete, Complete, Complete, Complete, Complete);

    private static DateTime At(int hour, int minute = 0) => ShiftCalendar.ToUtc(Day, new(hour, minute));

    private static OverviewMoment Running(int hour = 12, int minute = 0) =>
        new("running", At(hour, minute), At(20), true);

    private static OverviewMoment Receiving(int hour, int minute = 0) =>
        new("receiving", At(hour, minute), At(20), true);

    private static OverviewMoment Closed() => new("closed", At(20), At(20), true);

    private static TargetBuilding B(int id, string? code = null, string? name = null) =>
        new(id, code ?? $"B{id}", name ?? $"Building {id}", true, Baseline);

    private static TargetArea A(int id, int building = 1, string? code = null,
        string? name = null, ShiftPattern pattern = ShiftPattern.DayAndNight) =>
        new(id, building, code ?? $"A{id}", name ?? $"Area {id}", true, pattern, Baseline);

    private static TargetPoint P(int id, int area = 10, string? code = null, int sort = 1) =>
        new(id, area, code ?? $"P{id}", $"Point {id}", sort, true, Baseline);

    private static TargetWindow W(int id, int point = 100, int start = 8, Shift shift = Shift.Day) =>
        new(id, point, shift, new(start, 0), new(start, 30), Baseline);

    private static TargetImage Image(TargetBuilding[]? buildings = null, TargetArea[]? areas = null,
        TargetPoint[]? points = null, TargetWindow[]? windows = null) =>
        new(1, new(60, 60), buildings ?? [B(1)], areas ?? [A(10)],
            points ?? [P(100)], windows ?? []);

    private static TargetTimeline Timeline(TargetImage? image, params RoundObligation[] rounds) =>
        new(image, image is null ? null : 1, rounds, image is not null, true);

    private static RoundObligation Round(int pointId, int origin, int startHour,
        int areaId = 10, int buildingId = 1, string areaCode = "A10", string areaName = "Hall",
        string buildingCode = "B1", string buildingName = "West",
        DateTime? activeFrom = null, DateTime? finalized = null, string? reason = null, long revision = 1) =>
        new(pointId, areaId, buildingId, areaCode, areaName, buildingCode, buildingName, origin,
            new(startHour, 0), new(startHour, 30), activeFrom ?? Baseline, finalized, revision, reason);

    private static OverviewSubmission Scan(long id, int pointId, int? origin, int startHour,
        DateTime submitted, Placement placement = Placement.OnTime,
        CleaningStatus status = CleaningStatus.Normal, DateOnly? shiftDate = null,
        Shift? shift = null, int? existingWindow = int.MinValue) =>
        new(id, pointId, shiftDate ?? Day.ShiftDate, shift ?? Day.Shift, origin,
            existingWindow == int.MinValue ? origin : existingWindow,
            new(startHour, 0), new(startHour, 30), placement, status, submitted, 5, null, null);

    private static OverviewInspection Inspect(long id, long submissionId, InspectionResult result,
        DateTime at, int supervisor = 1) =>
        new(id, submissionId, result, at, supervisor, null);

    private static OverviewFacts Facts(IReadOnlyList<OverviewSubmission>? scans = null,
        IReadOnlyList<OverviewInspection>? inspections = null, IReadOnlySet<int>? assigned = null,
        IReadOnlyList<AreaEntryEvidence>? evidence = null,
        IReadOnlyList<OverviewAttendance>? attendance = null,
        IReadOnlyList<OverviewCover>? covers = null, bool originsComplete = true) =>
        new(scans ?? [], inspections ?? [], attendance ?? [], covers ?? [], [], evidence ?? [],
            assigned ?? new HashSet<int>(), originsComplete, true);

    private static AreaEntryEvidence EnteredAt(int areaId,
        string kind = EntryEvidenceKinds.ShiftIn, int? transferId = null) =>
        new(areaId, Day.ShiftDate, Day.Shift, new DateTimeOffset(At(7), TimeSpan.Zero), kind, transferId);

    private static OverviewSnapshot Build(TargetTimeline timeline, OverviewFacts facts,
        OverviewMoment? moment = null, int? buildingId = null) =>
        OverviewProjection.Build(Day, moment ?? Running(), timeline, facts, Sections, buildingId);

    [Fact]
    public void Entry_partition_is_90_entered_5_covered_5_unassigned_and_not_entered_is_10()
    {
        var areas = Enumerable.Range(1, 100).Select(i => A(i, i <= 50 ? 1 : 2)).ToArray();
        var image = Image([B(1), B(2)], areas, []);
        var evidence = Enumerable.Range(1, 89).Select(i => EnteredAt(i)).ToList();
        evidence.Add(EnteredAt(90, EntryEvidenceKinds.TransferArrival, 7));
        evidence.Add(EnteredAt(1));
        evidence.Add(EnteredAt(90, EntryEvidenceKinds.TransferArrival, 7));
        var facts = Facts(assigned: new HashSet<int> { 91, 92, 93, 94, 95 }, evidence: evidence,
            attendance:
            [
                new(1, 96, 5, AttendanceEvent.ShiftIn, At(7), false),
                new(2, 10, 5, AttendanceEvent.ShiftOut, At(18), false),
                new(3, 10, 5, AttendanceEvent.BreakOut, At(10), false),
            ],
            covers:
            [
                new(1, 10, 5, At(7), null),
                new(2, 96, 6, At(7), At(8)),
            ]);

        var snapshot = Build(Timeline(image), facts);

        Assert.Equal(100, snapshot.Areas.Count);
        Assert.Equal(90, OverviewProjection.Members(snapshot, OverviewGroupKeys.Entered).Count);
        Assert.Equal(5, OverviewProjection.Members(snapshot, OverviewGroupKeys.Covered).Count);
        Assert.Equal(5, OverviewProjection.Members(snapshot, OverviewGroupKeys.Unassigned).Count);
        Assert.Equal(10, OverviewProjection.Members(snapshot, OverviewGroupKeys.NotEntered).Count);
        Assert.All(snapshot.Areas, area => Assert.Equal(1,
            area.Groups.Count(g => g is OverviewGroupKeys.Entered or OverviewGroupKeys.Covered
                or OverviewGroupKeys.Unassigned)));
        // A confirmed transfer keeps the origin entered and adds the destination.
        Assert.Contains(OverviewProjection.Members(snapshot, OverviewGroupKeys.Entered), a => a.Id is 50 or 90);
        // Attendance (even Shift-In), a live cover and a cancelled instruction never prove entry.
        Assert.Contains(OverviewProjection.Members(snapshot, OverviewGroupKeys.Unassigned), a => a.Id == 96);
    }

    [Fact]
    public void Moved_area_keeps_its_historical_owner_and_is_counted_once_factory_wide()
    {
        var image = Image([B(1, "B1", "West"), B(2, "B2", "East")], [A(10, 2)]);
        var morning = Round(100, 1000, 8, finalized: At(13), reason: "superseded");
        var afternoon = Round(100, 1001, 13, buildingId: 2, buildingCode: "B2", buildingName: "East",
            activeFrom: At(13));
        var timeline = Timeline(image, morning, afternoon);

        var factory = Build(timeline, Facts());
        var area = Assert.Single(factory.Areas);
        Assert.Equal((10, 2, "B2"), (area.Id, area.BuildingId, area.BuildingCode));
        Assert.Equal(1, area.Work.DuePoints);
        Assert.Equal("notDone", Assert.Single(area.History).Outcome);
        Assert.Contains(OverviewGroupKeys.MissedCleaning, area.Groups);
        Assert.Contains(OverviewGroupKeys.Unassigned, area.Groups);
        Assert.Single(OverviewProjection.Members(factory, OverviewGroupKeys.MissedCleaning));
        Assert.Single(OverviewProjection.Members(factory, OverviewGroupKeys.Incomplete));

        var old = Assert.Single(Build(timeline, Facts(), buildingId: 1).Areas);
        Assert.Equal((10, 1, "B1", "West"), (old.Id, old.BuildingId, old.BuildingCode, old.BuildingName));
        Assert.Equal(0, old.Work.DuePoints);
        Assert.Equal("notDone", Assert.Single(old.History).Outcome);
        Assert.Contains(OverviewGroupKeys.MissedCleaning, old.Groups);
        // The historical owner is not a required current structure, so it never inflates staffing or no-round counts.
        Assert.DoesNotContain(OverviewGroupKeys.Unassigned, old.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.NotEntered, old.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.NoRounds, old.Groups);

        var current = Assert.Single(Build(timeline, Facts(), buildingId: 2).Areas);
        Assert.Equal(2, current.BuildingId);
        Assert.Empty(current.History);
        Assert.Equal(1, current.Work.DuePoints);
        Assert.Contains(OverviewGroupKeys.Unassigned, current.Groups);
    }

    [Fact]
    public void Receiving_keeps_the_editable_final_round_out_of_history_but_closed_includes_it()
    {
        var image = Image();
        var open = Round(100, 1000, 8);
        var closed = Round(100, 1000, 8, finalized: At(20), reason: "slotClosed");

        var receiving = Assert.Single(Build(Timeline(image, open), Facts(), Receiving(19, 45)).Areas);
        Assert.Empty(receiving.History);
        Assert.True(receiving.Work.Incomplete);
        Assert.True(receiving.Work.NoSubmission);
        Assert.DoesNotContain(OverviewGroupKeys.MissedCleaning, receiving.Groups);

        var atClose = Assert.Single(Build(Timeline(image, closed), Facts(), Closed()).Areas);
        Assert.Equal("notDone", Assert.Single(atClose.History).Outcome);
        Assert.Equal("slotClosed", atClose.History[0].Round.FinalizedReason);
        Assert.True(atClose.Work.NoSubmission);
        Assert.Contains(OverviewGroupKeys.MissedCleaning, atClose.Groups);
        Assert.Equal(1, atClose.Work.DuePoints);
    }

    [Fact]
    public void Cancelled_round_keeps_its_reason_and_excludes_a_later_submission()
    {
        var image = Image();
        var cancelled = Round(100, 1000, 8, finalized: At(12), reason: "configurationChanged");
        var late = Scan(1, 100, 1000, 8, At(12, 30), Placement.Late);

        var area = Assert.Single(Build(Timeline(image, cancelled), Facts([late]), Running(13)).Areas);

        var result = Assert.Single(area.History);
        Assert.Equal("notDone", result.Outcome);
        Assert.Null(result.SubmissionId);
        Assert.Equal("configurationChanged", result.Round.FinalizedReason);
        Assert.Equal(0, area.Work.DuePoints);
        Assert.Contains(OverviewGroupKeys.MissedCleaning, area.Groups);
    }

    [Fact]
    public void A_later_passing_round_cannot_remove_an_earlier_miss()
    {
        var image = Image();
        var missed = Round(100, 1000, 8, finalized: At(11), reason: "superseded");
        var passing = Round(100, 1001, 11, activeFrom: At(11));
        var scan = Scan(1, 100, 1001, 11, At(11, 15));
        var inspection = Inspect(10, 1, InspectionResult.Passed, At(11, 30));

        var area = Assert.Single(Build(Timeline(image, missed, passing), Facts([scan], [inspection])).Areas);

        Assert.Equal("notDone", Assert.Single(area.History).Outcome);
        Assert.True(area.Work.Inspected);
        Assert.Contains(OverviewGroupKeys.MissedCleaning, area.Groups);
        Assert.Single(OverviewProjection.Members(
            Build(Timeline(image, missed, passing), Facts([scan], [inspection])),
            OverviewGroupKeys.MissedCleaning));
    }

    [Fact]
    public void Incomplete_and_pending_inspection_overlap_without_claiming_a_submission_miss()
    {
        var image = Image(points: [P(100), P(101)], windows: [W(1000, 100), W(1001, 101)]);
        var timeline = Timeline(image, Round(100, 1000, 8), Round(101, 1001, 8));
        var scan = Scan(1, 100, 1000, 8, At(8, 10));

        var area = Assert.Single(Build(timeline, Facts([scan])).Areas);

        Assert.True(area.Work.Incomplete);
        Assert.True(area.Work.PendingInspection);
        Assert.False(area.Work.NoSubmission);
        Assert.False(area.Work.Submitted);
        Assert.False(area.Work.Inspected);
        Assert.Contains(OverviewGroupKeys.Incomplete, area.Groups);
        Assert.Contains(OverviewGroupKeys.PendingInspection, area.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.Submitted, area.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.Inspected, area.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.NoSubmission, area.Groups);
    }

    [Fact]
    public void Four_inspected_of_five_is_incomplete_with_no_pending_inspection()
    {
        var points = Enumerable.Range(0, 5).Select(i => P(100 + i)).ToArray();
        var windows = Enumerable.Range(0, 5).Select(i => W(1000 + i, 100 + i)).ToArray();
        var rounds = Enumerable.Range(0, 5).Select(i => Round(100 + i, 1000 + i, 8)).ToArray();
        var scans = Enumerable.Range(0, 4).Select(i => Scan(i + 1, 100 + i, 1000 + i, 8, At(8, 10))).ToArray();
        var inspections = Enumerable.Range(0, 4)
            .Select(i => Inspect(10 + i, i + 1, InspectionResult.Passed, At(8, 20))).ToArray();

        var area = Assert.Single(Build(Timeline(Image(points: points, windows: windows), rounds),
            Facts(scans, inspections)).Areas);

        Assert.Equal((5, 4, 4), (area.Work.DuePoints, area.Work.SubmittedPoints, area.Work.InspectedPoints));
        Assert.True(area.Work.Incomplete);
        Assert.False(area.Work.PendingInspection);
        Assert.False(area.Work.Inspected);
        Assert.False(area.Work.Submitted);
        Assert.Contains(OverviewGroupKeys.Incomplete, area.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.PendingInspection, area.Groups);
    }

    [Fact]
    public void Five_reworked_points_stay_inspected_until_a_resubmission_resets_the_point()
    {
        var points = Enumerable.Range(0, 5).Select(i => P(100 + i)).ToArray();
        var windows = Enumerable.Range(0, 5).Select(i => W(1000 + i, 100 + i)).ToArray();
        var rounds = Enumerable.Range(0, 5).Select(i => Round(100 + i, 1000 + i, 8)).ToArray();
        var scans = Enumerable.Range(0, 5).Select(i => Scan(i + 1, 100 + i, 1000 + i, 8, At(8, 10))).ToArray();
        var inspections = Enumerable.Range(0, 5)
            .Select(i => Inspect(10 + i, i + 1, InspectionResult.Rework, At(8, 20))).ToArray();
        var timeline = Timeline(Image(points: points, windows: windows), rounds);

        var reworked = Assert.Single(Build(timeline, Facts(scans, inspections)).Areas);
        Assert.True(reworked.Work.Inspected);
        Assert.True(reworked.Work.Rework);
        Assert.Equal(5, reworked.Work.InspectedPoints);

        var resubmission = Scan(100, 100, 1000, 8, At(10));
        var reset = Assert.Single(Build(timeline, Facts([.. scans, resubmission], inspections)).Areas);
        Assert.False(reset.Work.Inspected);
        Assert.True(reset.Work.PendingInspection);
        Assert.True(reset.Work.Submitted);
        Assert.True(reset.Work.Rework);
        Assert.Contains(OverviewGroupKeys.PendingInspection, reset.Groups);
        Assert.Contains(OverviewGroupKeys.Rework, reset.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.Inspected, reset.Groups);
    }

    [Fact]
    public void Off_round_is_ignored_for_completion_but_supplies_the_issue_tag_and_overdue()
    {
        var image = Image();
        var round = Round(100, 1000, 8);
        var offRound = Scan(1, 100, 1000, 8, At(8, 10), Placement.OffRound, CleaningStatus.Issue);

        var early = Assert.Single(Build(Timeline(image, round), Facts([offRound]), Running(8, 20)).Areas);
        Assert.Equal(0, early.Work.SubmittedPoints);
        Assert.True(early.Work.Incomplete);
        Assert.True(early.Work.NoSubmission);
        Assert.True(early.Work.Issue);
        Assert.False(early.Work.Overdue);
        Assert.Contains(OverviewGroupKeys.Issue, early.Groups);

        var overdue = Assert.Single(Build(Timeline(image, round), Facts([offRound]), Running(8, 40)).Areas);
        Assert.True(overdue.Work.Overdue);
        Assert.Contains(OverviewGroupKeys.Overdue, overdue.Groups);
    }

    [Fact]
    public void Equal_times_choose_the_highest_submission_and_inspection_id()
    {
        var image = Image();
        var round = Round(100, 1000, 8);
        var scans = new[]
        {
            Scan(1, 100, 1000, 8, At(8, 10)),
            Scan(2, 100, 1000, 8, At(8, 10)),
        };
        var inspections = new[]
        {
            Inspect(10, 2, InspectionResult.Rework, At(9)),
            Inspect(11, 2, InspectionResult.Passed, At(9)),
        };

        var point = Assert.Single(Assert.Single(Build(Timeline(image, round), Facts(scans, inspections)).Areas).Points);

        Assert.Equal(2L, point.SubmissionId);
        Assert.Equal(11L, point.InspectionId);
        Assert.True(point.IsInspected);
        Assert.False(point.NeedsRework);
    }

    [Fact]
    public void Issue_after_the_evaluation_instant_is_excluded()
    {
        var image = Image();
        var round = Round(100, 1000, 8);
        var normal = Scan(1, 100, 1000, 8, At(8, 10));
        var laterIssue = Scan(2, 100, 1000, 8, At(14), Placement.OffRound, CleaningStatus.Issue);

        var atNoon = Assert.Single(Build(Timeline(image, round), Facts([normal, laterIssue]), Running(12)).Areas);
        Assert.False(atNoon.Work.Issue);
        Assert.DoesNotContain(OverviewGroupKeys.Issue, atNoon.Groups);

        var atThree = Assert.Single(Build(Timeline(image, round), Facts([normal, laterIssue]), Running(15)).Areas);
        Assert.True(atThree.Work.Issue);
        Assert.Contains(OverviewGroupKeys.Issue, atThree.Groups);
    }

    [Fact]
    public void Issue_follows_the_point_card_from_an_earlier_slot()
    {
        var image = Image();
        var round = Round(100, 1000, 8);
        var previous = Scan(1, 100, 900, 10, At(10).AddDays(-1), Placement.OnTime,
            CleaningStatus.Issue, Day.ShiftDate.AddDays(-1));

        var area = Assert.Single(Build(Timeline(image, round), Facts([previous])).Areas);

        Assert.True(area.Work.Issue);
        Assert.True(area.Work.NoSubmission);
        Assert.Contains(OverviewGroupKeys.Issue, area.Groups);
    }

    [Fact]
    public void No_rounds_and_upcoming_are_separate_and_never_vacuously_incomplete()
    {
        var image = Image(
            areas: [A(10), A(11), A(12)],
            points: [P(100, 10), P(101, 11), P(102, 12)],
            windows: [W(1000, 100, 15), W(1001, 101, 8)]);
        var timeline = Timeline(image, Round(101, 1001, 8, areaId: 11, areaCode: "A11"));

        var snapshot = Build(timeline, Facts(), Running(12));
        var upcoming = Assert.Single(OverviewProjection.Members(snapshot, OverviewGroupKeys.Upcoming));
        var noRounds = Assert.Single(OverviewProjection.Members(snapshot, OverviewGroupKeys.NoRounds));
        var due = Assert.Single(OverviewProjection.Members(snapshot, OverviewGroupKeys.Incomplete));

        Assert.Equal(10, upcoming.Id);
        Assert.Equal(12, noRounds.Id);
        Assert.Equal(11, due.Id);
        Assert.True(Assert.Single(upcoming.Points).IsUpcoming);
        Assert.DoesNotContain(upcoming.Points, p => p.IsDue);
        Assert.DoesNotContain(OverviewProjection.Members(snapshot, OverviewGroupKeys.NoSubmission),
            a => a.Id is 10 or 12);
    }

    [Fact]
    public void Missing_origin_attaches_only_when_one_active_interval_matches()
    {
        var image = Image();
        var prior = Round(100, 1000, 8, finalized: At(11), reason: "superseded");
        var current = Round(100, 1001, 8, activeFrom: At(11));
        var timeline = Timeline(image, prior, current);
        var unlinked = Scan(1, 100, null, 8, At(9), existingWindow: null);
        var passed = Inspect(10, 1, InspectionResult.Passed, At(9, 30));

        var unique = Assert.Single(Build(timeline, Facts([unlinked], [passed])).Areas);
        Assert.Equal("passed", Assert.Single(unique.History).Outcome);
        Assert.Equal(1L, unique.History[0].SubmissionId);
        Assert.True(unique.Work.Incomplete);

        var ambiguousScan = Scan(2, 100, null, 8, At(11), existingWindow: null);
        var ambiguous = Assert.Single(Build(timeline, Facts([ambiguousScan])).Areas);
        Assert.Equal("notDone", Assert.Single(ambiguous.History).Outcome);
        Assert.Null(ambiguous.History[0].SubmissionId);
        Assert.Null(Assert.Single(ambiguous.Points).SubmissionId);
        Assert.True(ambiguous.Work.Incomplete);
    }

    [Fact]
    public void A_placed_origin_at_the_transition_chooses_its_own_interval()
    {
        var image = Image();
        var prior = Round(100, 1000, 8, finalized: At(11), reason: "superseded");
        var current = Round(100, 1001, 8, activeFrom: At(11));
        var placed = Scan(1, 100, 1000, 8, At(11));
        var inspection = Inspect(10, 1, InspectionResult.Passed, At(11));

        var area = Assert.Single(Build(Timeline(image, prior, current), Facts([placed], [inspection])).Areas);

        Assert.Equal("passed", Assert.Single(area.History).Outcome);
        Assert.Null(Assert.Single(area.Points).SubmissionId);
        Assert.True(area.Work.Incomplete);
    }

    [Fact]
    public void A_recreated_window_never_accepts_the_previous_origins_scan()
    {
        var image = Image();
        var prior = Round(100, 1000, 8, finalized: At(11), reason: "superseded");
        var current = Round(100, 1001, 11, activeFrom: At(11));
        var oldScan = Scan(1, 100, 1000, 8, At(11, 15));

        var area = Assert.Single(Build(Timeline(image, prior, current), Facts([oldScan])).Areas);

        Assert.Null(Assert.Single(area.Points).SubmissionId);
        Assert.True(area.Work.NoSubmission);
        Assert.Equal("notDone", Assert.Single(area.History).Outcome);
    }

    [Fact]
    public void A_late_scan_before_close_counts_and_an_uninspected_correction_stays_pending()
    {
        var image = Image();
        var round = Round(100, 1000, 8);
        var late = Scan(1, 100, 1000, 8, At(19, 45), Placement.Late);

        var open = Assert.Single(Build(Timeline(image, round), Facts([late]), Receiving(19, 50)).Areas);
        Assert.True(open.Work.Submitted);
        Assert.True(open.Work.PendingInspection);
        Assert.False(open.Work.Overdue);

        var correction = Scan(2, 100, 1000, 8, At(19, 55), Placement.Late);
        var corrected = Assert.Single(Build(Timeline(image, round), Facts([late, correction]), Receiving(19, 58)).Areas);
        Assert.Equal(2L, Assert.Single(corrected.Points).SubmissionId);
        Assert.True(corrected.Work.PendingInspection);

        var closedRound = Round(100, 1000, 8, finalized: At(20), reason: "slotClosed");
        var closed = Assert.Single(Build(Timeline(image, closedRound), Facts([late, correction]), Closed()).Areas);
        var result = Assert.Single(closed.History);
        Assert.Equal("notInspected", result.Outcome);
        Assert.Equal(2L, result.SubmissionId);
        Assert.Null(result.InspectionId);
        Assert.Contains(OverviewGroupKeys.MissedInspection, closed.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.MissedCleaning, closed.Groups);
    }

    [Fact]
    public void Overdue_starts_after_the_window_end_and_only_without_a_counted_scan()
    {
        var image = Image();
        var round = Round(100, 1000, 8);

        Assert.False(Assert.Single(Build(Timeline(image, round), Facts(), Running(8, 30)).Areas).Work.Overdue);
        var after = Assert.Single(Build(Timeline(image, round), Facts(), Running(8, 31)).Areas);
        Assert.True(after.Work.Overdue);
        Assert.Contains(OverviewGroupKeys.Overdue, after.Groups);

        var late = Scan(1, 100, 1000, 8, At(8, 31), Placement.Late);
        var submitted = Assert.Single(Build(Timeline(image, round), Facts([late]), Running(9)).Areas);
        Assert.False(submitted.Work.Overdue);
    }

    [Fact]
    public void A_scan_of_another_slot_is_never_progress_even_when_its_wall_clock_fits()
    {
        var image = Image();
        var round = Round(100, 1000, 8);
        var otherSlot = Scan(1, 100, 1000, 8, At(8, 10),
            shiftDate: Day.ShiftDate.AddDays(-1), shift: Shift.Night);

        var wrong = Assert.Single(Build(Timeline(image, round), Facts([otherSlot])).Areas);
        Assert.True(wrong.Work.NoSubmission);

        var right = Scan(2, 100, 1000, 8, At(8, 10));
        var correct = Assert.Single(Build(Timeline(image, round), Facts([right])).Areas);
        Assert.True(correct.Work.Submitted);
    }

    [Fact]
    public void History_outcomes_are_exactly_not_done_not_inspected_not_reworked_passed()
    {
        static void Check(string expected, bool missedCleaning, bool missedInspection,
            OverviewSubmission[] scans, OverviewInspection[] inspections)
        {
            var image = Image();
            var round = Round(100, 1000, 8, finalized: At(11));
            var area = Assert.Single(Build(Timeline(image, round), Facts(scans, inspections), Running(12)).Areas);
            Assert.Equal(expected, Assert.Single(area.History).Outcome);
            Assert.Equal(missedCleaning, area.Groups.Contains(OverviewGroupKeys.MissedCleaning));
            Assert.Equal(missedInspection, area.Groups.Contains(OverviewGroupKeys.MissedInspection));
        }

        Check("notDone", true, false, [], []);
        Check("notInspected", false, true,
            [Scan(1, 100, 1000, 8, At(9))], []);
        Check("notReworked", false, false,
            [Scan(1, 100, 1000, 8, At(9))],
            [Inspect(10, 1, InspectionResult.Rework, At(10))]);
        Check("passed", false, false,
            [Scan(1, 100, 1000, 8, At(9))],
            [Inspect(10, 1, InspectionResult.Passed, At(10))]);
    }

    [Fact]
    public void A_historical_area_outside_the_current_image_is_still_resolvable()
    {
        var round = Round(100, 1000, 8, finalized: At(11), reason: "configurationChanged",
            areaCode: "OLD", areaName: "Old Hall", buildingCode: "B9", buildingName: "Old Wing");

        var area = Assert.Single(Build(Timeline(null, round), Facts(), Running(12)).Areas);

        Assert.Equal((10, 1, "OLD", "Old Hall", "B9", "Old Wing"),
            (area.Id, area.BuildingId, area.Code, area.Name, area.BuildingCode, area.BuildingName));
        Assert.Equal("notDone", Assert.Single(area.History).Outcome);
        Assert.Contains(OverviewGroupKeys.MissedCleaning, area.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.Unassigned, area.Groups);
        Assert.DoesNotContain(OverviewGroupKeys.NoRounds, area.Groups);
        Assert.Single(OverviewProjection.Members(
            Build(Timeline(null, round), Facts(), Running(12)), OverviewGroupKeys.MissedCleaning));
    }

    [Fact]
    public void Members_order_by_building_then_area_code_then_id()
    {
        var image = Image([B(1, "B1"), B(2, "B2")],
            [A(12, 2, "A2"), A(11, 1, "A9"), A(10, 1, "A1")], []);

        var members = OverviewProjection.Members(Build(Timeline(image), Facts()), OverviewGroupKeys.Unassigned);

        Assert.Equal(new[] { 10, 11, 12 }, members.Select(a => a.Id));
    }

    [Fact]
    public void The_group_allowlist_is_exactly_the_locked_sixteen_keys()
    {
        Assert.Equal(
            new[]
            {
                "entered", "covered", "unassigned", "notEntered", "submitted", "incomplete",
                "inspected", "pendingInspection", "noSubmission", "rework", "issue", "overdue",
                "upcoming", "noRounds", "missedCleaning", "missedInspection",
            },
            OverviewGroupKeys.All);
        Assert.All(OverviewGroupKeys.All, group => Assert.True(OverviewGroupKeys.IsKnown(group)));
        Assert.False(OverviewGroupKeys.IsKnown("unknown"));
    }
}
