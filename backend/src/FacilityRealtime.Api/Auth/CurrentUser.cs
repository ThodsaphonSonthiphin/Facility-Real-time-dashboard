using System.Security.Claims;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Auth;

public static class CurrentUser
{
    /// <summary>facility-0017: the account in the access token, read fresh; null when it is missing or deactivated.</summary>
    public static async Task<User?> LoadAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var user = int.TryParse(principal.FindFirstValue(AuthClaims.UserId), out var id) ? await db.Users.FindAsync(id) : null;
        return user is { IsActive: true } ? user : null;
    }
}
