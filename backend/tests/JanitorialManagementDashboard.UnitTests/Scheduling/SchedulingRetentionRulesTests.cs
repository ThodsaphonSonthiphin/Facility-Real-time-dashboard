using JanitorialManagementDashboard.Application.Scheduling;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// Spec §12 and A27: a retained-detail run keeps the configuration that still interprets a retained date, and a version is
/// not obsolete merely because it is old. The predicate is only one input to that selection — the caller also keeps the
/// latest predecessor of the boundary — which is why the boundary cases below are pinned.
/// </summary>
public class SchedulingRetentionRulesTests
{
    private static readonly DateOnly Earliest = new(2026, 11, 17);

    [Fact]
    public void Active_baseline_is_not_obsolete_merely_because_it_is_old()
    {
        Assert.True(SchedulingRetentionRules.KeepBaselineAnchor(
            new DateOnly(2024, 1, 1), until: null, Earliest));
    }

    [Fact]
    public void A_closed_version_that_still_covers_the_retained_boundary_is_kept()
    {
        Assert.True(SchedulingRetentionRules.KeepBaselineAnchor(
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1), Earliest));
    }

    [Fact]
    public void A_version_that_ends_exactly_at_the_boundary_does_not_cover_it()
    {
        // Effective ranges are half-open, so "until = boundary" means the boundary belongs to the next version.
        Assert.False(SchedulingRetentionRules.KeepBaselineAnchor(
            new DateOnly(2025, 1, 1), Earliest, Earliest));
    }

    [Fact]
    public void A_version_starting_on_the_boundary_is_kept()
    {
        Assert.True(SchedulingRetentionRules.KeepBaselineAnchor(Earliest, new DateOnly(2027, 1, 1), Earliest));
    }

    [Fact]
    public void A_future_version_is_kept()
    {
        Assert.True(SchedulingRetentionRules.KeepBaselineAnchor(
            Earliest.AddDays(1), new DateOnly(2028, 1, 1), Earliest));
    }

    [Fact]
    public void A_version_that_ended_before_the_boundary_is_the_only_prunable_shape()
    {
        Assert.False(SchedulingRetentionRules.KeepBaselineAnchor(
            new DateOnly(2024, 1, 1), new DateOnly(2025, 6, 1), Earliest));
    }
}
