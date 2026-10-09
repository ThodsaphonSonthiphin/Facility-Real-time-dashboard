using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0045: the Admin sets up Areas, their shift pattern and their Cleaners. Every change is logged (facility-0051).</summary>
public static class AdminAreaEndpoints
{
    public static IEndpointRouteBuilder MapAdminAreaEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/buildings", ListBuildingsAsync);
        admin.MapGet("/cleaners", ListCleanersAsync);
        admin.MapGet("/areas", async (AppDbContext db) => Results.Ok(await AdminSetupView.LoadAreasAsync(db)));
        admin.MapGet("/areas/{id:int}", async (int id, AppDbContext db) =>
            await AdminSetupView.LoadAreaAsync(db, id) is { } area ? Results.Ok(area) : AreaNotFound());
        return app;
    }

    private static async Task<IResult> ListBuildingsAsync(AppDbContext db) =>
        Results.Ok(await db.Buildings.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Code)
            .Select(b => new BuildingDto(b.Id, b.Code, b.Name))
            .ToListAsync());

    /// <summary>Every active Cleaner with their regular shift and current Area; the form offers those without an Area.</summary>
    private static async Task<IResult> ListCleanersAsync(AppDbContext db) =>
        Results.Ok(await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive)
            .OrderBy(u => u.EmployeeId)
            .Select(u => new CleanerOptionDto(
                u.Id, u.EmployeeId ?? string.Empty, u.DisplayName, u.Shift, u.AreaId, u.Area == null ? null : u.Area.Code))
            .ToListAsync());

    private static IResult AreaNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบ Area นี้");
}
