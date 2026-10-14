using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>
/// One row of the accounts page (wireframe D6). LoginName is the employee ID, or the username of an Admin.
/// No phone, password or hash ever leaves the server (facility-0054).
/// </summary>
public record AccountDto(
    int Id,
    UserRole Role,
    string LoginName,
    string DisplayName,
    int? AreaId,
    string? AreaCode,
    int? BuildingId,
    string? BuildingCode,
    Shift? Shift,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>
/// Cleaner: EmployeeId, Phone, Shift, optional AreaId. Supervisor: EmployeeId, Phone, BuildingId, Shift.
/// Admin: Username and a temporary Password. Leave out (null) every field the role does not use.
/// </summary>
public record CreateAccountRequest(
    UserRole? Role,
    string? DisplayName,
    string? EmployeeId,
    string? Phone,
    string? Username,
    string? Password,
    int? AreaId,
    int? BuildingId,
    Shift? Shift);

/// <summary>The role, employee ID and username are fixed. The place fields replace the current ones; null clears a Cleaner's Area.</summary>
public record UpdateAccountRequest(string? DisplayName, int? AreaId, int? BuildingId, Shift? Shift);

public record AccountPhoneRequest(string? Phone);

public record AccountPasswordRequest(string? Password);
