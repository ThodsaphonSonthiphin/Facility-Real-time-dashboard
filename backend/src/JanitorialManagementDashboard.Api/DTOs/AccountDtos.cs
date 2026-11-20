using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Api.DTOs;

/// <summary>
/// One row of the accounts page (wireframe D6). LoginName is the employee ID, or the username of an Admin.
/// No phone, password or hash ever leaves the server (facility-0054).
/// <para>
/// <see cref="CleanerKind"/> and the home building are the dated identity of spec §10 (Task 7): a Relief Cleaner has a home
/// building and no regular Area, a regular one has an Area and belongs to that Area's building. They are null for an Admin
/// or a Supervisor, and for a Cleaner whose dated identity is not known yet.
/// </para>
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
    DateTime CreatedAt,
    CleanerKind? CleanerKind = null,
    int? HomeBuildingId = null,
    string? HomeBuildingCode = null);

/// <summary>
/// Cleaner: EmployeeId, Phone, Shift, optional AreaId. Supervisor: EmployeeId, Phone, BuildingId, Shift.
/// Admin: Username and a temporary Password. Leave out (null) every field the role does not use.
/// <para>
/// Task 7 adds two optional Cleaner fields, both absent for every existing caller: <see cref="CleanerKind"/> says whether
/// this is a regular Cleaner (the default when absent, exactly as before) or a Relief Cleaner, and
/// <see cref="HomeBuildingId"/> is a relief's dated home building. A regular Cleaner derives its building from its Area.
/// </para>
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
    Shift? Shift,
    CleanerKind? CleanerKind = null,
    int? HomeBuildingId = null);

/// <summary>
/// The role, employee ID and username are fixed. The place fields replace the current ones; null clears a Cleaner's Area.
/// <paramref name="CleanerKind"/> absent keeps the identity the person already has (a regular Cleaner when they have no
/// dated profile), so an existing form that sends nothing new changes nothing about the kind.
/// </summary>
public record UpdateAccountRequest(
    string? DisplayName,
    int? AreaId,
    int? BuildingId,
    Shift? Shift,
    CleanerKind? CleanerKind = null,
    int? HomeBuildingId = null);

public record AccountPhoneRequest(string? Phone);

public record AccountPasswordRequest(string? Password);
