using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record AssignCoverRequest(int UserId, int AreaId, DateOnly ShiftDate, Shift? Shift);

public record CoverAssignmentDto(
    int Id, int UserId, string CleanerName, int AreaId, string AreaCode, DateOnly ShiftDate, Shift Shift, DateTime AssignedAt);
