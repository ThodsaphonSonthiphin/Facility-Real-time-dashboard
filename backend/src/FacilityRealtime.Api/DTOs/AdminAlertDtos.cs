using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record AlertPersonDto(int Id, string EmployeeId, string DisplayName, UserRole Role, string? AreaCode);

/// <summary>What to send to POST /api/admin/cover-assignments, with the Blocked Scan's id, to let this Cleaner cover that Area.</summary>
public record CoverSuggestionDto(int UserId, int AreaId, DateOnly ShiftDate, Shift Shift);

/// <summary>
/// One line of the alert list (wireframe D3). RecordId is the row in shift_attendances, scan_records or blocked_scans;
/// for MissingAttendance it is the Shift-In. At is when it happened; for MissingAttendance, when the shift ended.
/// AreaCode is where it happened. RadiusM is the sign's radius now. Cover is set while a cover can still be assigned.
/// </summary>
public record AlertDto(
    AlertKind Kind,
    long RecordId,
    DateTime At,
    AlertPersonDto Person,
    string AreaCode,
    string AreaName,
    string? SignCode,
    string? PointName,
    short? DistanceM,
    short? RadiusM,
    short? AccuracyM,
    IReadOnlyList<AttendanceEvent> MissingEvents,
    bool Reviewed,
    DateTime? ReviewedAt,
    CoverSuggestionDto? Cover);

/// <summary>"คุยแล้ว ปิดเตือน". Note is optional and kept as the audit row's reason.</summary>
public record ReviewAlertRequest(AlertKind? Kind, long RecordId, string? Note);
