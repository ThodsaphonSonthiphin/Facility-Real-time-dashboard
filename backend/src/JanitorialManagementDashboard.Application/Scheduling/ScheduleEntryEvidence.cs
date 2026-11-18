using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// Spec §13 and ADR facility-0216 (Task 11, closing overview D7): one legitimate actual entry fact of a slot. Entry is
/// evidence that somebody physically arrived in an Area, never an arrangement to be there: the initial Shift-In scanned at
/// the Area, or a midshift transfer arrival the person confirmed at the destination sign. A supervisor's instruction, a
/// relief loan, a skip marker or a pending replacement is never entry evidence.
/// <para>
/// The Area is the one the fact was recorded at, so the same person can produce evidence for A and B in one slot — A stays
/// entered after a transfer, and B is entered only once the arrival is real. The set of Areas is what the overview counts;
/// current responsibility is a separate projection and is never inferred from this record.
/// </para>
/// <para>
/// <see cref="TransferId"/> names the instruction whose arrival this is, and is <c>int</c> because the stored
/// <c>area_transfers</c> identity is the Task 2 int key (Task 10's ruling); the plan's <c>Guid</c> sketch would need a
/// migration for no product gain.
/// </para>
/// </summary>
public sealed record AreaEntryEvidence(
    int AreaId,
    DateOnly ShiftDate,
    Shift Shift,
    DateTimeOffset RecordedAt,
    string Kind,
    int? TransferId);

/// <summary>The only two kinds of fact that count as entry (spec §13, ADR facility-0216).</summary>
public static class EntryEvidenceKinds
{
    /// <summary>The initial Shift-In scanned at the Area. One per person per slot.</summary>
    public const string ShiftIn = "shift-in";

    /// <summary>A destination-sign arrival the transferred person confirmed (Task 10).</summary>
    public const string TransferArrival = "transfer-arrival";
}

/// <summary>
/// Spec §13 and ADR facility-0216: the one shared answer to "which Areas did somebody enter in this slot", and the entry
/// category of one Area. It is pure — no clock and no database — so the overview, the attendance board and any later reader
/// cannot grow a second Shift-In-only rule.
/// <para>
/// The three categories are the overview's accepted ones: <see cref="Entered"/> (there is evidence), <see cref="Covered"/>
/// (assigned, but no entry evidence yet — a live cover or a person currently responsible) and <see cref="Unassigned"/>.
/// They are assignments to one Area each and together keep the target denominator.
/// </para>
/// </summary>
public static class ScheduleEntryEvidence
{
    /// <summary>Somebody's legitimate entry evidence exists for the Area in this slot.</summary>
    public const string Entered = "entered";

    /// <summary>Assigned without entry evidence yet (assigned-without-entry in spec §13).</summary>
    public const string Covered = "covered";

    /// <summary>Neither entered nor assigned.</summary>
    public const string Unassigned = "unassigned";

    /// <summary>
    /// The distinct Areas entered in <paramref name="slot"/>. Filters by the recorded slot (never by a wall-clock guess),
    /// keeps only the two legitimate kinds, and counts an Area once however many facts name it.
    /// </summary>
    public static IReadOnlySet<int> EnteredAreas(ScheduleSlot slot, IEnumerable<AreaEntryEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return evidence
            .Where(row => row.ShiftDate == slot.ShiftDate && row.Shift == slot.Shift)
            .Where(row => row.Kind is EntryEvidenceKinds.ShiftIn or EntryEvidenceKinds.TransferArrival)
            .Select(row => row.AreaId)
            .ToHashSet();
    }

    /// <summary>The entry category of one Area: evidence wins over an assignment, and an assignment over nothing.</summary>
    public static string EntryOf(bool hasEntryEvidence, bool isAssigned) =>
        hasEntryEvidence ? Entered
        : isAssigned ? Covered
        : Unassigned;
}
