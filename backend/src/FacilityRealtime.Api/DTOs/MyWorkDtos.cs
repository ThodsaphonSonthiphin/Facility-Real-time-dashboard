namespace FacilityRealtime.Api.DTOs;

public record MyWorkAreaDto(int AreaId, string AreaCode, string AreaName, bool IsCover, IReadOnlyList<PointStatusDto> Points);

/// <summary>facility-0052: the Cleaner's page — attendance buttons, then the own Area, then covered Areas.</summary>
public record MyWorkDto(AttendanceStateDto Attendance, IReadOnlyList<MyWorkAreaDto> Areas);
