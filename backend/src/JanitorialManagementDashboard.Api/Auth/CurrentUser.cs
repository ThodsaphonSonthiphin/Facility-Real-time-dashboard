using System.Security.Claims;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Auth;

public static class CurrentUser
{
    /// <summary>facility-0017: the account in the access token, read fresh; null when it is missing or deactivated.</summary>
    public static async Task<User?> LoadAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        if (!int.TryParse(principal.FindFirstValue(AuthClaims.UserId), out var id))
        {
            return null;
        }

        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
    }
}
