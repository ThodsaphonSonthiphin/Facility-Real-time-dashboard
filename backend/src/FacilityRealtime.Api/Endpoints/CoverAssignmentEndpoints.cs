using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0041: the Admin lets a Cleaner work another Area for one shift. Every change is logged (facility-0051).</summary>
public static class CoverAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapCoverAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/cover-assignments").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapPost("/", AssignAsync);
        admin.MapDelete("/{id:int}", CancelAsync);
        return app;
    }

    private static async Task<IResult> AssignAsync(AssignCoverRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.Shift is not { } shift || !Enum.IsDefined(shift))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุกะ");
        }

        var cleaner = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.Role == UserRole.Cleaner && u.IsActive);
        if (cleaner is null)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องเลือกแม่บ้านที่ใช้งานอยู่");
        }

        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == request.AreaId && a.IsActive);
        if (area is null)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ไม่พบ Area นี้ หรือปิดใช้งานแล้ว");
        }

        if (area.Id == cleaner.AreaId)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "Area นี้เป็น Area ประจำของแม่บ้านคนนี้อยู่แล้ว");
        }

        // Owner decision 2026-10-09: a cover only in the Cleaner's own regular shift
        if (shift != cleaner.Shift)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ทำแทนได้เฉพาะกะประจำของแม่บ้านคนนี้");
        }

        if (!area.HasShift(shift))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "Area นี้ไม่มีกะนี้");
        }

        var slot = new ShiftSlot(request.ShiftDate, shift);
        var now = clock.GetUtcNow().UtcDateTime;
        if (ShiftCalendar.EndUtc(slot) <= now)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "กะนี้จบไปแล้ว");
        }

        var duplicate = await db.CoverAssignments.AnyAsync(c =>
            c.UserId == cleaner.Id && c.AreaId == area.Id && c.ShiftDate == slot.ShiftDate && c.Shift == shift && c.CancelledAt == null);
        if (duplicate)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "มอบหมายไว้แล้ว");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var cover = new CoverAssignment
        {
            UserId = cleaner.Id,
            AreaId = area.Id,
            ShiftDate = slot.ShiftDate,
            Shift = shift,
            AssignedById = admin.Id,
            AssignedAt = now,
        };
        db.CoverAssignments.Add(cover);
        await db.SaveChangesAsync();
        AuditTrail.Add(
            db, admin.Id, now, "COVER_ASSIGN", "cover_assignments", cover.Id,
            $"มอบหมาย {cleaner.DisplayName} ทำแทน {area.Code} กะ{ShiftName(shift)} {slot.ShiftDate:yyyy-MM-dd}",
            before: null,
            after: new { cover.UserId, cover.AreaId, ShiftDate = cover.ShiftDate.ToString("yyyy-MM-dd"), Shift = shift.ToString() });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Created(
            $"/api/admin/cover-assignments/{cover.Id}",
            new CoverAssignmentDto(cover.Id, cleaner.Id, cleaner.DisplayName, area.Id, area.Code, cover.ShiftDate, shift, DateTime.SpecifyKind(now, DateTimeKind.Utc)));
    }

    private static async Task<IResult> CancelAsync(int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var cover = await db.CoverAssignments
            .Include(c => c.User)
            .Include(c => c.Area)
            .FirstOrDefaultAsync(c => c.Id == id && c.CancelledAt == null);
        if (cover is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบการมอบหมายนี้ หรือยกเลิกไปแล้ว");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        // No unique index guards against a racing duplicate, so close every identical active cover with this one.
        var twins = await db.CoverAssignments
            .Where(c => c.Id != cover.Id && c.UserId == cover.UserId && c.AreaId == cover.AreaId
                && c.ShiftDate == cover.ShiftDate && c.Shift == cover.Shift && c.CancelledAt == null)
            .ToListAsync();
        foreach (var twin in twins.Append(cover))
        {
            twin.CancelledById = admin.Id;
            twin.CancelledAt = now;
        }
        AuditTrail.Add(
            db, admin.Id, now, "COVER_CANCEL", "cover_assignments", cover.Id,
            $"ยกเลิกการทำแทนของ {cover.User!.DisplayName} ที่ {cover.Area!.Code} กะ{ShiftName(cover.Shift)} {cover.ShiftDate:yyyy-MM-dd}",
            before: new { cancelled = false },
            after: new { cancelled = true, alsoCancelled = twins.Select(t => t.Id).ToList() });
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static string ShiftName(Shift shift) => shift == Shift.Day ? "เช้า" : "ดึก";
}
