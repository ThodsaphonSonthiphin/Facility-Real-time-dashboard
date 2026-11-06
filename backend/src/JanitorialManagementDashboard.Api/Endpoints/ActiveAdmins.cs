using System.Globalization;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>
/// facility-0105: pushes to Admins go to the active Admins read at send time, never to a group joined once at connect, so
/// a deactivated Admin whose page is still open gets nothing from the next push on.
/// </summary>
internal static class ActiveAdmins
{
    public static async Task<IReadOnlyList<string>> IdsAsync(AppDbContext db) =>
        (await db.Users.Where(u => u.Role == UserRole.Admin && u.IsActive).Select(u => u.Id).ToListAsync())
            .Select(id => id.ToString(CultureInfo.InvariantCulture))
            .ToList();
}
