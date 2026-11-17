using System.Text.Json;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

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

/// <summary>
/// Spec §11: one server-issued command identity as the screen receives it. <see cref="CommandId"/> is what the envelope
/// submits, <see cref="Token"/> is the signed value it carries, and <see cref="ExpiresAt"/> is when it can no longer start
/// a command (a retained result can still be looked up afterwards).
/// </summary>
public sealed record ScheduleCommandTokenDto(Guid CommandId, string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Spec §11 and §3: one authorised week as a screen reads it. Every revision is the stored integer as a decimal string and
/// every date is an ISO date, so the contract has one number format and one date format; a person row holds the revisions
/// its edits must send back, and each day holds the provenance of the value it resolved.
/// <para>
/// <see cref="Rows"/> carries only the dates this actor may see — a Supervisor whose dated period starts or ends mid-week
/// gets fewer than seven days and must never read that as "no plan" (spec §3).
/// </para>
/// </summary>
public sealed record ScheduleWeekDto(
    DateOnly WeekStart,
    string Revision,
    DateTimeOffset ReadAt,
    string Availability,
    IReadOnlyList<SchedulePersonDto> Rows);

/// <summary>
/// Spec §10: one person's week. <see cref="WeeklyRevision"/> is the week-header version (0 when nobody has chosen),
/// <see cref="RegularBaselineRevision"/> and <see cref="ReliefBaselineRevision"/> are the head version of each repeating
/// week — the same keys and values the command contract names as <c>week:</c> and <c>baseline:</c>.
/// </summary>
public sealed record SchedulePersonDto(
    int UserId,
    string DisplayName,
    string CleanerKind,
    int? HomeBuildingId,
    bool HasWeeklyChoice,
    string WeeklyRevision,
    string RegularBaselineRevision,
    string ReliefBaselineRevision,
    IReadOnlyList<ScheduleDayDto> Days);

/// <summary>
/// Spec §5, §10 and §11: one resolved day with its provenance per field — a dated choice, the applicable version or nothing
/// at all — plus the availability word and the conflicts someone has to correct. <see cref="IsUnset"/> is not a working day.
/// <para>
/// <see cref="Shift"/> is the shift that day's slot belongs to, resolved by the store exactly as the command handlers resolve
/// it (a dated profile period, else the spec §14 compatibility projection, else nothing). It is per day because a person's
/// shift can change inside a week, and it exists so a client can name the row it read in a dated/leave payload instead of
/// guessing a shift the server will refuse (spec §11: a write identifies the row from what it read).
/// </para>
/// </summary>
public sealed record ScheduleDayDto(
    DateOnly Date,
    Shift? Shift,
    ScheduleValueDto Area,
    ScheduleValueDto BreakGroup,
    bool IsDayOff,
    bool IsLeave,
    bool IsUnset,
    string Availability,
    IReadOnlyList<ScheduleProblemDto> Problems);

/// <summary>One resolved value: what it is, which row supplied it, and that row's revision (0 when nothing supplied it).</summary>
public sealed record ScheduleValueDto(int? Value, int? SourceId, string SourceKind, string Revision);

/// <summary>One conflict or gap a screen has to show rather than resolve by itself.</summary>
public sealed record ScheduleProblemDto(string Code, string Message, DateOnly Date);

/// <summary>
/// Spec §11: the projection the store resolved, mapped to the wire contract. Nothing is re-derived here — the endpoint hands
/// on exactly the values, provenance, availability words and problems the one resolver produced.
/// </summary>
public static class ScheduleDtoMapper
{
    public static ScheduleWeekDto ToDto(this ScheduleProjection projection) => new(
        projection.WeekStart,
        projection.Revision,
        projection.ReadAt,
        projection.Availability,
        projection.Rows.Select(row => new SchedulePersonDto(
            row.UserId,
            row.DisplayName,
            row.CleanerKind,
            row.HomeBuildingId,
            row.HasWeeklyChoice,
            row.WeeklyRevision,
            row.RegularBaselineRevision,
            row.ReliefBaselineRevision,
            row.Days.Select(ToDto).ToList())).ToList());

    private static ScheduleDayDto ToDto(ScheduleDay day) => new(
        day.Date,
        day.Shift,
        ToDto(day.Area),
        ToDto(day.BreakGroup),
        day.IsDayOff,
        day.IsLeave,
        day.IsUnset,
        day.Availability,
        day.Problems.Select(problem => new ScheduleProblemDto(problem.Code, problem.Message, problem.Date)).ToList());

    private static ScheduleValueDto ToDto<T>(ResolvedValue<T> value) =>
        new(Value: value.Value is int number ? number : null,
            SourceId: value.SourceId,
            SourceKind: value.SourceKind,
            Revision: value.Revision);
}
