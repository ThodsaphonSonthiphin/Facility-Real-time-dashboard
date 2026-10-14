using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>One attendance event. Source AdminAdd/AdminEdit is the "แก้โดย Admin" badge (facility-0051); WithinRadius false is a flag (facility-0035).</summary>
public record BoardEventDto(long Id, AttendanceEvent EventType, DateTime OccurredAt, AttendanceSource Source, bool? WithinRadius, short? DistanceM);

/// <summary>A Cleaner assigned to cover this Area for the shift (facility-0041), with their own Area and their Shift-In.</summary>
public record BoardCoverDto(int Id, int UserId, string EmployeeId, string DisplayName, string? OwnAreaCode, DateTime AssignedAt, DateTime? ShiftInAt);

/// <summary>Cleaner is the Area's regular Cleaner for the shift (null when none is assigned); Events are theirs.</summary>
public record BoardRowDto(
    int AreaId,
    string AreaCode,
    string AreaName,
    string BuildingCode,
    AssignedCleanerDto? Cleaner,
    AreaAttendanceState State,
    IReadOnlyList<BoardEventDto> Events,
    IReadOnlyList<BoardCoverDto> Covers);

/// <summary>Required = Areas that work this shift; Present = Working, OnBreak or Done. Required = Present + Empty + Covered.</summary>
public record BoardSummaryDto(int Required, int Present, int Empty, int Covered);

public record AttendanceBoardDto(DateOnly ShiftDate, Shift Shift, bool ShiftEnded, BoardSummaryDto Summary, IReadOnlyList<BoardRowDto> Rows);

/// <summary>SignalR "AttendanceRecorded" to Admins (facility-0027 rule 5): the page reloads that shift's board.</summary>
public record AttendanceChangedDto(int AreaId, DateOnly ShiftDate, Shift Shift);

/// <summary>facility-0051. Time is Thai wall-clock "HH:mm" inside the shift's attendance window (facility-0069); Reason is required.</summary>
public record CorrectAttendanceRequest(int UserId, DateOnly? ShiftDate, Shift? Shift, AttendanceEvent? EventType, string? Time, string? Reason);
