using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// ADR facility-0183: a shift is dated by the day it started, so a night shift still running after midnight keeps
/// its Sunday date.
/// </summary>
public sealed record ScheduleSlot(DateOnly ShiftDate, Shift Shift);

/// <summary>
/// ADRs facility-0173 and facility-0165: one dated cell of a person's week, unset when no choice exists yet, with
/// its revision for stale-write rejection.
/// </summary>
public sealed record ScheduleCell(int UserId, DateOnly Date, int? AreaId, int? BreakGroup,
    bool IsDayOff, bool IsLeave, bool IsUnset, string Revision);

/// <summary>
/// ADRs facility-0140 and facility-0142: one Supervisor's authority over a building and shift from a date until
/// cleared.
/// </summary>
public sealed record ScopePeriod(int SupervisorId, int BuildingId, Shift Shift, DateOnly From, DateOnly? Until);

/// <summary>
/// Spec §5 and §10: one field of a resolved day with the record it came from. <see cref="SourceId"/> and
/// <see cref="Revision"/> are the stored identity and revision the value was read from (both ids are the existing
/// integer identities, the revision is the stored <c>long</c> as a decimal string), so a client can tell an explicit
/// choice from a default and can reject a stale write.
/// </summary>
public sealed record ResolvedValue<T>(T Value, int? SourceId, string SourceKind, string Revision);

/// <summary>
/// ADR facility-0189: one weekday cell of one baseline version — the applicable version is chosen by its own
/// half-open range, so a later version never rewrites the days the earlier one covered. <see cref="ShiftDate"/> is
/// absent because a baseline repeats every week; the dated slot of a day comes from the profile period instead.
/// </summary>
public sealed record BaselineFact(int Id, DateOnly From, DateOnly? Until, DayOfWeek? Weekday,
    int? AreaId, int? BreakGroup, string Revision, bool IsDayOff = false);

/// <summary>
/// ADRs facility-0134, 0145, 0157 and 0189: one live dated override of a person's slot. An Area override carries
/// <see cref="AreaId"/> and a first-break override carries <see cref="BreakGroup"/>; the row's own date and shift keep
/// the resolver a pure function of the day it is asked about.
/// </summary>
public sealed record DatedFact(int Id, DateOnly ShiftDate, Shift Shift, int? AreaId, int? BreakGroup, string Revision);

/// <summary>
/// ADRs facility-0202 and 0214: one leave interval of a slot. A cancelled row is kept as history and is not an absence
/// state; the interval decides whether the whole shift is covered or only part of it.
/// </summary>
public sealed record LeaveFact(int Id, DateOnly ShiftDate, Shift Shift, DateTimeOffset Start, DateTimeOffset End,
    bool Cancelled, string Revision);

/// <summary>
/// ADRs facility-0203, 0204 and 0213: one transfer instruction of a slot with its single confirmation, if any. A
/// transfer is a plan, never a second ShiftIn, so it does not rewrite the day's Area.
/// </summary>
public sealed record TransferFact(int Id, DateOnly ShiftDate, Shift Shift, int FromAreaId, int ToAreaId,
    DateTimeOffset EffectiveAt, DateTimeOffset? ConfirmedAt, bool Cancelled, string Revision);

/// <summary>
/// Spec §5 and §11: something the stored records disagree about, reported for a person to fix instead of being resolved
/// silently. Machine-readable <see cref="Code"/>, a message for the screen, and the date it belongs to.
/// </summary>
public sealed record ScheduleProblem(string Code, string Message, DateOnly Date);

/// <summary>
/// Spec §10 and A27: one resolved day. <see cref="Area"/> and <see cref="BreakGroup"/> each carry their own source;
/// <see cref="IsDayOff"/> and <see cref="IsLeave"/> are absence states that keep the underlying eligible Area visible,
/// <see cref="IsUnset"/> says the day has no confirmed state of any kind (so it is exactly the "unset"
/// <see cref="Availability"/>, never a working day and never a day off), and <see cref="Availability"/> is the state the
/// day is shown in ("available", "day-off", "leave", "unset" or "unavailable" before history coverage).
/// <para>
/// <see cref="Shift"/> is the shift this date's slot belongs to, resolved the same way the write contract resolves it — the
/// dated profile period in force that day, else the spec §14 compatibility projection when the person has no dated row at
/// all — and null when the person has no shift on that date at all. It is a named extension record (spec §11: a write must
/// identify the row from what it read), not a change to what the other members mean, and it is per day because a person's
/// shift can change inside one week.
/// </para>
/// </summary>
public sealed record ScheduleDay(DateOnly Date, ResolvedValue<int?> Area,
    ResolvedValue<int?> BreakGroup, bool IsDayOff, bool IsLeave, bool IsUnset,
    string Availability, IReadOnlyList<ScheduleProblem> Problems, Shift? Shift = null, ScheduleEditFacts? Edit = null);

/// <summary>Raw facts for a management editor, independent of the winning values of the resolver.</summary>
public sealed record ScheduleEditFacts(
    bool CanChange, bool WeeklyOff, int? BaselineAreaId, int? BaselineBreakGroup, bool BaselineDayOff,
    string AreaRevision, string BreakRevision, ScheduleLeaveEdit? Leave,
    IReadOnlyList<ScheduleAreaOption> Areas, IReadOnlyList<ScheduleCorrection> Corrections);

