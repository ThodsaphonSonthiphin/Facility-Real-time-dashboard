namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// Spec §5 and ADR facility-0211: the answer of <see cref="ReliefAssignmentRules.ValidateGroup"/> — an accepted row with the
/// group it will store, or the machine code a screen branches on. <see cref="Group"/> is null exactly when the result was
/// not accepted, so a caller can never store a group the rule refused.
/// </summary>
public sealed record ReliefGroupResult(bool Accepted, int? Group, string? Code);

/// <summary>
/// Spec §5, §6 and acceptance A08 (ADRs facility-0152, 0189 and 0211): the one rule that decides the first-break group of a
/// relief assignment. A relief replacing a regular Cleaner inherits that person's applicable group — the date's explicit
/// choice when there is one, else their repeating pattern — and the recorder may adjust it for the day. Standalone relief
/// work names nobody to inherit from, so the group cannot be guessed: a working row must carry 1 or 2, chosen explicitly.
/// <para>
/// The rule is pure: it reads no database, no clock and no mutable account field, because the command that validates a
/// submitted row and anything that later explains a stored row must give the same answer from the same two inputs.
/// </para>
/// </summary>
public static class ReliefAssignmentRules
{
    /// <summary>
    /// The machine code for "this row needs a break group before it can be stored as working relief": a standalone
    /// assignment with no explicit group, or a value that is not one of the two groups the shift structure has.
    /// </summary>
    public const string BreakGroupRequired = "break-group-required";

    /// <summary>The groups a first break can belong to. Nothing else is a valid stored value.</summary>
    public static IReadOnlyList<int> Groups { get; } = [1, 2];

    /// <summary>
    /// The group this assignment stores. An explicit choice always wins — including a wrong one, which is refused instead of
    /// being silently replaced — and only when the recorder chose nothing does the replaced person's group apply. With
    /// neither, the result is <see cref="BreakGroupRequired"/>.
    /// </summary>
    public static ReliefGroupResult ValidateGroup(int? replacedGroup, int? explicitGroup)
    {
        var group = explicitGroup ?? replacedGroup;
        return group is 1 or 2 ? new(true, group, null) : new(false, null, BreakGroupRequired);
    }
}
