using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Api.DTOs;

/// <summary>One attendance event. Source AdminAdd/AdminEdit is the "แก้โดย Admin" badge (facility-0051); WithinRadius false is a flag (facility-0035). AreaId is where the fact was recorded, which a transferred person makes different from the Area of their other facts.</summary>
public record BoardEventDto(long Id, AttendanceEvent EventType, DateTime OccurredAt, AttendanceSource Source, bool? WithinRadius, short? DistanceM, int AreaId = 0);

/// <summary>A Cleaner assigned to cover this Area for the shift (facility-0041), with their own Area and their Shift-In.</summary>
public record BoardCoverDto(int Id, int UserId, string EmployeeId, string DisplayName, string? OwnAreaCode, DateTime AssignedAt, DateTime? ShiftInAt);

/// <summary>
/// Spec §13 and ADR facility-0216 (Task 11): one Area's actual entry evidence in the slot, with its provenance. Kind is
/// "shift-in" or "transfer-arrival"; TransferId names the instruction a confirmed arrival belongs to and is null for an
/// initial Shift-In.
/// </summary>
public record BoardEntryEvidenceDto(string Kind, DateTime RecordedAt, int? TransferId);

/// <summary>
/// Spec §13 and ADR facility-0216 (Task 11): who is currently responsible for the Area, kept apart from the historical entry
/// evidence. State is "instructed" (the transfer has taken effect but the arrival is not confirmed) or "confirmed";
/// ConfirmedAt is the actual arrival, never the instruction's plan time, and LastConfirmedAreaId is where the person was
/// last confirmed before a pending move.
/// </summary>
public record BoardResponsibilityDto(
    int UserId,
    string EmployeeId,
    string DisplayName,
    string State,
    int? LastConfirmedAreaId,
    DateTime? ConfirmedAt);

public record BoardCleanerAttendanceDto(AssignedCleanerDto Cleaner, IReadOnlyList<BoardEventDto> Events);

/// <summary>
/// Cleaner is the Area's primary displayed Cleaner for the shift (null when none is responsible); Events are theirs. Entry is the
/// spec §13 category — "entered" from actual evidence, "covered" when assigned without entry, or "unassigned" — and
/// EntryEvidence is its provenance. AdditionalCleaners carry the other current people and their own correction facts
/// when an arrival shares the Area. Transfer responsibilities stay separate from actual Entry.
/// </summary>
public record BoardRowDto(
    int AreaId,
    string AreaCode,
    string AreaName,
    string BuildingCode,
    AssignedCleanerDto? Cleaner,
    AreaAttendanceState State,
    IReadOnlyList<BoardEventDto> Events,
    IReadOnlyList<BoardCoverDto> Covers,
    string Entry,
    IReadOnlyList<BoardEntryEvidenceDto> EntryEvidence,
    BoardResponsibilityDto? Responsibility = null,
    IReadOnlyList<BoardCleanerAttendanceDto>? AdditionalCleaners = null,
    IReadOnlyList<BoardResponsibilityDto>? Responsibilities = null);

/// <summary>Required = Areas that work this shift; Present = Working, OnBreak or Done. Required = Present + Empty + Covered.</summary>
public record BoardSummaryDto(int Required, int Present, int Empty, int Covered);

public record AttendanceBoardDto(DateOnly ShiftDate, Shift Shift, bool ShiftEnded, BoardSummaryDto Summary, IReadOnlyList<BoardRowDto> Rows);

/// <summary>SignalR "AttendanceRecorded" to Admins (facility-0027 rule 5): the page reloads that shift's board.</summary>
public record AttendanceChangedDto(int AreaId, DateOnly ShiftDate, Shift Shift);

/// <summary>facility-0051. Time is Thai wall-clock "HH:mm" inside the shift's attendance window (facility-0069); Reason is required.</summary>
public record CorrectAttendanceRequest(int UserId, DateOnly? ShiftDate, Shift? Shift, AttendanceEvent? EventType, string? Time, string? Reason);
