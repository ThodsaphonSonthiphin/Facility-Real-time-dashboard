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
/// <see cref="IsUnset"/> says the day's choice is not confirmed yet, and <see cref="Availability"/> is the state the day
/// is shown in ("available", "day-off", "leave", "unset" or "unavailable" before history coverage).
/// </summary>
public sealed record ScheduleDay(DateOnly Date, ResolvedValue<int?> Area,
    ResolvedValue<int?> BreakGroup, bool IsDayOff, bool IsLeave, bool IsUnset,
    string Availability, IReadOnlyList<ScheduleProblem> Problems);

/// <summary>Spec §10: one person's resolved week, with the roster facts the management screen shows beside it.</summary>
public sealed record SchedulePersonWeek(int UserId, string DisplayName, string CleanerKind,
    int? HomeBuildingId, IReadOnlyList<ScheduleDay> Days);

/// <summary>
/// Spec §10 and §11: the authorised projection of one week. <see cref="Revision"/> is an opaque token derived from every
/// row the week was built from, so a client can tell a changed week from an unchanged one; <see cref="ReadAt"/> is the
/// last successful read's server time.
/// </summary>
public sealed record ScheduleProjection(DateOnly WeekStart, IReadOnlyList<SchedulePersonWeek> Rows,
    string Revision, DateTimeOffset ReadAt, string Availability);

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
