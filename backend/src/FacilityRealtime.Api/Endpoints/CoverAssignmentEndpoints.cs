using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
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
        admin.MapGet("/candidates", ListCandidatesAsync);
        return app;
    }

    private static async Task<IResult> AssignAsync(AssignCoverRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
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

        // Assigning from the alert list (wireframe D3): the Blocked Scan must be this Cleaner's, at a sign of this Area
        BlockedScan? blocked = null;
        if (request.BlockedScanId is long blockedScanId)
        {
            blocked = await db.BlockedScans.FirstOrDefaultAsync(b => b.Id == blockedScanId);
            if (blocked is null)
            {
                return ApiResults.Message(StatusCodes.Status400BadRequest, "ไม่พบการสแกนที่ถูกบล็อกนี้");
            }

            var signId = blocked.SignId;
            var signAreaId = await db.Signs.Where(s => s.Id == signId).Select(s => s.AreaId).SingleAsync();
            if (blocked.UserId != cleaner.Id || signAreaId != area.Id)
            {
                return ApiResults.Message(StatusCodes.Status400BadRequest, "การสแกนที่ถูกบล็อกนี้ไม่ใช่ของแม่บ้านคนนี้ที่ Area นี้");
            }

            // A scan from another shift is no reason to cover this one
            if (AttendanceCalendar.SlotFor(shift, blocked.ScannedAt, settings) != slot)
            {
                return ApiResults.Message(StatusCodes.Status400BadRequest, "การสแกนที่ถูกบล็อกนี้ไม่ได้อยู่ในกะนี้");
            }
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
            $"มอบหมาย {cleaner.DisplayName} ทำแทน {area.Code} กะ{ThaiLabels.ShiftName(shift)} {ThaiLabels.Date(slot.ShiftDate)}",
            before: null,
            after: new
            {
                cover.UserId,
                cover.AreaId,
                ShiftDate = ThaiLabels.Date(cover.ShiftDate),
                Shift = shift.ToString(),
                BlockedScanId = blocked?.Id,
            });

        // The alert is handled: close it, unless an Admin already did (facility-0041 rule 4)
        if (blocked is { } scan && !await db.AlertReviews.AnyAsync(r => r.AlertType == AlertType.BlockedScan && r.RecordId == scan.Id))
        {
            AdminAlertEndpoints.AddReview(
                db, AlertType.BlockedScan, scan.Id, "blocked_scans", admin.Id, now,
                $"ปิดเตือน{ThaiLabels.AlertName(AlertKind.BlockedScan)} ของ {cleaner.DisplayName} เวลา {ThaiLabels.Clock(scan.ScannedAt)}: มอบหมายทำแทน {area.Code} แล้ว",
                note: null);
        }

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
            $"ยกเลิกการทำแทนของ {cover.User!.DisplayName} ที่ {cover.Area!.Code} กะ{ThaiLabels.ShiftName(cover.Shift)} {ThaiLabels.Date(cover.ShiftDate)}",
            before: new { cancelled = false },
            after: new { cancelled = true, alsoCancelled = twins.Select(t => t.Id).ToList() });
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    /// <summary>
    /// Wireframe D3 "ให้ใครทำแทน": active Cleaners of that regular shift with an Area of their own (they check in at its sign),
    /// not already covering this Area. Whoever scanned this Area's signs in the shift comes first, then who checked in.
    /// </summary>
    private static async Task<IResult> ListCandidatesAsync(
        int? areaId, string? shiftDate, string? shift, ClaimsPrincipal principal, AppDbContext db, AttendanceSettings settings)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        if (areaId is null || !AdminQuery.TryReadDate(shiftDate, out var date) || !AdminQuery.TryReadEnum<Shift>(shift, out var parsedShift))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุ Area วันที่ (yyyy-MM-dd) และกะ (Day หรือ Night)");
        }

        var area = await db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId && a.IsActive);
        if (area is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบ Area นี้ หรือปิดใช้งานแล้ว");
        }

        if (!area.HasShift(parsedShift))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "Area นี้ไม่มีกะนี้");
        }

        var slot = new ShiftSlot(date, parsedShift);
        var cleaners = await db.Users.AsNoTracking()
            .Include(u => u.Area)
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.Shift == slot.Shift && u.AreaId != null && u.AreaId != area.Id)
            .ToListAsync();
        var ids = cleaners.Select(u => u.Id).ToList();
        var covering = await db.CoverAssignments.AsNoTracking()
            .Where(c => c.AreaId == area.Id && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.UserId)
            .ToListAsync();
        var shiftIns = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift && a.EventType == AttendanceEvent.ShiftIn && ids.Contains(a.UserId))
            .ToListAsync();
        var opens = ShiftCalendar.StartUtc(slot).AddMinutes(-settings.OpensMinutesBeforeShift);
        var closes = ShiftCalendar.EndUtc(slot).AddMinutes(settings.ClosesMinutesAfterShift);
        var areaSignIds = await db.Signs.Where(s => s.AreaId == area.Id).Select(s => s.Id).ToListAsync();
        var scans = await db.BlockedScans.AsNoTracking()
            .Where(b => ids.Contains(b.UserId) && areaSignIds.Contains(b.SignId) && b.ScannedAt >= opens && b.ScannedAt <= closes)
            .ToListAsync();

        var candidates = cleaners
            .Where(u => !covering.Contains(u.Id))
            .Select(u => new CoverCandidateDto(
                u.Id,
                u.EmployeeId ?? string.Empty,
                u.DisplayName,
                u.Area!.Code,
                shiftIns.Where(a => a.UserId == u.Id).Select(a => (DateTime?)Utc(a.OccurredAt)).FirstOrDefault(),
                scans.Where(b => b.UserId == u.Id).Select(b => (DateTime?)Utc(b.ScannedAt)).Max()))
            .OrderBy(c => c.ScannedThisAreaAt is null ? 1 : 0)
            .ThenByDescending(c => c.ScannedThisAreaAt)
            .ThenBy(c => c.ShiftInAt is null ? 1 : 0)
            .ThenBy(c => c.ShiftInAt)
            .ThenBy(c => c.EmployeeId)
            .ToList();
        return Results.Ok(candidates);
    }

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z".</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);
}
