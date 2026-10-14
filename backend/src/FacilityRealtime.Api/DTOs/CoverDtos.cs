using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>BlockedScanId: assigning from that Blocked Scan on the alert list (wireframe D3) also closes its alert.</summary>
public record AssignCoverRequest(int UserId, int AreaId, DateOnly ShiftDate, Shift? Shift, long? BlockedScanId = null);

/// <summary>Who could cover an Area in a shift (wireframe D3): their own Area, their Shift-In, and their latest scan at this Area's signs.</summary>
public record CoverCandidateDto(int Id, string EmployeeId, string DisplayName, string AreaCode, DateTime? ShiftInAt, DateTime? ScannedThisAreaAt);

public record CoverAssignmentDto(
    int Id, int UserId, string CleanerName, int AreaId, string AreaCode, DateOnly ShiftDate, Shift Shift, DateTime AssignedAt);
