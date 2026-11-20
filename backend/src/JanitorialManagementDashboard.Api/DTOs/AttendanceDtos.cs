using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Api.DTOs;

/// <summary>A scan includes the current state revision and any breaks the Cleaner explicitly confirms skipping.</summary>
public record RecordAttendanceRequest(
    string QrToken,
    AttendanceEvent? EventType,
    double? Latitude,
    double? Longitude,
    double? AccuracyM,
    string? StateRevision = null,
    IReadOnlyList<AttendanceEvent>? ConfirmGaps = null,
    bool ConfirmPendingTransfer = false);

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
/// Spec §9: the slot's midshift transfer as the attendance read reports it, so a screen shows the instruction, the arrival
/// evidence and the revision it must send back — and offers "ยืนยันเข้าพื้นที่ใหม่" only when the server says the transfer
/// is the person's own and still pending. <see cref="State"/> is "instructed", "confirmed" or "cancelled";
/// <see cref="ConfirmedAt"/> is the actual server time of the arrival and null until somebody arrived under it, because the
/// plan's <see cref="EffectiveAt"/> is never an arrival time.
/// </summary>
public record AttendanceTransferDto(
    int Id,
    int FromAreaId,
    int ToAreaId,
    DateTime EffectiveAt,
    string State,
    DateTime? ConfirmedAt,
    string Revision);

/// <summary>Six-action attendance state. Shift and revision are unset outside the attendance window.</summary>
public record AttendanceStateDto(
    DateOnly? ShiftDate,
    Shift? Shift,
    IReadOnlyList<AttendanceEntryDto> Events,
    AttendanceOutcome? Outcome,
    string? StateRevision = null,
    IReadOnlyList<AttendanceActionDto>? Actions = null,
    IReadOnlyList<AttendanceGapDto>? Gaps = null,
    IReadOnlyList<AttendanceChoiceDto>? NextActions = null,
    int? DutyAreaId = null,
    int? DutyBreakGroup = null,
    string? DutySource = null,
    bool IsDayOff = false,
    bool IsLeave = false,
    AttendanceTransferDto? Transfer = null,
    IReadOnlyList<AttendanceTransferDto>? TransferHistory = null,
    IReadOnlyList<AttendanceEvent>? FillableActions = null,
    bool? CanCancelTransfer = null,
    bool? DepartureLeaveMismatch = null);

/// <summary>A refused write, with the breaks that need confirmation when applicable.</summary>
public record AttendanceRefusedDto(
    string Message,
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
