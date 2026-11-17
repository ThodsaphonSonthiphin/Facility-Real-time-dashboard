using System.Text.Json;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §11: one submitted schedule command. <see cref="Token"/> is the server-issued identity this command belongs to
/// and <see cref="CommandId"/> must be that identity's id — the client never chooses it. <see cref="ExpectedRevisions"/>
/// is the revision map the client read (every revision is the stored integer as a decimal string, so the contract has no
/// second number format); <see cref="Payload"/> is the operation's own content and is the only part whose JSON object
/// order is not part of its meaning.
/// </summary>
public sealed record ScheduleCommandEnvelope(
    string Token,
    Guid CommandId,
    IReadOnlyDictionary<string, string> ExpectedRevisions,
    string Kind,
    JsonElement Payload);

/// <summary>
/// Spec §11 and A18: what a command did, or why it refused. <see cref="Revisions"/> carries the authorised revisions the
/// answer is valid against, so a refused client keeps its input and compares against current values instead of guessing;
/// a refusal never names another team's rows and never reports a partial success.
/// </summary>
public sealed record CommandResult(
    Guid CommandId,
    string Outcome,
    IReadOnlyDictionary<string, string> Revisions,
    IReadOnlyList<RowProblem> Problems);

/// <summary>Spec §11: one row a command refused, with a machine code the screen can branch on and a message for a person.</summary>
public sealed record RowProblem(string RowId, string Code, string Message);

/// <summary>
/// Spec §11: the outcomes a schedule command can report. Only <see cref="Committed"/> means the stored facts changed;
/// everything else leaves the database exactly as it was, so a client can never treat a refusal as a completed write.
/// </summary>
public static class ScheduleCommandOutcome
{
    /// <summary>The command applied, and its receipt holds this result for every later repeat of the same identity.</summary>
    public const string Committed = "committed";

    /// <summary>Validation refused at least one row: nothing was written and nothing was merged.</summary>
    public const string Rejected = "rejected";

    /// <summary>Same identity, different content. Not a retry, and never applied; the client needs a new command.</summary>
    public const string PayloadMismatch = "command-payload-mismatch";

    /// <summary>The identity was issued more than 24 hours ago, so it can never start a command again (A19, A27).</summary>
    public const string Expired = "command-expired";

    /// <summary>The token is not this server's, is bound to another actor, or its identity is not the submitted one.</summary>
    public const string NotAuthorised = "command-not-authorised";

    /// <summary>No handler is registered for the submitted kind; an unsupported operation is not a stored fact.</summary>
    public const string UnknownKind = "command-unknown-kind";
}

/// <summary>
/// Spec §11: the machine codes a row problem carries. A handler adds them to
/// <see cref="ScheduleCommandApplication.Problems"/>, and the engine answers with the whole command rolled back.
/// </summary>
public static class ScheduleCommandErrorCode
{
    /// <summary>The row moved since the client read it: the answer names the current revision and writes nothing.</summary>
    public const string StaleRevision = "stale-revision";

    /// <summary>The submitted row is not storable or not allowed; the whole command rolls back, never one row of it.</summary>
    public const string RowInvalid = "row-invalid";
}
