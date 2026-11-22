using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>What the accounts page shows, deactivated accounts included.</summary>
internal static class AccountView
{
    /// <summary>
    /// Admins, then Supervisors, then Cleaners, each by login name. About 180 rows: the page filters them itself
    /// (facility-0056). Each Cleaner carries the effective dated identity, shift, and regular Area in force on the
    /// clock's business date, resolved from <see cref="CleanerEffectiveAssignment"/> so editing an account never sees
    /// stale <c>users.AreaId</c>/<c>users.Shift</c> projections.
    /// </summary>
    public static async Task<List<AccountDto>> LoadAsync(
        AppDbContext db, TimeProvider clock, int? onlyUserId = null)
    {
        var users = await db.Users.AsNoTracking()
            .Include(u => u.Area)
            .Include(u => u.Building)
            .Where(u => onlyUserId == null || u.Id == onlyUserId)
            .ToListAsync();

        var businessDate = ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime);
        var effectiveByUser = await CleanerEffectiveAssignment.ResolveForDateAsync(
            db, users, businessDate, useFallbackWeekdayArea: true);

        var homeIds = effectiveByUser.Values
            .Where(state => state.HomeBuildingId != null)
            .Select(state => state.HomeBuildingId!.Value)
            .Distinct()
            .ToList();
        var buildingCodes = homeIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Buildings.AsNoTracking()
                .Where(building => homeIds.Contains(building.Id))
                .Select(building => new { building.Id, building.Code })
                .ToDictionaryAsync(building => building.Id, building => building.Code);

        var effectiveAreaIds = effectiveByUser.Values
            .Where(state => state.AreaId != null)
            .Select(state => state.AreaId!.Value)
            .Distinct()
            .ToList();
        var areaCodes = effectiveAreaIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Areas.AsNoTracking()
                .Where(area => effectiveAreaIds.Contains(area.Id))
                .Select(area => new { area.Id, area.Code })
                .ToDictionaryAsync(area => area.Id, area => area.Code);

        return users
            .OrderBy(u => RoleOrder(u.Role))
            .ThenBy(u => u.LoginName, StringComparer.Ordinal)
            .Select(user => ToDto(user, effectiveByUser.GetValueOrDefault(user.Id), buildingCodes, areaCodes))
            .ToList();
    }

    public static async Task<AccountDto> LoadOneAsync(AppDbContext db, TimeProvider clock, int userId) =>
        (await LoadAsync(db, clock, userId)).Single();

    private static int RoleOrder(UserRole role) => role switch
    {
        UserRole.Admin => 0,
        UserRole.Supervisor => 1,
        _ => 2,
    };

    private static AccountDto ToDto(
        User user,
        CleanerEffectiveState? effective,
        IReadOnlyDictionary<int, string> buildingCodes,
        IReadOnlyDictionary<int, string> areaCodes)
    {
        if (user.Role != UserRole.Cleaner || effective is null)
        {
            return new AccountDto(
                user.Id,
                user.Role,
                user.LoginName,
                user.DisplayName,
                user.AreaId,
                user.Area?.Code,
                user.BuildingId,
                user.Building?.Code,
                user.Shift,
                user.IsActive,
                DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc),
                null,
                null,
                null);
        }

        var areaId = effective.AreaId;
        var areaCode = areaId is int aId ? areaCodes.GetValueOrDefault(aId) ?? user.Area?.Code : null;
        var home = effective.HomeBuildingId;
        return new AccountDto(
            user.Id,
            user.Role,
            user.LoginName,
            user.DisplayName,
            areaId,
            areaCode,
            user.BuildingId,
            user.Building?.Code,
            effective.Shift,
            user.IsActive,
            DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc),
            effective.Kind,
            home,
            home is int bId ? buildingCodes.GetValueOrDefault(bId) : null);
    }
}
