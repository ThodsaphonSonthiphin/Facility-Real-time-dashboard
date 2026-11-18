namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// Spec §12 and acceptance A27: the scheduling personal detail is pruned at the configured detail boundary, but a
/// baseline version is not obsolete merely because it is old. A version survives while it is open, while it still covers a
/// retained date, and while it starts after the retained boundary; only a version whose whole effective range lies before
/// the retained detail is prunable. <c>from</c> stays part of the signature because the caller also selects the latest
/// predecessor of the retained window — the one version needed to interpret the oldest retained date — and that row is not
/// chosen by this predicate alone.
/// </summary>
public static class SchedulingRetentionRules
{
    /// <summary>
    /// True when a baseline effective [<paramref name="from"/>, <paramref name="until"/>) must survive a run whose oldest
    /// retained date is <paramref name="earliestRetained"/>. An open version and a future version are always kept; a closed
    /// version is kept only while the retained boundary still falls inside it (the range is half-open, so a version ending
    /// at the boundary does not cover it).
    /// </summary>
    public static bool KeepBaselineAnchor(DateOnly from, DateOnly? until, DateOnly earliestRetained) =>
        (from <= earliestRetained && (until is null || until.Value > earliestRetained)) || from > earliestRetained;
}
