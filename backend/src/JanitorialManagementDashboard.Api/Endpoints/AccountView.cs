using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>What the accounts page shows, deactivated accounts included.</summary>
internal static class AccountView
{
    /// <summary>
    /// Admins, then Supervisors, then Cleaners, each by login name. About 180 rows: the page filters them itself
    /// (facility-0056). Each Cleaner also carries the dated identity in force on the clock's business date (Task 7) — the
    /// regular/relief kind and the home building the screen shows and the form edits — read from the same rule the week read
    /// uses, so the page never claims a history the stored rows do not have.
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
        var cleanerIds = users.Where(u => u.Role == UserRole.Cleaner).Select(u => u.Id).ToList();
        var profiles = cleanerIds.Count == 0
            ? []
            : await db.CleanerProfilePeriods.AsNoTracking()
                .Where(period => cleanerIds.Contains(period.UserId))
                .OrderBy(period => period.EffectiveFrom).ThenBy(period => period.Id)
                .ToListAsync();
        var homeIds = profiles.Where(period => period.HomeBuildingId != null)
            .Select(period => period.HomeBuildingId!.Value)
            .Concat(users.Where(u => u.Role == UserRole.Cleaner && u.Area != null).Select(u => u.Area!.BuildingId))
            .Distinct()
            .ToList();
        var codes = homeIds.Count == 0
            ? []
            : await db.Buildings.AsNoTracking()
                .Where(building => homeIds.Contains(building.Id))
                .Select(building => new { building.Id, building.Code })
                .ToDictionaryAsync(building => building.Id, building => building.Code);

        return users
            .OrderBy(u => RoleOrder(u.Role))
            .ThenBy(u => u.LoginName, StringComparer.Ordinal)
            .Select(user => ToDto(user, Identity(user, profiles, businessDate), codes))
            .ToList();
    }

    public static async Task<AccountDto> LoadOneAsync(AppDbContext db, TimeProvider clock, int userId) =>
        (await LoadAsync(db, clock, userId)).Single();

    /// <summary>
    /// The identity in force on the business date: the dated profile that covers it, else — only for a Cleaner with no dated
    /// profile at all — the account row's own projection (spec §14). A profile that ended before today lends nothing, so the
    /// page never shows a current-looking identity the stored rows do not state.
    /// </summary>
    private static CleanerProfilePeriod? Identity(
        User user, IReadOnlyList<CleanerProfilePeriod> profiles, DateOnly businessDate) =>
        user.Role != UserRole.Cleaner
            ? null
            : profiles.Where(period => period.UserId == user.Id)
                .LastOrDefault(period => ScheduleResolver.Applies(period.EffectiveFrom, period.EffectiveUntil, businessDate));

    private static int RoleOrder(UserRole role) => role switch
    {
        UserRole.Admin => 0,
        UserRole.Supervisor => 1,
        _ => 2,
    };

    private static AccountDto ToDto(
        User user, CleanerProfilePeriod? profile, IReadOnlyDictionary<int, string> codes)
    {
        // A Cleaner whose dated identity is not stored yet is a regular one when their account row names an Area (the
        // pre-backfill projection of spec §14); with no Area and no profile there is nothing to show rather than a guess.
        var kind = user.Role != UserRole.Cleaner
            ? (CleanerKind?)null
            : profile?.Kind ?? (user.AreaId is not null ? CleanerKind.Regular : null);
        var home = profile?.HomeBuildingId ?? (user.Role == UserRole.Cleaner ? user.Area?.BuildingId : null);
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
            kind,
            home,
            home is int id ? codes.GetValueOrDefault(id) : null);
    }
}
