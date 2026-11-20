namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// ADRs facility-0203, 0204, 0212 and 0213 (spec §9): the invariants of a midshift Area transfer a Supervisor instructs and
/// the transferred Cleaner confirms once at the destination sign. They are pure — no clock, no database, no caller state —
/// so every path enforces the same rule instead of trusting a client boolean, and a screen cannot argue a confirmed arrival
/// away.
/// <para>
/// The helpers are deliberately the whole rule set: an arrival is <em>not</em> an attendance action, so nothing about the
/// break cycle may move when it is confirmed (<see cref="BreakStateAfterArrival"/> is the identity), and a confirmation is
/// retained evidence that may never be deleted or cancelled — a further move is a new forward instruction instead
/// (<see cref="CanCancel"/>).
/// </para>
/// </summary>
public static class TransferRules
{
    /// <summary>
    /// ADR facility-0213: only an instruction nobody has arrived under may be cancelled. A confirmed arrival is history the
    /// schedule screen must not erase; the next change is a new forward record.
    /// </summary>
    public static bool CanCancel(bool hasConfirmation) => !hasConfirmation;

    /// <summary>
    /// ADR facility-0212: arriving at the destination Area is not a Break-In, a Break-Out or a second Shift-In, so the break
    /// state the person was in stays exactly what it was — "working", "break1" or "break2" alike. An in-progress cycle keeps
    /// its plan and its progress, and no past break action is fabricated.
    /// </summary>
    public static string BreakStateAfterArrival(string before) => before;
}
