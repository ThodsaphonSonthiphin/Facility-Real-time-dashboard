using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Api.DTOs;

/// <summary>
/// Spec §8: a scan, with the slot lifecycle marker and the state revision of the page that sent it so a stale or
/// incompatible page is refused before anything is written. Every new member is optional, so an old page that sends only
/// the four original fields keeps working exactly as it did.
/// <para>
/// <see cref="ConfirmGaps"/> carries the intermediate actions the Cleaner explicitly skips to reach the one being recorded —
/// skip markers, never guessed times (<c>ส่ง skipped markers ไม่ใช่ guessed times</c>) — and has to equal the choice's
/// required gaps exactly.
/// </para>
/// </summary>
public record RecordAttendanceRequest(
    string QrToken,
    AttendanceEvent? EventType,
    double? Latitude,
    double? Longitude,
    double? AccuracyM,
    string? LifecycleVersion = null,
    string? StateRevision = null,
    IReadOnlyList<AttendanceEvent>? ConfirmGaps = null);

/// <summary>
/// One recorded attendance action. <see cref="Source"/> is Scan, AdminAdd or AdminEdit (facility-0051) or SupervisorAdd;
/// <see cref="WithinRadius"/> false is a flag (facility-0035); <see cref="CreatedAt"/> is the row's own recorded time.
/// </summary>
public record AttendanceEntryDto(
    AttendanceEvent EventType,
    DateTime OccurredAt,
    AttendanceSource Source,
    bool? WithinRadius,
    DateTime? CreatedAt = null);

public enum AttendanceOutcome
{
    Recorded,

    /// <summary>
    /// The event already exists and the first time is kept.
    /// See docs/adr/facility-0026-shift-check-in-two-shifts-strict-presence.md.
    /// </summary>
    AlreadyRecorded,

    /// <summary>facility-0031: a different event within RepeatIgnoreMinutes of the latest one is not counted.</summary>
    TooSoon,
}

/// <summary>
/// One action of the slot's lifecycle and what it is right now (spec §8): "recorded" with its actual time and source,
/// "skipped" for a stored skip marker, or — for an action with no event — "missing", "not-applicable" or "pending".
/// <see cref="ExpectedAt"/> is the plan's own expectation (ADR facility-0148), shown beside the actual time and never used
/// to reject one (ADR facility-0149).
/// </summary>
public record AttendanceActionDto(
    AttendanceEvent EventType,
    string State,
    DateTime? OccurredAt,
    AttendanceSource? Source,
    DateTime? ExpectedAt);

/// <summary>Spec §8: one stored gap row — a skip marker or its later resolution — with no invented time of its own.</summary>
public record AttendanceGapDto(AttendanceEvent EventType, string Status, string? Reason, DateTime? RecordedAt);

/// <summary>Spec §8: one action the Cleaner may record next, with the intermediate gaps reaching it steps over.</summary>
public record AttendanceChoiceDto(AttendanceEvent Action, IReadOnlyList<AttendanceEvent> RequiredGaps);

/// <summary>
/// ShiftDate and Shift are null, and the lists empty, outside every attendance window of the Cleaner's shift.
/// <para>
/// Task 9 extends the read additively (spec §8 and §11): <see cref="LifecycleVersion"/> is the slot's contract marker
/// ("Legacy4" or "Numbered6"), <see cref="StateRevision"/> the token a write sends back, <see cref="Actions"/> the ordered
/// lifecycle with each action's state and expected instant, <see cref="Gaps"/> the stored skip markers and their
/// resolutions, and <see cref="NextActions"/> what may be recorded next. <see cref="Events"/> and
/// <see cref="NextEvents"/> keep exactly their old meaning so an older page still works.
/// </para>
/// <para>
/// <see cref="DutyAreaId"/>, <see cref="DutyBreakGroup"/> and <see cref="DutySource"/> are the slot's effective duty
/// (spec §7): the Area the Cleaner is eligible in even on a recorded day off or leave, with <see cref="IsDayOff"/> and
/// <see cref="IsLeave"/> as the conflict markers shown beside it.
/// </para>
/// </summary>
public record AttendanceStateDto(
    DateOnly? ShiftDate,
    Shift? Shift,
    IReadOnlyList<AttendanceEntryDto> Events,
    IReadOnlyList<AttendanceEvent> NextEvents,
    AttendanceOutcome? Outcome,
    string? LifecycleVersion = null,
    string? StateRevision = null,
    IReadOnlyList<AttendanceActionDto>? Actions = null,
    IReadOnlyList<AttendanceGapDto>? Gaps = null,
    IReadOnlyList<AttendanceChoiceDto>? NextActions = null,
    int? DutyAreaId = null,
    int? DutyBreakGroup = null,
    string? DutySource = null,
    bool IsDayOff = false,
    bool IsLeave = false);

/// <summary>
/// A refused write (spec §8, §11). <see cref="Code"/> is the machine word a page branches on — "refresh-required",
/// "action-not-allowed" or "gaps-not-confirmed" — and <see cref="NextEvents"/> keeps the old list of actions that are
/// allowed now, so a page written before these words existed still shows what it showed.
/// </summary>
public record AttendanceRefusedDto(
    string Message,
    IReadOnlyList<AttendanceEvent> NextEvents,
    string? Code = null,
    IReadOnlyList<AttendanceEvent>? RequiredGaps = null);

/// <summary>Spec §11: a stale expected revision, answered with the state the caller has to re-read before writing.</summary>
public record AttendanceStateConflictDto(string Message, string Code, AttendanceStateDto State);

/// <summary>
/// Spec §8 and §11: a scoped missing-time entry. <see cref="Time"/> is Thai wall-clock "HH:mm" inside the slot's receiving
/// window, <see cref="Reason"/> is required and at most the existing 500 characters, and <see cref="ExpectedRevision"/> is
/// the state revision the actor read — a stale one is refused with the current state instead of writing over it.
/// </summary>
public record AddMissingAttendanceRequest(
    int UserId,
    DateOnly? ShiftDate,
    Shift? Shift,
    AttendanceEvent? EventType,
    string? Time,
    string? Reason,
    string? ExpectedRevision);

/// <summary>facility-0041, 0048: tells the phone why the scan was refused and where the scanned sign is.</summary>
public record BlockedScanResponse(string Message, string AreaCode, string AreaName, BlockReason Reason, string BuildingCode);
