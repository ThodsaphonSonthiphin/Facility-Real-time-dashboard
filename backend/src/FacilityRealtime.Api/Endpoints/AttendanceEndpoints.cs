using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

public static class AttendanceEndpoints
{
    private const string CleanersOnly = "การลงเวลาใช้สำหรับแม่บ้านเท่านั้น";

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/attendance", RecordAsync).RequireAuthorization();
        app.MapGet("/api/attendance/me", GetMineAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> RecordAsync(
        RecordAttendanceRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.Role != UserRole.Cleaner)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, CleanersOnly);
        }

        if (request.EventType is not { } eventType || !Enum.IsDefined(eventType))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุประเภทการลงเวลา");
        }

        if (!GpsInput.TryRead(request.Latitude, request.Longitude, request.AccuracyM, out var gps, out var gpsError))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, gpsError);
        }

        var sign = await db.Signs.Include(s => s.Area)
            .FirstOrDefaultAsync(s => s.QrToken == request.QrToken && s.ServicePointId == null);
        if (sign?.Area is not { IsActive: true } area)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบป้ายลงเวลานี้");
        }

        var now = clock.GetUtcNow().UtcDateTime;

        // facility-0041: decided by the sign's QR Token; a Cleaner covering another Area still checks in at their own sign
        if (area.Id != user.AreaId)
        {
            return await BlockedScans.BlockAsync(db, user, sign, area, gps, now);
        }

        var slot = user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, now, settings) : null;
        if (slot is null || !area.HasShift(slot.Shift))
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ");
        }

        var existing = await LoadEventsAsync(db, user.Id, slot);
        var recorded = existing.Select(a => a.EventType).ToList();
        if (recorded.Contains(eventType))
        {
            return Results.Ok(State(slot, existing, AttendanceOutcome.AlreadyRecorded));
        }

        var latest = existing.MaxBy(a => a.OccurredAt);
        if (latest is not null && now - latest.OccurredAt < TimeSpan.FromMinutes(settings.RepeatIgnoreMinutes))
        {
            return Results.Ok(State(slot, existing, AttendanceOutcome.TooSoon));
        }

        var allowed = AttendanceRules.NextAllowed(recorded);
        if (!allowed.Contains(eventType))
        {
            return Results.Json(new AttendanceRefusedDto("ลงเวลาไม่ตรงลำดับ", allowed), statusCode: StatusCodes.Status409Conflict);
        }

        var entry = new ShiftAttendance
        {
            UserId = user.Id,
            AreaId = area.Id,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            EventType = eventType,
            OccurredAt = now,
            Source = AttendanceSource.Scan,
            SignId = sign.Id,
            CreatedAt = now,
        };
        entry.StampGps(gps, sign);
        db.ShiftAttendances.Add(entry);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // facility-0026: two presses raced and the unique index kept the first; anything else is a real error
            db.ChangeTracker.Clear();
            var recordedNow = await LoadEventsAsync(db, user.Id, slot);
            if (!recordedNow.Any(a => a.EventType == eventType))
            {
                throw;
            }

            return Results.Ok(State(slot, recordedNow, AttendanceOutcome.AlreadyRecorded));
        }

        return Results.Ok(State(slot, [.. existing, entry], AttendanceOutcome.Recorded));
    }

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        return user.Role != UserRole.Cleaner
            ? ApiResults.Message(StatusCodes.Status403Forbidden, CleanersOnly)
            : Results.Ok(await StateForAsync(db, user, clock.GetUtcNow().UtcDateTime, settings));
    }

    internal static async Task<AttendanceStateDto> StateForAsync(AppDbContext db, User user, DateTime nowUtc, AttendanceSettings settings)
    {
        var slot = user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, nowUtc, settings) : null;
        return slot is null
            ? new AttendanceStateDto(null, null, [], [], null)
            : State(slot, await LoadEventsAsync(db, user.Id, slot), null);
    }

    internal static Task<List<ShiftAttendance>> LoadEventsAsync(AppDbContext db, int userId, ShiftSlot slot) =>
        db.ShiftAttendances.AsNoTracking()
            .Where(a => a.UserId == userId && a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync();

    private static AttendanceStateDto State(ShiftSlot slot, IReadOnlyList<ShiftAttendance> events, AttendanceOutcome? outcome) =>
        new(
            slot.ShiftDate,
            slot.Shift,
            events.OrderBy(a => a.OccurredAt)
                .Select(a => new AttendanceEntryDto(a.EventType, DateTime.SpecifyKind(a.OccurredAt, DateTimeKind.Utc), a.Source, a.WithinRadius))
                .ToList(),
            AttendanceRules.NextAllowed(events.Select(a => a.EventType).ToList()),
            outcome);
}
