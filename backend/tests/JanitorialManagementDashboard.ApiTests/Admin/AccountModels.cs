namespace JanitorialManagementDashboard.ApiTests.Admin;

/// <summary>
/// One account row as the accounts page reads it. The three trailing fields are Task 7's dated Cleaner identity: a Relief
/// Cleaner's kind and home building, null for an Admin, a Supervisor, or a Cleaner with no identity stored yet.
/// </summary>
public record AccountModel(
    int Id, string Role, string LoginName, string DisplayName, int? AreaId, string? AreaCode, int? BuildingId, string? BuildingCode,
    string? Shift, bool IsActive, DateTime CreatedAt, string? CleanerKind = null, int? HomeBuildingId = null,
    string? HomeBuildingCode = null);
