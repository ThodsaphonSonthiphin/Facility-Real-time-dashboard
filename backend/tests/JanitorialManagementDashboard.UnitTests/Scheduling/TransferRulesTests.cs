using JanitorialManagementDashboard.Application.Scheduling;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// ADRs facility-0212 and 0213 (spec §9): the two invariants a midshift transfer must not break, as pure rules a caller cannot
/// talk itself out of. An arrival confirmation is a fact of its own — it is never a Break-In, a Break-Out or a shift
/// boundary — so whatever break state the person was in when they scanned the destination sign is the state they are in
/// afterwards. And a confirmed arrival is evidence that history keeps: only an instruction with no confirmation may be
/// cancelled.
/// </summary>
public class TransferRulesTests
{
    [Theory]
    [InlineData("working")]
    [InlineData("break1")]
    [InlineData("break2")]
    public void Arrival_does_not_fabricate_an_attendance_transition(string before) =>
        Assert.Equal(before, TransferRules.BreakStateAfterArrival(before));

    [Fact]
    public void A_pending_instruction_may_be_cancelled()
    {
        Assert.True(TransferRules.CanCancel(hasConfirmation: false));
    }

    [Fact]
    public void A_confirmed_arrival_is_history_and_may_not_be_cancelled()
    {
        Assert.False(TransferRules.CanCancel(hasConfirmation: true));
    }
}
