using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Endpoints;

public static class MeEndpoints
{
    /// <summary>The logged-in account, read fresh from the database (the My Work page and its header build on it).</summary>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = int.TryParse(principal.FindFirstValue(AuthClaims.UserId), out var id) ? await db.Users.FindAsync(id) : null;
            return user is null ? Results.Unauthorized() : Results.Ok(AuthEndpoints.ToUserDto(user));
        }).RequireAuthorization();

        return app;
    }
}