public sealed record ScheduleLeaveEdit(string Revision, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record ScheduleAreaOption(int Id, string Label);
public sealed record ScheduleCorrection(
    string Field, DateTimeOffset At, string Actor, string Reason, int? Before, int? After);

/// <summary>
/// Spec §10 and ADR facility-0190: one person's resolved week, with the roster facts the management screen shows beside
/// it. <see cref="HasWeeklyChoice"/> is the week-header fact — false when no <c>WeeklyDayOff</c> row exists for this week
/// at all, which is "nobody has been asked" — and is kept away from <see cref="ScheduleDay.IsUnset"/> because a saved
/// week is one fact about the row while a day's confirmation is a fact about the day.
/// <see cref="Days"/> holds the dates this reader may see, in order: seven for an Admin or the person themselves, and only
/// the covered dates for a Supervisor whose dated period starts or ends mid-week (spec §3).
/// <para>
/// The revisions are the week header and the shared per-person baseline timeline anchor, added by Task 6 as an optional extension and never changing
/// what <see cref="Days"/> means: a screen sends them back as its expected revisions so a stale edit is refused instead of
/// silently overwriting a newer plan (spec §11). They are the stored versions as decimal strings, and 0 when the row does
/// not exist yet — the same values <c>ScheduleRevisionKeys</c> names in the API.
/// </para>
/// </summary>
public sealed record SchedulePersonWeek(int UserId, string DisplayName, string CleanerKind,
    int? HomeBuildingId, bool HasWeeklyChoice, IReadOnlyList<ScheduleDay> Days,
    string WeeklyRevision = "0", string RegularBaselineRevision = "0", string ReliefBaselineRevision = "0",
    bool? WeeklyReplaceAllowed = null);

/// <summary>
/// Spec §10 and §11: the authorised projection of one week. <see cref="Revision"/> is an opaque token derived from every
/// row the week was built from, so a client can tell a changed week from an unchanged one; <see cref="ReadAt"/> is the
/// last successful read's server time.
/// <para>
/// <see cref="Relief"/> is the top-level list of relief assignments (loans) the reader may see in this week, added by Task 7
/// without changing what <see cref="Rows"/> means: a relief row belongs to a destination Area and a person who is not on
/// that building's roster, so it cannot live inside a person's week. Cancelled rows are retained with their state, and each
/// row carries the destination and source names the screen shows — never another team's schedule.
/// </para>
/// </summary>
public sealed record ScheduleProjection(DateOnly WeekStart, IReadOnlyList<SchedulePersonWeek> Rows,
    string Revision, DateTimeOffset ReadAt, string Availability, IReadOnlyList<ReliefAssignmentFact> Relief);

/// <summary>
/// Spec §6 and §10 (ADRs facility-0137–0139, 0144, 0145, 0152, 0155, 0206, 0207 and 0211): one recorded relief assignment
/// — the destination Area, the person who works it, the regular person they replace or nobody for standalone work, and the
/// source building the agreement was about. <see cref="AgreementReported"/> is the destination actor's attestation that the
/// source agreed outside the web; it is never a source signature and never evidence that the server verified a conversation.
/// <see cref="Revision"/> is the stored revision as a decimal string, the value the write contract names for the slot.
/// </summary>
public sealed record ReliefAssignmentFact(int Id, int UserId, string DisplayName, DateOnly ShiftDate, Shift Shift,
    int AreaId, string AreaCode, int DestinationBuildingId, string DestinationBuildingCode, int? BreakGroup,
    int? ReplacedUserId, string? ReplacedDisplayName, int SourceBuildingId, string SourceBuildingCode,
    int? SourceSupervisorId, string? SourceSupervisorName, bool AgreementReported, bool IsCancelled, string Revision);

/// <summary>
/// Spec §6 and A09: what a lookup for a relief of a named source building answers. It is the minimum a destination recorder
/// needs — the stable identity, the home building, the dated shift and whether the person can be planned into the shift —
/// so a search never becomes a way to read another team's schedule. <see cref="Conflicts"/> carries machine codes
/// (<c>shift-mismatch</c>, <c>existing-assignment</c>, <c>existing-duty</c>, <c>on-leave</c>) and
/// <see cref="Eligible"/> is exactly "no conflict at all".
/// </summary>
public sealed record ReliefCandidateFacts(int UserId, string EmployeeId, string DisplayName, int HomeBuildingId,
    string HomeBuildingCode, Shift Shift, bool Eligible, string Status, IReadOnlyList<string> Conflicts);

/// <summary>
/// Spec §10 and §11: everything the pure resolver needs about one person. <see cref="HistoryStartsAt"/>, the baseline
/// versions, the saved week's day-off choice (null when no week was saved at all), the dated overrides, the leave
/// intervals and the transfer evidence are loaded by the store; the resolver never reads a mutable User field.
/// </summary>
public sealed record ScheduleInput(int UserId, DateOnly HistoryStartsAt,
    IReadOnlyList<BaselineFact> Baselines, IReadOnlySet<DateOnly>? DaysOff,
    IReadOnlyList<DatedFact> Exceptions, IReadOnlyList<LeaveFact> Leaves,
    IReadOnlyList<TransferFact> Transfers)
{
    /// <summary>A person with no stored schedule at all: nothing is known, so every day before it is unavailable.</summary>
    public static ScheduleInput Empty(DateOnly historyStartsAt) =>
        new(0, historyStartsAt, [], null, [], [], []);
}
