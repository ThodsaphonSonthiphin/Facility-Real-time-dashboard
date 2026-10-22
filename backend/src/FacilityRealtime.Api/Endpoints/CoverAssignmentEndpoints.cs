using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0041: the Admin lets a Cleaner work another Area for one shift; facility-0048: a Supervisor inspect another
/// building, or their own outside their shift, for one shift. Every change is logged (facility-0051).
/// </summary>
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
            return Bad("ต้องระบุกะ");
        }

        var person = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive && u.Role != UserRole.Admin);
        if (person is null)
        {
            return Bad("ต้องเลือกแม่บ้านหรือหัวหน้าที่ใช้งานอยู่");
        }

        var slot = new ShiftSlot(request.ShiftDate, shift);
        var now = clock.GetUtcNow().UtcDateTime;
        return person.Role == UserRole.Supervisor
            ? await AssignBuildingAsync(request, db, admin, person, slot, now)
            : await AssignAreaAsync(request, db, admin, person, slot, now, settings);
    }

    /// <summary>facility-0041: a Cleaner works another Area, only in their own regular shift (owner decision 2026-10-09).</summary>
    private static async Task<IResult> AssignAreaAsync(
        AssignCoverRequest request, AppDbContext db, User admin, User cleaner, ShiftSlot slot, DateTime now, AttendanceSettings settings)
    {
        if (request.BuildingId is not null)
        {
            return Bad("แม่บ้านทำแทนเป็นพื้นที่ ไม่ต้องระบุตึก");
        }

        var area = request.AreaId is int areaId ? await db.Areas.FirstOrDefaultAsync(a => a.Id == areaId && a.IsActive) : null;
        if (area is null)
        {
            return Bad("ไม่พบพื้นที่นี้ หรือปิดใช้งานแล้ว");
        }

        if (area.Id == cleaner.AreaId)
        {
            return Bad("พื้นที่นี้เป็นพื้นที่ประจำของแม่บ้านคนนี้อยู่แล้ว");
        }

        if (slot.Shift != cleaner.Shift)
        {
            return Bad("ทำแทนได้เฉพาะกะประจำของแม่บ้านคนนี้");
        }

        if (!area.HasShift(slot.Shift))
        {
            return Bad("พื้นที่นี้ไม่มีกะนี้");
        }

        if (ShiftCalendar.EndUtc(slot) <= now)
        {
            return Bad("กะนี้จบไปแล้ว");
        }

        if (await IsDuplicateAsync(db, cleaner.Id, area.Id, null, slot))
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
                return Bad("ไม่พบการสแกนนี้");
            }

            var signId = blocked.SignId;
            var signAreaId = await db.Signs.Where(s => s.Id == signId).Select(s => s.AreaId).SingleAsync();
            if (blocked.UserId != cleaner.Id || signAreaId != area.Id)
            {
                return Bad("การสแกนนี้ไม่ใช่ของแม่บ้านคนนี้ที่พื้นที่นี้");
            }

            // A scan from another shift is no reason to cover this one
            if (AttendanceCalendar.SlotFor(slot.Shift, blocked.ScannedAt, settings) != slot)
            {
                return Bad("การสแกนนี้ไม่ได้อยู่ในกะนี้");
            }
        }

        var cover = new CoverAssignment
        {
            UserId = cleaner.Id,
            AreaId = area.Id,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            AssignedById = admin.Id,
            AssignedAt = now,
        };
        return await SaveAsync(
            db, admin, cleaner, cover, blocked, now,
            $"มอบหมาย {cleaner.DisplayName} ทำแทน {area.Code} กะ{ThaiLabels.ShiftName(slot.Shift)} {ThaiLabels.Date(slot.ShiftDate)}",
            $"มอบหมายทำแทน {area.Code} แล้ว",
            area,
            null);
    }

    /// <summary>
    /// facility-0048 rule 2 and its amendment: a Supervisor inspects another building, or their own outside their shift.
    /// Any shift may be covered except the one they already hold (decision 7). The shift is the clock's.
    /// </summary>
    private static async Task<IResult> AssignBuildingAsync(
        AssignCoverRequest request, AppDbContext db, User admin, User supervisor, ShiftSlot slot, DateTime now)
    {
        if (request.AreaId is not null)
        {
            return Bad("หัวหน้าตรวจแทนเป็นตึก ไม่ต้องระบุพื้นที่");
        }

        var building = request.BuildingId is int buildingId ? await db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId && b.IsActive) : null;
        if (building is null)
        {
            return Bad("ไม่พบตึกนี้ หรือปิดใช้งานแล้ว");
        }

        if (SupervisorScope.WithoutCover(supervisor.BuildingId, supervisor.Shift, building.Id, slot.Shift) is null)
        {
            return Bad("ตึกและกะนี้เป็นของหัวหน้าคนนี้อยู่แล้ว");
        }

        if (ShiftCalendar.EndUtc(slot) <= now)
        {
            return Bad("กะนี้จบไปแล้ว");
        }

        if (await IsDuplicateAsync(db, supervisor.Id, null, building.Id, slot))
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "มอบหมายไว้แล้ว");
        }

        BlockedScan? blocked = null;
        if (request.BlockedScanId is long blockedScanId)
        {
            blocked = await db.BlockedScans.FirstOrDefaultAsync(b => b.Id == blockedScanId);
            if (blocked is null)
            {
                return Bad("ไม่พบการสแกนนี้");
            }

            var signId = blocked.SignId;
            var signBuildingId = await db.Signs.Where(s => s.Id == signId).Select(s => s.Area!.BuildingId).SingleAsync();
            if (blocked.UserId != supervisor.Id || signBuildingId != building.Id)
            {
                return Bad("การสแกนนี้ไม่ใช่ของหัวหน้าคนนี้ที่ตึกนี้");
            }

            if (ShiftCalendar.SlotAt(blocked.ScannedAt) != slot)
            {
                return Bad("การสแกนนี้ไม่ได้อยู่ในกะนี้");
            }
        }

        var cover = new CoverAssignment
        {
            UserId = supervisor.Id,
            BuildingId = building.Id,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            AssignedById = admin.Id,
            AssignedAt = now,
        };
        return await SaveAsync(
            db, admin, supervisor, cover, blocked, now,
            $"มอบหมาย {supervisor.DisplayName} ตรวจแทนตึก {building.Code} กะ{ThaiLabels.ShiftName(slot.Shift)} {ThaiLabels.Date(slot.ShiftDate)}",
            $"มอบหมายตรวจแทนตึก {building.Code} แล้ว",
            null,
            building);
    }

    /// <summary>An active cover of the same person, place and shift. AreaId and BuildingId compare as SQL IS NULL when null.</summary>
    private static Task<bool> IsDuplicateAsync(AppDbContext db, int userId, int? areaId, int? buildingId, ShiftSlot slot) =>
        db.CoverAssignments.AnyAsync(c =>
            c.UserId == userId && c.AreaId == areaId && c.BuildingId == buildingId
            && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null);

    /// <summary>Saves the cover with its audit row and, from a Blocked Scan, closes that alert unless an Admin already did (facility-0041 rule 4).</summary>
    private static async Task<IResult> SaveAsync(
        AppDbContext db, User admin, User person, CoverAssignment cover, BlockedScan? blocked, DateTime now,
        string summary, string alertOutcome, Area? area, Building? building)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.CoverAssignments.Add(cover);
        await db.SaveChangesAsync();
        AuditTrail.Add(
            db, admin.Id, now, "COVER_ASSIGN", "cover_assignments", cover.Id, summary,
            before: null,
            after: new
            {
                cover.UserId,
                cover.AreaId,
                cover.BuildingId,
                ShiftDate = ThaiLabels.Date(cover.ShiftDate),
                Shift = cover.Shift.ToString(),
                BlockedScanId = blocked?.Id,
            });

        if (blocked is { } scan && !await db.AlertReviews.AnyAsync(r => r.AlertType == AlertType.BlockedScan && r.RecordId == scan.Id))
        {
            AdminAlertEndpoints.AddReview(
                db, AlertType.BlockedScan, scan.Id, "blocked_scans", admin.Id, now,
                $"ปิดเตือน{ThaiLabels.BlockedName(scan.Reason)} ของ {person.DisplayName} เวลา {ThaiLabels.Clock(scan.ScannedAt)}: {alertOutcome}",
                note: null);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Created(
            $"/api/admin/cover-assignments/{cover.Id}",
            new CoverAssignmentDto(
                cover.Id, person.Id, person.DisplayName, area?.Id, area?.Code, building?.Id, building?.Code,
                cover.ShiftDate, cover.Shift, DateTime.SpecifyKind(now, DateTimeKind.Utc)));
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);

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
            .Include(c => c.Building)
            .FirstOrDefaultAsync(c => c.Id == id && c.CancelledAt == null);
        if (cover is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบการมอบหมายนี้ หรือยกเลิกไปแล้ว");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        // No unique index guards against a racing duplicate, so close every identical active cover with this one.
        var twins = await db.CoverAssignments
            .Where(c => c.Id != cover.Id && c.UserId == cover.UserId && c.AreaId == cover.AreaId && c.BuildingId == cover.BuildingId
                && c.ShiftDate == cover.ShiftDate && c.Shift == cover.Shift && c.CancelledAt == null)
            .ToListAsync();
        foreach (var twin in twins.Append(cover))
        {
            twin.CancelledById = admin.Id;
            twin.CancelledAt = now;
        }
        var what = cover.Area is { } area
            ? $"การทำแทนของ {cover.User!.DisplayName} ที่ {area.Code}"
            : $"การตรวจแทนของ {cover.User!.DisplayName} ที่ตึก {cover.Building!.Code}";
        AuditTrail.Add(
            db, admin.Id, now, "COVER_CANCEL", "cover_assignments", cover.Id,
            $"ยกเลิก{what} กะ{ThaiLabels.ShiftName(cover.Shift)} {ThaiLabels.Date(cover.ShiftDate)}",
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
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุพื้นที่ วันที่ (yyyy-MM-dd) และกะ (Day หรือ Night)");
        }

        var area = await db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId && a.IsActive);
        if (area is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบพื้นที่นี้ หรือปิดใช้งานแล้ว");
        }

        if (!area.HasShift(parsedShift))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "พื้นที่นี้ไม่มีกะนี้");
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
