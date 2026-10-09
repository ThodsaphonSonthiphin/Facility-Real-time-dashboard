using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Signs;
using FacilityRealtime.Domain.Entities;
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
        admin.MapPost("/areas", CreateAsync);
        admin.MapPut("/areas/{id:int}", UpdateAsync);
        admin.MapPost("/areas/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, false, principal, db, clock));
        admin.MapPost("/areas/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, true, principal, db, clock));
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

    private sealed record AreaForm(string Name, Building Building, ShiftPattern Pattern, User? DayCleaner, User? NightCleaner);

    /// <summary>What the audit log keeps of an Area: no names of people, only ids.</summary>
    private sealed record AreaSnapshot(
        string Code, string Name, int BuildingId, string ShiftPattern, int? DayCleanerId, int? NightCleanerId, int? RemovedNightWindows = null);

    private static async Task<IResult> CreateAsync(CreateAreaRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (SignCodes.NormalizeAreaCode(request.Code) is not { } code)
        {
            return Bad("รหัส Area ต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 2–20 ตัว เช่น AR03");
        }

        var (form, error) = await ReadFormAsync(
            db, areaId: null, request.Name, request.BuildingId, request.ShiftPattern, request.DayCleanerId, request.NightCleanerId);
        if (form is null)
        {
            return error!;
        }

        if (await db.Areas.AnyAsync(a => a.Code == code))
        {
            return Conflict("รหัส Area นี้มีแล้ว");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var area = new Area { BuildingId = form.Building.Id, Code = code, Name = form.Name, ShiftPattern = form.Pattern, CreatedAt = now };
        // facility-0045: the Area's one check-in sign comes with it
        db.Signs.Add(new Sign { Area = area, Code = SignCodes.CheckIn(code), QrToken = SignCodes.NewQrToken(), QrIssuedAt = now });

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.SaveChangesAsync();
            await AreaCleaners.ApplyAsync(db, area.Id, form.DayCleaner, form.NightCleaner);
            AuditTrail.Add(
                db, admin.Id, now, "AREA_CREATE", "areas", area.Id, $"เพิ่ม Area {code} {form.Name}",
                before: null, after: Snapshot(area, form.DayCleaner, form.NightCleaner));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("มีการบันทึกซ้อนกัน ลองใหม่อีกครั้ง");
        }

        return Results.Created($"/api/admin/areas/{area.Id}", await AdminSetupView.LoadAreaAsync(db, area.Id));
    }

    private static async Task<IResult> UpdateAsync(int id, UpdateAreaRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == id);
        if (area is null)
        {
            return AreaNotFound();
        }

        var (form, error) = await ReadFormAsync(
            db, area.Id, request.Name, request.BuildingId, request.ShiftPattern, request.DayCleanerId, request.NightCleanerId);
        if (form is null)
        {
            return error!;
        }

        var current = await db.Users.Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == area.Id).ToListAsync();
        var before = Snapshot(area, current.FirstOrDefault(u => u.Shift == Shift.Day), current.FirstOrDefault(u => u.Shift == Shift.Night));

        area.Name = form.Name;
        area.BuildingId = form.Building.Id;
        area.ShiftPattern = form.Pattern;
        var now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            List<PointRoundWindow> nightWindows = form.Pattern == ShiftPattern.DayOnly
                ? await db.PointRoundWindows.Where(w => w.Shift == Shift.Night && w.ServicePoint!.AreaId == area.Id).ToListAsync()
                : [];
            // Wireframe D10: a day-only Area has no night rounds
            await RoundWindowRemoval.RemoveAsync(db, nightWindows);
            await AreaCleaners.ApplyAsync(db, area.Id, form.DayCleaner, form.NightCleaner);
            var after = Snapshot(area, form.DayCleaner, form.NightCleaner);
            if (after != before || nightWindows.Count > 0)
            {
                AuditTrail.Add(
                    db, admin.Id, now, "AREA_UPDATE", "areas", area.Id, $"แก้ Area {area.Code} {area.Name}",
                    before, after with { RemovedNightWindows = nightWindows.Count });
                await db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("มีการบันทึกซ้อนกัน ลองใหม่อีกครั้ง");
        }

        return Results.Ok(await AdminSetupView.LoadAreaAsync(db, area.Id));
    }

    /// <summary>Wireframe D10: a deactivated Area and its signs leave the Dashboard and cannot be scanned; history stays.</summary>
    private static async Task<IResult> SetActiveAsync(int id, bool active, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == id);
        if (area is null)
        {
            return AreaNotFound();
        }

        if (area.IsActive != active)
        {
            area.IsActive = active;
            AuditTrail.Add(
                db, admin.Id, clock.GetUtcNow().UtcDateTime, active ? "AREA_ACTIVATE" : "AREA_DEACTIVATE", "areas", area.Id,
                $"{(active ? "เปิด" : "ปิด")}ใช้งาน Area {area.Code}",
                before: new { IsActive = !active }, after: new { IsActive = active });
            await db.SaveChangesAsync();
        }

        return Results.Ok(await AdminSetupView.LoadAreaAsync(db, area.Id));
    }

    /// <summary>The fields Create and Update share; returns the form, or the answer to give instead.</summary>
    private static async Task<(AreaForm? Form, IResult? Error)> ReadFormAsync(
        AppDbContext db, int? areaId, string? name, int buildingId, ShiftPattern? pattern, int? dayCleanerId, int? nightCleanerId)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > 150)
        {
            return (null, Bad("ต้องใส่ชื่อ Area ไม่เกิน 150 ตัวอักษร"));
        }

        if (pattern is not { } shiftPattern || !Enum.IsDefined(shiftPattern))
        {
            return (null, Bad("ต้องเลือกรูปแบบกะ"));
        }

        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId && b.IsActive);
        if (building is null)
        {
            return (null, Bad("ไม่พบตึกนี้"));
        }

        if (shiftPattern == ShiftPattern.DayOnly && nightCleanerId is not null)
        {
            return (null, Bad("Area ที่ทำเฉพาะกะเช้าไม่มีแม่บ้านกะดึก"));
        }

        User? day = null;
        User? night = null;
        foreach (var (shift, cleanerId) in new[] { (Shift.Day, dayCleanerId), (Shift.Night, nightCleanerId) })
        {
            if (cleanerId is null)
            {
                continue;
            }

            var cleaner = await db.Users.Include(u => u.Area)
                .FirstOrDefaultAsync(u => u.Id == cleanerId && u.Role == UserRole.Cleaner && u.IsActive);
            if (cleaner is null)
            {
                return (null, Bad("ต้องเลือกแม่บ้านที่ใช้งานอยู่"));
            }

            if (cleaner.Shift != shift)
            {
                return (null, Bad($"{cleaner.DisplayName} ประจำกะ{ShiftName(cleaner.Shift)} ใส่ในช่องกะ{ShiftName(shift)}ไม่ได้"));
            }

            // Wireframe D10: the picker offers only Cleaners without an Area in that shift
            if (cleaner.AreaId is { } otherAreaId && otherAreaId != areaId)
            {
                return (null, Conflict($"{cleaner.DisplayName} ประจำ {cleaner.Area!.Code} อยู่แล้ว ย้ายออกจาก Area นั้นก่อน"));
            }

            if (shift == Shift.Day)
            {
                day = cleaner;
            }
            else
            {
                night = cleaner;
            }
        }

        return (new AreaForm(trimmed, building, shiftPattern, day, night), null);
    }

    private static AreaSnapshot Snapshot(Area area, User? day, User? night) =>
        new(area.Code, area.Name, area.BuildingId, area.ShiftPattern.ToString(), day?.Id, night?.Id);

    private static string ShiftName(Shift? shift) => shift switch
    {
        Shift.Day => "เช้า",
        Shift.Night => "ดึก",
        _ => "ที่ไม่ระบุ",
    };

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);

    private static IResult Conflict(string message) => ApiResults.Message(StatusCodes.Status409Conflict, message);

    private static IResult AreaNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบ Area นี้");
}
