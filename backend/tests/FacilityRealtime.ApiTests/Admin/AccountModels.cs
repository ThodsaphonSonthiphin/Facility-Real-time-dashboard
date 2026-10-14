namespace FacilityRealtime.ApiTests.Admin;

public record AccountModel(
    int Id, string Role, string LoginName, string DisplayName, int? AreaId, string? AreaCode, int? BuildingId, string? BuildingCode,
    string? Shift, bool IsActive, DateTime CreatedAt);
