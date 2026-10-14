namespace FacilityRealtime.ApiTests.Admin;

public record MessageModel(string Message);

public record BuildingModel(int Id, string Code, string Name);

public record CleanerOptionModel(int Id, string EmployeeId, string DisplayName, string? Shift, int? AreaId, string? AreaCode);

public record AssignedCleanerModel(int Id, string EmployeeId, string DisplayName);

public record AreaSummaryModel(
    int Id, string Code, string Name, int BuildingId, string BuildingCode, string ShiftPattern, bool IsActive,
    AssignedCleanerModel? DayCleaner, AssignedCleanerModel? NightCleaner, int ActivePointCount, int SignsNotConfirmedOnSite);

public record AdminSignModel(
    int Id, string Code, string QrToken, DateTime QrIssuedAt, decimal? Latitude, decimal? Longitude,
    short? LocationAccuracyM, string? LocationSource, DateTime? LocatedAt, short RadiusM);

public record AdminRoundWindowModel(int Id, string Shift, string Start, string End);

public record AdminPointModel(int Id, string Name, short SortOrder, bool IsActive, AdminSignModel Sign, List<AdminRoundWindowModel> RoundWindows);

public record AreaDetailModel(AreaSummaryModel Area, AdminSignModel CheckInSign, List<AdminPointModel> Points);
