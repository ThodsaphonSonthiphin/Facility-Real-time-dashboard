using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;

namespace JanitorialManagementDashboard.Api.Endpoints;

public static class MeEndpoints
{
    /// <summary>The logged-in account, read fresh from the database (the My Work page and its header build on it).</summary>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await CurrentUser.LoadAsync(principal, db); // facility-0105: a deactivated account gets 401 at once
            return user is null ? Results.Unauthorized() : Results.Ok(AuthEndpoints.ToUserDto(user));
        }).RequireAuthorization();

        return app;
    }
}
