using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §11 and acceptance A18, A19 and A27: the one engine every schedule command runs through. It decides whether a
/// submitted command may start or must be answered from its retained result, holds the transaction that makes the
/// mutation, its audit rows and its receipt one fact, hands validation to the kind's handler, and notifies only after the
/// commit.
/// <para>
/// The order is the contract, not an implementation detail: the token's identity is checked before anything is read, the
/// retained receipt is looked up <em>before</em> the 24-hour start window is judged (so an uncertain write can still be
/// resolved by an old identity), a genuinely expired identity is refused before any handler runs, and every mutation then
/// happens inside one transaction whose anchors are locked in a single sorted order.
/// </para>
/// <para>
/// A refusal writes nothing at all — not a partial command, not a receipt success, not a merge. That is why a refused
/// command's answer says which revisions are current: the client keeps the input it typed and compares, instead of losing
/// it to a reload.
/// </para>
/// </summary>
public sealed class ScheduleCommandService(
    AppDbContext db,
    ScheduleStore store,
    ScheduleCommandTokenService tokens,
    IEnumerable<IScheduleCommandHandler> handlers,
    IScheduleCommandNotifier notifier,
    TimeProvider clock,
    ILogger<ScheduleCommandService>? logger = null)
{
    /// <summary>An answer that names no revision: a refusal is not entitled to describe the rows it refused to touch.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoRevisions = new Dictionary<string, string>();

    private readonly IReadOnlyDictionary<string, IScheduleCommandHandler> _handlers =
        handlers.ToDictionary(handler => handler.Kind, StringComparer.Ordinal);

    private readonly ILogger _logger = logger ?? NullLogger<ScheduleCommandService>.Instance;

    /// <summary>
    /// Spec §11: apply this command at most once for <paramref name="actorId"/>. A repeated identity with the same payload
    /// returns the retained result and writes nothing a second time; the same identity with a different payload is a
    /// conflict; a stale expected revision, an unreadable row or an unsupported kind leaves the database untouched and
    /// returns the problems with the current revisions.
    /// </summary>
    public async Task<CommandResult> ExecuteAsync(
        int actorId, ScheduleCommandEnvelope command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow();
        var fingerprint = Fingerprint(command);

        // The identity is the token's, never the client's: an envelope naming an identity the server did not sign for this
        // actor is refused before any row is read, so a probe cannot even learn whether that identity exists.
        var verified = tokens.Verify(command.Token, actorId, now);
        var claims = tokens.ReadClaims(command.Token);
        if (claims is null || claims.ActorId != actorId || claims.Id != command.CommandId)
        {
            return Refused(command.CommandId, ScheduleCommandOutcome.NotAuthorised);
        }

        // Spec §11: no account at all reaches a handler. The engine's one generic gate is the same authority rule the
        // status lookup asks — an active Admin, or an active Supervisor whose dated scope covers today's business date, and
        // nothing else — because a Cleaner may never run a management command. Which building and shift a particular row
        // may touch is still the kind's own check (Task 6); this gate only decides who may submit a command at all.
        var authority = await ReadAuthorityAsync(actorId, ct);
        if (authority is null)
        {
            return Refused(command.CommandId, ScheduleCommandOutcome.NotAuthorised);
        }

        // A19: the retained result is resolved before the start window is judged, so an identity that is too old to start
        // a command can still answer for the one it already committed — and only ever with the same payload.
        var retained = await db.ScheduleCommandReceipts.AsNoTracking()
            .FirstOrDefaultAsync(receipt => receipt.CommandId == command.CommandId, ct);
        if (retained is not null)
        {
            return Retained(retained, fingerprint, actorId, authority);
        }

        // A27: past the window and with nothing retained, the identity can never start a mutation again. It is refused
        // here, before a handler is chosen, so a purged receipt cannot become a second application.
        if (!verified.Accepted)
        {
            return Refused(command.CommandId, ScheduleCommandOutcome.Expired);
        }

        if (string.IsNullOrEmpty(command.Kind) || !_handlers.TryGetValue(command.Kind, out var handler))
        {
            return Refused(command.CommandId, ScheduleCommandOutcome.UnknownKind);
        }

        var request = new ScheduleCommandRequest(
            actorId, command.CommandId, command.Kind, command.ExpectedRevisions, command.Payload, now);

        // The identities the handlers' anchors named are what the post-commit invalidation is scoped to; they are read
        // outside the transaction because a failed command must notify nobody at all.
        var plan = ScheduleCommandLockSet.None;

        await using var transaction = await store.BeginCommandTransactionAsync(ct);
        try
        {
            // The stable anchors are locked, in one global sorted order, before any row is validated: a command that has
            // to create a row for a slot nobody has written yet still excludes the command doing the same thing.
            plan = await handler.PlanLocksAsync(request, db, ct);
            await LockAsync(plan, now, ct);

            // The lock may have been granted to this transaction only after another one committed the same identity.
            var raced = await db.ScheduleCommandReceipts.AsNoTracking()
                .FirstOrDefaultAsync(receipt => receipt.CommandId == command.CommandId, ct);
            if (raced is not null)
            {
                await DiscardAsync(transaction, ct);
                return Retained(raced, fingerprint, actorId, authority);
            }

            var receipt = new ScheduleCommandReceipt
            {
                CommandId = command.CommandId,
                ActorId = actorId,
                Kind = command.Kind,
                PayloadFingerprint = fingerprint,
                ExpectedRevisionsJson = JsonSerializer.Serialize(SortedRevisions(command.ExpectedRevisions)),

                Outcome = ScheduleCommandOutcome.Committed,
                ResultJson = "{}",
                CommittedAt = now.UtcDateTime,
            };
            db.ScheduleCommandReceipts.Add(receipt);

            // The handler validates every row and writes, but never saves: the engine's single SaveChanges is what makes a
            // row failure roll the whole command back instead of leaving the rows before it stored.
            var applied = await handler.ValidateAndApplyAsync(request, receipt, db, ct);
            if (applied.Problems.Count > 0)
            {
                await DiscardAsync(transaction, ct);
                return new CommandResult(
                    command.CommandId, ScheduleCommandOutcome.Rejected, applied.Revisions, applied.Problems);
            }

            var result = new CommandResult(
                command.CommandId, ScheduleCommandOutcome.Committed, applied.Revisions, []);
            receipt.ScopeJson = SerializeScope(applied.ReceiptScopes ?? plan.Scopes);
            receipt.ResultJson = JsonSerializer.Serialize(result);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // After the commit, never before: the facts are stored whatever happens to the push (spec §11 and §12).
            await NotifyAsync(result, new ScheduleChangeScope(plan.Slots, plan.Scopes, plan.BaselineHeads, plan.DatedScopes), ct);
            return result;
        }
        catch (DbUpdateException)
        {
            // Only a duplicate command identity can make a command that validated inside its locks lose the save: the
            // UNIQUE index on command_id. If another connection committed this identity, its result is the answer.
            await DiscardAsync(transaction, ct);
            var committed = await db.ScheduleCommandReceipts.AsNoTracking()
                .FirstOrDefaultAsync(receipt => receipt.CommandId == command.CommandId, ct);
            if (committed is not null)
            {
                return Retained(committed, fingerprint, actorId, authority);
            }

            throw;
        }
    }

    /// <summary>
    /// Spec §11 and controller ruling 4: the result of an uncertain write, only to the actor that committed it, and only
    /// while that actor still holds the scope the command applied to. Another actor gets "not found" — no payload, no
    /// enumeration and no timing hint — and so does the actor itself once it no longer holds the authority the command
    /// needed (a building this command touched is not one of today's periods; the account is deactivated) or its scope
    /// cannot be read back.
    /// </summary>
    public async Task<CommandResult?> ReadResultAsync(int actorId, Guid id, CancellationToken ct = default)
    {
        var receipt = await db.ScheduleCommandReceipts.AsNoTracking()
            .FirstOrDefaultAsync(row => row.CommandId == id, ct);
        if (receipt is null || receipt.ActorId != actorId)
        {
            return null;
        }

        // The scope check is the applied scope, not "some authority today": a Supervisor who kept the role but lost this
        // building/shift may not read what it changed there. An unreadable stored scope denies rather than widens.
        var authority = await ReadAuthorityAsync(actorId, ct);
        return authority is not null && HoldsRecordedScope(receipt, authority) ? StoredResult(receipt) : null;
    }

    /// <summary>
    /// Spec §11: the canonical fingerprint of a command — the operation, the expected revisions in ordinal key order and
    /// the payload with every JSON object's keys in ordinal order and every array in its own order, as SHA-256 hex. Object
    /// order is not content, so a client that re-serialises its payload still sends the same command; array order is
    /// content, so a reordered batch of rows is a different command.
    /// </summary>
    public static string Fingerprint(ScheduleCommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var payload = JsonDocument.Parse(CanonicalPayload(command.Payload));
        var canonical = JsonSerializer.Serialize(new
        {
            command.Kind,
            Revisions = SortedRevisions(command.ExpectedRevisions),
            Payload = payload.RootElement,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>The payload with object keys normalised recursively; arrays keep the order the client submitted.</summary>
    private static string CanonicalPayload(JsonElement payload)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonical(writer, payload);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(pair => pair.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    /// <summary>The expected revisions in ordinal key order, so the fingerprint does not depend on dictionary enumeration.</summary>
    private static SortedDictionary<string, string> SortedRevisions(
        IReadOnlyDictionary<string, string>? revisions)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in revisions ?? NoRevisions)
        {
            sorted[pair.Key] = pair.Value;
        }

        return sorted;
    }

    /// <summary>
    /// Locks every anchor the handler planned, in one order that does not depend on the order the plan named them, so two
    /// commands that need overlapping anchors can never wait on each other.
    /// </summary>
    private async Task LockAsync(ScheduleCommandLockSet plan, DateTimeOffset now, CancellationToken ct)
    {
        var ordered = new List<(string Key, Func<Task> Lock)>();
        foreach (var slot in plan.Slots.Distinct())
        {
            ordered.Add((
                $"slot|{slot.PersonId.ToString("D10", CultureInfo.InvariantCulture)}|" +
                $"{slot.ShiftDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}|{slot.Shift}",
                async () => await store.LockSlotAsync(slot.PersonId, slot.ShiftDate, slot.Shift, now.UtcDateTime, ct)));
        }

        foreach (var personId in plan.BaselineHeads.Distinct())
        {
            ordered.Add((
                $"baseline|{personId.ToString("D10", CultureInfo.InvariantCulture)}",
                async () => await store.LockBaselineHeadAsync(personId, now.UtcDateTime, ct, advanceRevision: false)));
        }

        foreach (var scope in plan.Scopes.Distinct())
        {
            ordered.Add((
                $"scope|{scope.BuildingId.ToString("D10", CultureInfo.InvariantCulture)}|{scope.Shift}",
                async () => await store.LockRegularScopeAsync(scope.BuildingId, scope.Shift, now.UtcDateTime, ct)));
        }

        ordered.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        foreach (var step in ordered)
        {
            await step.Lock();
        }
    }

    /// <summary>Rolls the command back and forgets what it staged, so the whole command left no fact behind.</summary>
    private async Task DiscardAsync(IDbContextTransaction transaction, CancellationToken ct)
    {
        await transaction.RollbackAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>
    /// The answer a retained receipt gives, and the three ways it can still refuse: another actor, another payload, or an
    /// actor that no longer holds the scope this command applied to. A retained answer is a status answer, so it asks the
    /// same question <see cref="ReadResultAsync"/> asks.
    /// </summary>
    private static CommandResult Retained(
        ScheduleCommandReceipt receipt, string fingerprint, int actorId, ActorAuthority authority)
    {
        if (receipt.ActorId != actorId || !HoldsRecordedScope(receipt, authority))
        {
            return Refused(receipt.CommandId, ScheduleCommandOutcome.NotAuthorised);
        }

        if (!string.Equals(receipt.PayloadFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return Refused(receipt.CommandId, ScheduleCommandOutcome.PayloadMismatch);
        }

        return StoredResult(receipt);
    }

    /// <summary>
    /// A Supervisor needs the original authority for this receipt. Old transfer receipts lack the origin proof.
    /// </summary>
    private static bool HoldsRecordedScope(ScheduleCommandReceipt receipt, ActorAuthority authority) =>
        ReadScope(receipt.ScopeJson) is { } scope
        && (scope.Count > 0 || authority.Role == UserRole.Admin
            || receipt.Kind is not (ScheduleCommandKinds.InstructTransfer or ScheduleCommandKinds.CancelTransfer))
        && authority.HoldsScope(scope);

    private static CommandResult Refused(Guid commandId, string outcome) =>
        new(commandId, outcome, NoRevisions, []);

    /// <summary>
    /// The committed result as it was stored. A receipt whose stored result cannot be read is still reported as committed
    /// — never as safe to retry — but with no revisions and no problems, which is the honest minimum it can still say.
    /// </summary>
    private static CommandResult StoredResult(ScheduleCommandReceipt receipt)
    {
        try
        {
            if (JsonSerializer.Deserialize<CommandResult>(receipt.ResultJson) is { } stored)
            {
                return stored;
            }
        }
        catch (JsonException)
        {
            // Falls through to the outcome-only answer below.
        }

        return new CommandResult(receipt.CommandId, receipt.Outcome, NoRevisions, []);
    }

    /// <summary>
    /// Spec §11 and §3: the authority the actor holds <em>now</em>, as it stands today rather than as it stood when the
    /// command ran. Admin holds it while the account is active, a Supervisor only while one of its dated periods covers
    /// today's business date, and a Cleaner never holds it. Everything that has to decide "may this actor do a management
    /// thing at all" asks this one method, and whether a recorded building/shift is inside it is
    /// <see cref="ActorAuthority.HoldsScope"/>, which asks <see cref="ScheduleAuthority.CanManageSchedule"/> — the same predicate
    /// every other management path uses.
    /// </summary>
    private async Task<ActorAuthority?> ReadAuthorityAsync(int actorId, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == actorId, ct);
        if (actor is not { IsActive: true })
        {
            return null;
        }

        if (actor.Role == UserRole.Admin)
        {
            return new ActorAuthority(actor.Id, UserRole.Admin, DateOnly.MinValue, []);
        }

        if (actor.Role != UserRole.Supervisor)
        {
            return null;
        }

        var periods = await SupervisorScopes.ReadAsync(db, actor, clock, ct);
        var businessDate = ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime);
        return periods.Any(period => ScheduleAuthority.Covers(period, businessDate))
            ? new ActorAuthority(actor.Id, UserRole.Supervisor, businessDate, periods)
            : null;
    }

    /// <summary>
    /// The building/shift anchors a plan named, as the JSON a receipt retains: distinct, in a stable order that does not
    /// depend on the order the plan listed them, and as names rather than ordinal values so the stored fact does not change
    /// meaning if an enum value is ever renumbered.
    /// </summary>
    private static string SerializeScope(IReadOnlyList<ScheduleScopeKey> scopes) =>
        JsonSerializer.Serialize(scopes
            .Distinct()
            .OrderBy(scope => scope.BuildingId)
            .ThenBy(scope => scope.Shift)
            .Select(scope => new StoredScopeKey(scope.BuildingId, scope.Shift.ToString())));

    /// <summary>
    /// The scope a receipt retained. Empty means "no building/shift anchor", which is a command whose result has no scope
    /// to lose (and a receipt written before the column existed); null means the stored value cannot be read, and a scope
    /// check that cannot be answered never answers yes.
    /// </summary>
    private static IReadOnlyList<ScheduleScopeKey>? ReadScope(string? scopeJson)
    {
        if (string.IsNullOrWhiteSpace(scopeJson))
        {
            return [];
        }

        try
        {
            if (JsonSerializer.Deserialize<List<StoredScopeKey>>(scopeJson) is not { } stored)
            {
                return null;
            }

            var scope = new List<ScheduleScopeKey>(stored.Count);
            foreach (var key in stored)
            {
                if (!Enum.TryParse<Shift>(key.Shift, ignoreCase: false, out var shift))
                {
                    return null;
                }

                scope.Add(new ScheduleScopeKey(key.BuildingId, shift));
            }

            return scope;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>One retained scope key as it is stored: the building id and the shift name.</summary>
    private sealed record StoredScopeKey(int BuildingId, string Shift);

    /// <summary>
    /// The authority an actor holds right now (spec §11). <see cref="HoldsScope"/> answers whether the building/shift keys a
    /// command applied to are all still inside it: an Admin holds every building, and a Supervisor holds only the keys one
    /// of today's dated periods names — through <see cref="ScheduleAuthority.CanManageSchedule"/>, never a second copy of the rule.
    /// </summary>
    private sealed record ActorAuthority(
        int ActorId, UserRole Role, DateOnly BusinessDate, IReadOnlyList<ScopePeriod> Periods)
    {
        public bool HoldsScope(IReadOnlyList<ScheduleScopeKey> scope) =>
            scope.All(key => ScheduleAuthority.CanManageSchedule(
                Role, ActorId, key.BuildingId, BusinessDate, Periods));
    }

    /// <summary>A failed push is logged and swallowed: the facts are already committed, so the command did not fail.</summary>
    private async Task NotifyAsync(CommandResult result, ScheduleChangeScope scope, CancellationToken ct)
    {
        try
        {
            await notifier.NotifyCommittedAsync(result, scope, ct);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Schedule command {CommandId} committed but its notification failed", result.CommandId);
        }
    }
}
