using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The attendance page (wireframe D2): who is in each Area this shift (facility-0027, 0050).</summary>
public static class AdminAttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAdminAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/attendance").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/", GetBoardAsync);
        return app;
    }

    private static async Task<IResult> GetBoardAsync(
        string? shiftDate, string? shift, int? buildingId, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        return AdminQuery.ReadSlot(shiftDate, shift, now, out var slot) is { } error
            ? ApiResults.Message(StatusCodes.Status400BadRequest, error)
            : Results.Ok(await AttendanceBoard.LoadAsync(db, slot, buildingId, now));
    }
}
