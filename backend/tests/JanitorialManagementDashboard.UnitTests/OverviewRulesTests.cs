using JanitorialManagementDashboard.Application.Overview;

namespace JanitorialManagementDashboard.UnitTests;

public class OverviewRulesTests
{
    [Fact]
    public void Four_of_five_submitted_and_inspected_is_not_whole_area_completion()
    {
        var points = Enumerable.Range(1, 5).Select(i => new OverviewPoint(
            i, true, i < 5, i < 5, false, false, false, false,
            i < 5 ? (long?)i : null, i < 5 ? (long?)i : null)).ToArray();
        var work = OverviewRules.Work(points);
        Assert.Equal(5, work.DuePoints);
        Assert.Equal(4, work.SubmittedPoints);
        Assert.False(work.Submitted);
        Assert.False(work.Inspected);
        Assert.False(work.PendingInspection);
        Assert.False(work.NoSubmission);
        Assert.True(work.Incomplete);
    }

    [Fact]
    public void Rework_is_inspected_until_a_new_submission_arrives()
    {
        var rework = new OverviewPoint(1, true, true, true, true, false, false, false, 10, 20);
        Assert.True(OverviewRules.Work([rework]).Inspected);
        var resubmitted = rework with { IsInspected = false, NeedsRework = false,
            SubmissionId = 11, InspectionId = null };
        var work = OverviewRules.Work([resubmitted]);
        Assert.False(work.Inspected);
        Assert.True(work.PendingInspection);
        Assert.True(work.Submitted);
    }

    [Fact]
    public void Entry_category_prefers_evidence_then_assignment()
    {
        IReadOnlySet<int> entered = new HashSet<int> { 1, 2 };
        IReadOnlySet<int> assigned = new HashSet<int> { 1, 3 };
        Assert.Equal("entered", OverviewRules.Entry(1, entered, assigned));
        Assert.Equal("entered", OverviewRules.Entry(2, entered, assigned));
        Assert.Equal("covered", OverviewRules.Entry(3, entered, assigned));
        Assert.Equal("unassigned", OverviewRules.Entry(4, entered, assigned));
    }

    [Fact]
    public void An_area_with_no_due_point_is_never_vacuously_complete()
    {
        var work = OverviewRules.Work([]);
        Assert.Equal(0, work.DuePoints);
        Assert.False(work.Submitted);
        Assert.False(work.Inspected);
        Assert.False(work.Incomplete);
        Assert.False(work.PendingInspection);
        Assert.False(work.NoSubmission);
        Assert.False(work.Rework);
        Assert.False(work.Overdue);
    }
}
