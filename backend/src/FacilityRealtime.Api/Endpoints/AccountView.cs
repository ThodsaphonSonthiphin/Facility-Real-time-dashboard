using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>What the accounts page shows, deactivated accounts included.</summary>
internal static class AccountView
{
    /// <summary>Admins, then Supervisors, then Cleaners, each by login name. About 180 rows: the page filters them itself (facility-0056).</summary>
    public static async Task<List<AccountDto>> LoadAsync(AppDbContext db, int? onlyUserId = null)
    {
        var users = await db.Users.AsNoTracking()
            .Include(u => u.Area)
            .Include(u => u.Building)
            .Where(u => onlyUserId == null || u.Id == onlyUserId)
            .ToListAsync();
        return users
            .OrderBy(u => RoleOrder(u.Role))
            .ThenBy(u => u.LoginName, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
    }

    public static async Task<AccountDto> LoadOneAsync(AppDbContext db, int userId) => (await LoadAsync(db, userId)).Single();

    private static int RoleOrder(UserRole role) => role switch
    {
        UserRole.Admin => 0,
        UserRole.Supervisor => 1,
        _ => 2,
    };

    private static AccountDto ToDto(User user) => new(
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
        DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc));
}
