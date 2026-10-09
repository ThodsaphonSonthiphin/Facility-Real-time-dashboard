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

/// <summary>The Area form (wireframe D10). The code is fixed once created: every sign of the Area prints it.</summary>
public record CreateAreaRequest(string? Code, string? Name, int BuildingId, ShiftPattern? ShiftPattern, int? DayCleanerId, int? NightCleanerId);

/// <summary>An empty Cleaner slot takes that shift's Cleaner off the Area.</summary>
public record UpdateAreaRequest(string? Name, int BuildingId, ShiftPattern? ShiftPattern, int? DayCleanerId, int? NightCleanerId);

/// <summary>Start and End as "HH:mm" Thai wall-clock, e.g. "07:00" (facility-0047).</summary>
public record RoundWindowRequest(Shift? Shift, string? Start, string? End);

/// <summary>The full list of the point's windows: unchanged ones are kept, the rest replaced.</summary>
public record SavePointRequest(string? Name, List<RoundWindowRequest>? RoundWindows);
