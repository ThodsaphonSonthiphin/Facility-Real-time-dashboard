using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Signs;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0045 + 0047: the Admin adds and edits Service Points and their Round Windows. Every change is logged (facility-0051).</summary>
public static class AdminPointEndpoints
{
    public static IEndpointRouteBuilder MapAdminPointEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapPost("/areas/{areaId:int}/points", CreateAsync);
        admin.MapPut("/points/{id:int}", UpdateAsync);
        admin.MapPost("/points/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, false, principal, db, clock));
        admin.MapPost("/points/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, true, principal, db, clock));
        return app;
    }

    private static async Task<IResult> CreateAsync(int areaId, SavePointRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == areaId);
        if (area is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบ Area นี้");
        }

        if (!TryReadForm(request, area.ShiftPattern, out var name, out var windows, out var error))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, error);
        }

        var usedCodes = await db.Signs.Where(s => s.AreaId == area.Id && s.ServicePointId != null).Select(s => s.Code).ToListAsync();
        var lastOrder = await db.ServicePoints.Where(p => p.AreaId == area.Id).MaxAsync(p => (int?)p.SortOrder) ?? 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var point = new ServicePoint { AreaId = area.Id, Name = name, SortOrder = (short)(lastOrder + 1), CreatedAt = now };
        var sign = new Sign
        {
            AreaId = area.Id,
            ServicePoint = point,
            Code = SignCodes.NextPointCode(area.Code, usedCodes),
            QrToken = SignCodes.NewQrToken(),
            QrIssuedAt = now,
        };
        db.Signs.Add(sign);
        db.PointRoundWindows.AddRange(windows.Select(w => NewWindow(point, w, now)));

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.SaveChangesAsync();
            AuditTrail.Add(
                db, admin.Id, now, "POINT_CREATE", "service_points", point.Id, $"เพิ่มจุด {sign.Code} {name}",
                before: null, after: Snapshot(name, windows));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "มีการบันทึกซ้อนกัน ลองใหม่อีกครั้ง");
        }

        return Results.Created($"/api/admin/points/{point.Id}", await AdminSetupView.LoadPointAsync(db, point.Id));
    }

    private static async Task<IResult> UpdateAsync(int id, SavePointRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var point = await db.ServicePoints.Include(p => p.Area).FirstOrDefaultAsync(p => p.Id == id);
        if (point is null)
        {
            return PointNotFound();
        }

        if (!TryReadForm(request, point.Area!.ShiftPattern, out var name, out var wanted, out var error))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, error);
        }

        var existing = await db.PointRoundWindows.Where(w => w.ServicePointId == id).ToListAsync();
        var before = Snapshot(point.Name, existing.Select(ToInput));

        // An unchanged window keeps its id, so the round running now keeps its Scan Records (facility-0047)
        var toAdd = wanted.ToList();
        var toRemove = new List<PointRoundWindow>();
        foreach (var row in existing)
        {
            var same = toAdd.FirstOrDefault(w => w == ToInput(row));
            if (same is null)
            {
                toRemove.Add(row);
            }
            else
            {
                toAdd.Remove(same);
            }
        }

        await RoundWindowRemoval.RemoveAsync(db, toRemove);
        var now = clock.GetUtcNow().UtcDateTime;
        db.PointRoundWindows.AddRange(toAdd.Select(w => NewWindow(point, w, now)));
        point.Name = name;

        if (db.ChangeTracker.HasChanges())
        {
            var code = await db.Signs.Where(s => s.ServicePointId == id).Select(s => s.Code).SingleAsync();
            AuditTrail.Add(
                db, admin.Id, now, "POINT_UPDATE", "service_points", point.Id, $"แก้จุด {code} {name}",
                before, after: Snapshot(name, wanted));
            await db.SaveChangesAsync();
        }

        return Results.Ok(await AdminSetupView.LoadPointAsync(db, point.Id));
    }

    /// <summary>facility-0021 rule 4: a deactivated point leaves the Dashboard and cannot be scanned; history stays.</summary>
    private static async Task<IResult> SetActiveAsync(int id, bool active, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.Id == id);
        if (point is null)
        {
            return PointNotFound();
        }

        if (point.IsActive != active)
        {
            point.IsActive = active;
            var code = await db.Signs.Where(s => s.ServicePointId == id).Select(s => s.Code).SingleAsync();
            AuditTrail.Add(
                db, admin.Id, clock.GetUtcNow().UtcDateTime, active ? "POINT_ACTIVATE" : "POINT_DEACTIVATE", "service_points", point.Id,
                $"{(active ? "เปิด" : "ปิด")}ใช้งานจุด {code} {point.Name}",
                before: new { IsActive = !active }, after: new { IsActive = active });
            await db.SaveChangesAsync();
        }

        return Results.Ok(await AdminSetupView.LoadPointAsync(db, point.Id));
    }

    private static bool TryReadForm(
        SavePointRequest request, ShiftPattern pattern, out string name, out List<RoundWindowInput> windows, out string error)
    {
        name = request.Name?.Trim() ?? string.Empty;
        windows = [];
        if (name.Length is 0 or > 150)
        {
            error = "ต้องใส่ชื่อจุด ไม่เกิน 150 ตัวอักษร";
            return false;
        }

        foreach (var window in request.RoundWindows ?? [])
        {
            if (window.Shift is not { } shift || !Enum.IsDefined(shift))
            {
                error = "ต้องระบุกะของทุกช่วงรอบ";
                return false;
            }

            if (!RoundWindowRules.TryParseTime(window.Start, out var start) || !RoundWindowRules.TryParseTime(window.End, out var end))
            {
                error = "เวลาของช่วงรอบต้องเขียนแบบ 07:00";
                return false;
            }

            windows.Add(new RoundWindowInput(shift, start, end));
        }

        error = RoundWindowRules.Validate(windows, pattern) ?? string.Empty;
        return error.Length == 0;
    }

    private static RoundWindowInput ToInput(PointRoundWindow row) => new(row.Shift, row.StartTime, row.EndTime);

    private static PointRoundWindow NewWindow(ServicePoint point, RoundWindowInput window, DateTime now) => new()
    {
        ServicePoint = point,
        Shift = window.Shift,
        StartTime = window.Start,
        EndTime = window.End,
        CreatedAt = now,
    };

    /// <summary>What the audit log keeps of a point: its name and windows as "DAY 07:00-09:00".</summary>
    private static object Snapshot(string name, IEnumerable<RoundWindowInput> windows) => new
    {
        Name = name,
        RoundWindows = windows
            .OrderBy(w => w.Shift)
            .ThenBy(w => RoundWindowRules.MinutesIntoShift(w.Shift, w.Start))
            .Select(RoundWindowRules.Describe)
            .ToList(),
    };

    private static IResult PointNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบจุดนี้");
}
