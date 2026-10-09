using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record BuildingDto(int Id, string Code, string Name);

/// <summary>For the Area form's Cleaner pickers (wireframe D10).</summary>
public record CleanerOptionDto(int Id, string EmployeeId, string DisplayName, Shift? Shift, int? AreaId, string? AreaCode);

public record AssignedCleanerDto(int Id, string EmployeeId, string DisplayName);

/// <summary>SignsNotConfirmedOnSite: the check-in sign and active points' signs with no location, or only a map pick (facility-0045).</summary>
public record AreaSummaryDto(
    int Id,
    string Code,
    string Name,
    int BuildingId,
    string BuildingCode,
    ShiftPattern ShiftPattern,
    bool IsActive,
    AssignedCleanerDto? DayCleaner,
    AssignedCleanerDto? NightCleaner,
    int ActivePointCount,
    int SignsNotConfirmedOnSite);

/// <summary>Carries the QR Token: the Admin's points and print pages are where it comes from (facility-0060).</summary>
public record AdminSignDto(
    int Id,
    string Code,
    string QrToken,
    DateTime QrIssuedAt,
    decimal? Latitude,
    decimal? Longitude,
    short? LocationAccuracyM,
    LocationSource? LocationSource,
    DateTime? LocatedAt,
    short RadiusM);

/// <summary>Start and End are Thai wall-clock "HH:mm", as the Admin types them.</summary>
public record AdminRoundWindowDto(int Id, Shift Shift, string Start, string End);

public record AdminPointDto(int Id, string Name, short SortOrder, bool IsActive, AdminSignDto Sign, IReadOnlyList<AdminRoundWindowDto> RoundWindows);

public record AreaDetailDto(AreaSummaryDto Area, AdminSignDto CheckInSign, IReadOnlyList<AdminPointDto> Points);
