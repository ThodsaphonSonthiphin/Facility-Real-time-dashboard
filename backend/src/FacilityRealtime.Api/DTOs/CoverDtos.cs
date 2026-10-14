using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>
/// A Cleaner's cover names AreaId (facility-0041); a Supervisor's names BuildingId (facility-0048). Leave the other out.
/// BlockedScanId: assigning from that Blocked Scan on the alert list (wireframe D3) also closes its alert.
/// </summary>
public record AssignCoverRequest(int UserId, int? AreaId, DateOnly ShiftDate, Shift? Shift, long? BlockedScanId = null, int? BuildingId = null);

/// <summary>Who could cover an Area in a shift (wireframe D3): their own Area, their Shift-In, and their latest scan at this Area's signs.</summary>
public record CoverCandidateDto(int Id, string EmployeeId, string DisplayName, string AreaCode, DateTime? ShiftInAt, DateTime? ScannedThisAreaAt);

/// <summary>Area fields are set for a Cleaner's cover, building fields for a Supervisor's.</summary>
public record CoverAssignmentDto(
    int Id,
    int UserId,
    string PersonName,
    int? AreaId,
    string? AreaCode,
    int? BuildingId,
    string? BuildingCode,
    DateOnly ShiftDate,
    Shift Shift,
    DateTime AssignedAt);
