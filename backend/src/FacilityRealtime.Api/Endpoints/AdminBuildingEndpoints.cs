using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Buildings;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0090: the Admin adds, renames and turns buildings on and off; a building is never deleted. The forms' list of
/// active buildings stays <c>GET /api/admin/buildings</c> in <see cref="AdminAreaEndpoints"/>. Every change is logged
/// (facility-0051).
/// </summary>
public static class AdminBuildingEndpoints
{
    public static IEndpointRouteBuilder MapAdminBuildingEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/buildings/all", async (ClaimsPrincipal principal, AppDbContext db) =>
            await CurrentUser.LoadAsync(principal, db) is null
                ? Results.Unauthorized()
                : Results.Ok(await Rows(db, db.Buildings.AsNoTracking().OrderBy(b => b.Code)).ToListAsync()));
        admin.MapPost("/buildings", CreateAsync);
        admin.MapPut("/buildings/{id:int}", RenameAsync);
        admin.MapPost("/buildings/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, false, principal, db, clock));
        admin.MapPost("/buildings/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, true, principal, db, clock));
        return app;
    }

    /// <summary>What the audit log keeps of a building on add and rename.</summary>
    private sealed record BuildingSnapshot(string Code, string Name);

    private static async Task<IResult> CreateAsync(CreateBuildingRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (BuildingRules.NormalizeCode(request.Code) is not { } code)
        {
            return Bad(BuildingRules.CodeProblem);
        }

        if (BuildingRules.NormalizeName(request.Name) is not { } name)
        {
            return Bad(BuildingRules.NameProblem);
        }

        // Decision 2: a code is never used twice, an off building's included
        if (await db.Buildings.AnyAsync(b => b.Code == code))
        {
            return Conflict(BuildingRules.CodeTaken);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var building = new Building { Code = code, Name = name, CreatedAt = now };
        db.Buildings.Add(building);
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.SaveChangesAsync();
            AuditTrail.Add(
                db, admin.Id, now, "BUILDING_CREATE", "buildings", building.Id, $"เพิ่มตึก {code} {name}",
                before: null, after: new BuildingSnapshot(code, name));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // The unique index on the code: another Admin added the same code at the same moment. Any other failed
            // save (a deadlock, a lock timeout, a dropped connection) is not "code taken", so it is rethrown.
            db.ChangeTracker.Clear();
            if (await db.Buildings.AnyAsync(b => b.Code == code))
            {
                return Conflict(BuildingRules.CodeTaken);
            }

            throw;
        }

        return Results.Created($"/api/admin/buildings/{building.Id}", await LoadOneAsync(db, building.Id));
    }

    private static async Task<IResult> RenameAsync(int id, UpdateBuildingRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id);
        if (building is null)
        {
            return BuildingNotFound();
        }

        if (BuildingRules.NormalizeName(request.Name) is not { } name)
        {
            return Bad(BuildingRules.NameProblem);
        }

        if (building.Name != name)
        {
            var before = new BuildingSnapshot(building.Code, building.Name);
            building.Name = name;
            AuditTrail.Add(
                db, admin.Id, clock.GetUtcNow().UtcDateTime, "BUILDING_UPDATE", "buildings", building.Id,
                $"แก้ตึก {building.Code} {name}", before, after: new BuildingSnapshot(building.Code, name));
            await db.SaveChangesAsync();
        }

        return Results.Ok(await LoadOneAsync(db, building.Id));
    }

    /// <summary>Decision 4: off only with no active Area and no active Supervisor in it; on with no condition.</summary>
    private static async Task<IResult> SetActiveAsync(int id, bool active, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id);
        if (building is null)
        {
            return BuildingNotFound();
        }

        if (building.IsActive == active)
        {
            return Results.Ok(await LoadOneAsync(db, building.Id));
        }

        if (!active)
        {
            var activeAreas = await db.Areas.CountAsync(a => a.BuildingId == building.Id && a.IsActive);
            var activeSupervisors = await db.Users.CountAsync(
                u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == building.Id);
            if (BuildingRules.DeactivateProblem(building.Code, activeAreas, activeSupervisors) is { } problem)
            {
                return Conflict(problem);
            }
        }

        building.IsActive = active;
        AuditTrail.Add(
            db, admin.Id, clock.GetUtcNow().UtcDateTime, active ? "BUILDING_ACTIVATE" : "BUILDING_DEACTIVATE", "buildings",
            building.Id, $"{(active ? "เปิด" : "ปิด")}ใช้งานตึก {building.Code}",
            before: new { IsActive = !active }, after: new { IsActive = active });
        await db.SaveChangesAsync();

        return Results.Ok(await LoadOneAsync(db, building.Id));
    }

    /// <summary>Filter and order <paramref name="buildings"/> before this: EF cannot order by the record it builds.</summary>
    private static IQueryable<ManagedBuildingDto> Rows(AppDbContext db, IQueryable<Building> buildings) =>
        buildings.Select(b => new ManagedBuildingDto(
            b.Id,
            b.Code,
            b.Name,
            b.IsActive,
            db.Areas.Count(a => a.BuildingId == b.Id && a.IsActive),
            db.Users.Count(u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == b.Id)));

    private static Task<ManagedBuildingDto> LoadOneAsync(AppDbContext db, int id) =>
        Rows(db, db.Buildings.AsNoTracking().Where(b => b.Id == id)).SingleAsync();

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);

    private static IResult Conflict(string message) => ApiResults.Message(StatusCodes.Status409Conflict, message);

    private static IResult BuildingNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบตึกนี้");
}
