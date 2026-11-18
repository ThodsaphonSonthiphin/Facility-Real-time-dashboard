using System.Text.Json;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §11: one command kind, resolved from DI by <see cref="Kind"/>. Task 6 supplies the real kinds in
/// <c>ScheduleCommandHandlers</c>; Task 5 owns only the engine that calls this seam, so a test can prove the transaction,
/// the audit row and the rollback without inventing business rules that Task 6 owns.
/// <para>
/// A handler never saves and never commits: <see cref="ScheduleCommandService"/> owns the one transaction and the one
/// SaveChanges, which is what makes "any row failure rolls the whole command back" a property of the engine instead of a
/// promise each kind has to keep. A handler that threw would also roll the command back, but it must answer with a
/// <see cref="RowProblem"/> for anything a person can fix, because a refusal is not a server error.
/// </para>
/// <para>
/// Revision coupling (spec §11, controller ruling 5): every writer that changes a choice must move the revision of the row
/// that owns that choice — in particular a writer that changes a weekly day-off choice must bump
/// <see cref="WeeklyDayOff.Revision"/>, because a weekly day-off digest moves only when the week header's revision moves.
/// A handler that writes a choice row without bumping its header is wrong even though the choice row itself was stored.
/// </para>
/// </summary>
public interface IScheduleCommandHandler
{
    /// <summary>The single command kind this handler owns; the service resolves handlers by this name, ordinal.</summary>
    string Kind { get; }

    /// <summary>
    /// The stable anchors this command must hold before it validates anything (spec §11: lock the anchor, not the target
    /// row that may not exist yet). Planning may read the database, because it runs inside the command's transaction, and
    /// the engine locks whatever is returned in one global sorted order so two commands can never wait on each other.
    /// </summary>
    Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default);

    /// <summary>
    /// Validate every submitted row against the state the locks now protect, then apply it. Answering with any
    /// <see cref="RowProblem"/> means the whole command is refused: the engine rolls back and writes no receipt success.
    /// The returned <see cref="ScheduleCommandApplication.Revisions"/> are the revisions the answer is valid against —
    /// the unchanged ones when the command was refused, the new ones when it applied.
    /// </summary>
    Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default);
}

/// <summary>
/// Spec §11: the command as the engine hands it to a handler — the authenticated actor (never a client-supplied id), the
/// identity the token vouches for, the canonical payload and the revisions the client read.
/// </summary>
public sealed record ScheduleCommandRequest(
    int ActorId,
    Guid CommandId,
    string Kind,
    IReadOnlyDictionary<string, string> ExpectedRevisions,
    JsonElement Payload,
    DateTimeOffset Now);

/// <summary>
/// Spec §11: the stable rows a command locks before validating, in the three shapes the stored model has. Empty
/// collections are meaningful: a command that touches no slot locks no slot.
/// </summary>
public sealed record ScheduleCommandLockSet(
    IReadOnlyList<ScheduleSlotKey> Slots,
    IReadOnlyList<int> BaselineHeads,
    IReadOnlyList<ScheduleScopeKey> Scopes,
    IReadOnlyList<ScheduleScopedDate>? DatedScopes = null)
{
    /// <summary>A command that needs no anchor at all; it still runs in the transaction, it just has nothing to exclude.</summary>
    public static ScheduleCommandLockSet None { get; } = new([], [], []);
}

/// <summary>One person's slot, the identity <see cref="ScheduleAnchor"/> is unique on (person, shift date, shift).</summary>
public sealed record ScheduleSlotKey(int PersonId, DateOnly ShiftDate, Shift Shift);

/// <summary>One building and shift, the identity <see cref="ScheduleScopeAnchor"/> is unique on.</summary>
public sealed record ScheduleScopeKey(int BuildingId, Shift Shift);

/// <summary>Associates a command's building/shift with the date it affects, independently of the shared scope lock.</summary>
public sealed record ScheduleScopedDate(ScheduleScopeKey Scope, DateOnly Date);

/// <summary>
/// Spec §11: what a handler reports after validating every row. Non-empty <see cref="Problems"/> means "refuse the whole
/// command" — there is no partial success and no automatic merge.
/// </summary>
public sealed record ScheduleCommandApplication(
    IReadOnlyDictionary<string, string> Revisions, IReadOnlyList<RowProblem> Problems);
