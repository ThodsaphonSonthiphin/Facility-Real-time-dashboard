using JanitorialManagementDashboard.Application.Scheduling;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// Spec §5, §6 and acceptance A08 (ADRs facility-0152 and 0211): the one rule that decides a relief assignment's
/// first-break group. A relief who replaces a regular Cleaner inherits that person's group as the default and may override
/// it for the day; a standalone assignment names no replaced person, so nothing can be guessed and the recorder has to
/// choose 1 or 2 explicitly before a working row exists. The rule is pure — no database, no clock — because the same answer
/// has to be given by the command and by anything that later explains the stored row.
/// </summary>
public class ReliefAssignmentRulesTests
{
    [Fact]
    public void Standalone_relief_work_cannot_guess_a_break_group()
    {
        var result = ReliefAssignmentRules.ValidateGroup(replacedGroup: null, explicitGroup: null);
        Assert.Equal("break-group-required", result.Code);
        Assert.True(ReliefAssignmentRules.ValidateGroup(null, 2).Accepted);
    }

    [Fact]
    public void The_replaced_persons_group_is_inherited_when_nothing_is_chosen()
    {
        var result = ReliefAssignmentRules.ValidateGroup(replacedGroup: 1, explicitGroup: null);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.Group);
        Assert.Null(result.Code);
    }

    [Fact]
    public void An_explicit_group_wins_over_the_replaced_persons_group()
    {
        // ADR facility-0152 with 0189: the person's default is inherited, and the day may be adjusted on its own.
        var result = ReliefAssignmentRules.ValidateGroup(replacedGroup: 1, explicitGroup: 2);

        Assert.True(result.Accepted);
        Assert.Equal(2, result.Group);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(99)]
    public void A_group_outside_one_or_two_is_never_accepted(int explicitGroup)
    {
        var result = ReliefAssignmentRules.ValidateGroup(replacedGroup: 1, explicitGroup: explicitGroup);

        // An unusable choice is refused rather than quietly replaced by the inherited default: the recorder asked for a
        // group the shift structure does not have, so the row is not storable as submitted.
        Assert.False(result.Accepted);
        Assert.Null(result.Group);
        Assert.Equal(ReliefAssignmentRules.BreakGroupRequired, result.Code);
    }

    [Fact]
    public void A_replaced_persons_unusable_group_is_not_inherited()
    {
        // The replaced person's row is corrupt rather than absent, and the relief row must not copy it.
        var result = ReliefAssignmentRules.ValidateGroup(replacedGroup: 7, explicitGroup: null);

        Assert.False(result.Accepted);
        Assert.Equal(ReliefAssignmentRules.BreakGroupRequired, result.Code);
    }
}
