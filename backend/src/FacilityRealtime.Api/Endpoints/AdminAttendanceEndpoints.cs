using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The attendance page (wireframe D2): who is in each Area this shift (facility-0027, 0050), and the Admin's corrections (facility-0051).</summary>
public static class AdminAttendanceEndpoints
{
    public const int MaxReasonLength = 500;

    public static IEndpointRouteBuilder MapAdminAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/attendance").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/", GetBoardAsync);
        admin.MapPut("/", CorrectAsync);
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
            ? Bad(error)
            : Results.Ok(await AttendanceBoard.LoadAsync(db, slot, buildingId, now));
    }

    /// <summary>
    /// facility-0051: adds a missing event or moves an existing one, always with a reason. The row gets the "แก้โดย Admin" badge
    /// (AdminAdd or AdminEdit) and the old time stays in the audit log. A scanned event keeps its sign and GPS verdict.
    /// </summary>
    private static async Task<IResult> CorrectAsync(
        CorrectAttendanceRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider clock,
        AttendanceSettings settings,
        IHubContext<ScanHub> hub)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.ShiftDate is not { } shiftDate
            || request.Shift is not { } shift || !Enum.IsDefined(shift)
            || request.EventType is not { } eventType || !Enum.IsDefined(eventType))
        {
            return Bad("ต้องระบุวันที่ กะ และรายการที่จะเพิ่มหรือแก้");
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return Bad("ต้องกรอกเหตุผล");
        }

        if (reason.Length > MaxReasonLength)
        {
            return Bad($"เหตุผลยาวได้ไม่เกิน {MaxReasonLength} ตัวอักษร");
        }

        if (!RoundWindowRules.TryParseTime(request.Time, out var time))
        {
            return Bad("เวลาต้องเป็นแบบ HH:mm เช่น 06:58");
        }

        var cleaner = await db.Users.Include(u => u.Area)
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.Role == UserRole.Cleaner && u.IsActive);
        if (cleaner is null)
        {
            return Bad("ต้องเลือกแม่บ้านที่ใช้งานอยู่");
        }

        // facility-0069: attendance counts for the Cleaner's own regular shift
        if (cleaner.Shift != shift)
        {
            return Bad("แก้ได้เฉพาะกะประจำของแม่บ้านคนนี้");
        }

        var slot = new ShiftSlot(shiftDate, shift);
        if (AttendanceCalendar.PlaceTime(slot, time, settings) is not { } at)
        {
            return Bad($"เวลานี้อยู่นอกช่วงลงเวลาของกะนี้ ({settings.OpensMinutesBeforeShift} นาทีก่อนกะถึง {settings.ClosesMinutesAfterShift} นาทีหลังกะ)");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (at > now)
        {
            return Bad("เวลาที่ใส่ยังมาไม่ถึง");
        }

        var events = await db.ShiftAttendances
            .Where(a => a.UserId == cleaner.Id && a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift)
            .ToListAsync();
        var existing = events.FirstOrDefault(a => a.EventType == eventType);
        if (existing is null && cleaner.AreaId is null)
        {
            return Bad("แม่บ้านคนนี้ยังไม่มี Area");
        }

        var proposed = events.ToDictionary(a => a.EventType, a => a.OccurredAt);
        proposed[eventType] = at;
        if (AttendanceRules.CheckTimes(proposed) is { } orderError)
        {
            return Bad(orderError);
        }

        if (existing is not null && existing.OccurredAt == at)
        {
            // Saving the same time again changes nothing, so it logs nothing
            return Results.Ok(AttendanceBoard.ToDto(existing));
        }

        var summary = $"{(existing is null ? "เพิ่ม" : "แก้")}เวลา{ThaiLabels.EventName(eventType)}ของ {cleaner.DisplayName} "
            + $"{cleaner.Area?.Code} กะ{ThaiLabels.ShiftName(shift)} {ThaiLabels.Date(shiftDate)}: "
            + $"{(existing is null ? "–" : ThaiLabels.Clock(existing.OccurredAt))} → {ThaiLabels.Clock(at)}";
        ShiftAttendance row;
        if (existing is null)
        {
            row = new ShiftAttendance
            {
                UserId = cleaner.Id,
                AreaId = cleaner.AreaId!.Value,
                ShiftDate = slot.ShiftDate,
                Shift = slot.Shift,
                EventType = eventType,
                OccurredAt = at,
                Source = AttendanceSource.AdminAdd,
                CreatedAt = now,
            };
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.ShiftAttendances.Add(row);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // facility-0026: the Cleaner scanned the same event a moment ago and the unique index kept theirs; anything else is a real error
                db.ChangeTracker.Clear();
                var racedScan = await db.ShiftAttendances.AnyAsync(a =>
                    a.UserId == cleaner.Id && a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift && a.EventType == eventType);
                if (!racedScan)
                {
                    throw;
                }

                return ApiResults.Message(StatusCodes.Status409Conflict, "มีการลงเวลารายการนี้แล้ว โหลดหน้าใหม่แล้วลองอีกครั้ง");
            }

            AuditTrail.Add(db, admin.Id, now, "ATTENDANCE_ADD", "shift_attendances", row.Id, summary, before: null, after: Snapshot(row), reason);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        else
        {
            var before = Snapshot(existing);
            existing.OccurredAt = at;
            if (existing.Source == AttendanceSource.Scan)
            {
                existing.Source = AttendanceSource.AdminEdit;
            }

            AuditTrail.Add(db, admin.Id, now, "ATTENDANCE_EDIT", "shift_attendances", existing.Id, summary, before, Snapshot(existing), reason);
            await db.SaveChangesAsync();
            row = existing;
        }

        await AttendanceBoard.NotifyAsync(hub, row.AreaId, slot);
        return Results.Ok(AttendanceBoard.ToDto(row));
    }

    private static object Snapshot(ShiftAttendance row) => new
    {
        EventType = row.EventType.ToString(),
        OccurredAt = DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc),
        Source = row.Source.ToString(),
    };

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
