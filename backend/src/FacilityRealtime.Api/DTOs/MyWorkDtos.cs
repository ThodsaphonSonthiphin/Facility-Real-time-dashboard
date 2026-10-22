using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record MyWorkAreaDto(int AreaId, string AreaCode, string AreaName, bool IsCover, IReadOnlyList<PointStatusDto> Points);

/// <summary>facility-0077: the Cleaner's regular shift and the Thai wall-clock times its attendance window opens and closes.</summary>
public record OwnShiftDto(Shift Shift, TimeOnly OpensAt, TimeOnly ClosesAt);

/// <summary>
/// facility-0052: the Cleaner's page — attendance buttons, then the own Area, then covered Areas. OwnShift is null
/// for an account with no shift; the phone uses it to say when the own shift is (facility-0077).
/// </summary>
public record MyWorkDto(AttendanceStateDto Attendance, IReadOnlyList<MyWorkAreaDto> Areas, OwnShiftDto? OwnShift);
