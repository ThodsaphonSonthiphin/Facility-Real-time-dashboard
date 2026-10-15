using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record AlertPersonDto(int Id, string EmployeeId, string DisplayName, UserRole Role, string? AreaCode);

/// <summary>
/// What to send to POST /api/admin/cover-assignments, with the Blocked Scan's id: AreaId for a Cleaner's cover,
/// BuildingId for a Supervisor's (the other is null).
/// </summary>
public record CoverSuggestionDto(int UserId, int? AreaId, int? BuildingId, DateOnly ShiftDate, Shift Shift);

/// <summary>
/// One line of the alert list (wireframe D3). RecordId is the row in shift_attendances, scan_records, inspections or blocked_scans;
/// for MissingAttendance it is the Shift-In. At is when it happened; for MissingAttendance, when the shift ended.
/// AreaCode is where it happened. RadiusM is the sign's radius now. Cover is set while a cover can still be assigned. BlockReason is set on a Blocked Scan.
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
    CoverSuggestionDto? Cover,
    BlockReason? BlockReason);

/// <summary>"คุยแล้ว ปิดเตือน". Note is optional and kept as the audit row's reason.</summary>
public record ReviewAlertRequest(AlertKind? Kind, long RecordId, string? Note);
