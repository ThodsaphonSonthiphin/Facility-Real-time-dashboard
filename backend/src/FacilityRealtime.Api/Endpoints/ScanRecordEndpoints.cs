using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

public static class ScanRecordEndpoints
{
    private const int MaxIssueTagsLength = 200;
    private const int MaxNoteLength = 1000;

    public static IEndpointRouteBuilder MapScanRecordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/scan-records", CreateAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateScanRecordRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IHubContext<ScanHub> hub,
        TimeProvider clock,
        AttendanceSettings settings)
    {
        // facility-0017: the scanner is the account in the access token, never a field in the body
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        // Owner decision 2026-10-09: only Cleaners submit; Supervisors inspect in their own flow
        if (user.Role != UserRole.Cleaner)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, "การส่งงานใช้สำหรับแม่บ้านเท่านั้น");
        }

        var issueTags = request.IssueTags is { Count: > 0 } tags ? string.Join(",", tags) : null;
        var note = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (issueTags?.Length > MaxIssueTagsLength || note?.Length > MaxNoteLength)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "แท็กปัญหาหรือหมายเหตุยาวเกินไป");
        }

        if (request.Status is not { } status || !Enum.IsDefined(status))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุสถานะการทำความสะอาด");
        }

        if (!GpsInput.TryRead(request.Latitude, request.Longitude, request.AccuracyM, out var gps, out var gpsError))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, gpsError);
        }

        var sign = await db.Signs
            .Include(s => s.ServicePoint).ThenInclude(p => p!.Area)
            .FirstOrDefaultAsync(s => s.QrToken == request.QrToken && s.ServicePointId != null);
        if (sign?.ServicePoint is not { IsActive: true, Area.IsActive: true } point)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว");
        }

        var now = clock.GetUtcNow().UtcDateTime;

        // facility-0069: the Cleaner's own shift, not the shift the clock is in
        var slot = user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, now, settings) : null;

        // facility-0041: the sign's QR Token decides the Area; another Area needs a Cover Assignment for this shift
        CoverAssignment? cover = null;
        if (point.AreaId != user.AreaId)
        {
            cover = slot is null
                ? null
                : await db.CoverAssignments.FirstOrDefaultAsync(c =>
                    c.UserId == user.Id && c.AreaId == point.AreaId && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null);
            if (cover is null)
            {
                return await BlockedScans.BlockAsync(db, user, sign, point.Area!, gps, now);
            }
        }

        // facility-0026/0050: submit from Shift-In until Shift-Out (owner decision 2026-10-09: breaks do not block)
        if (slot is null)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "ตอนนี้ไม่อยู่ในกะของคุณ");
        }

        var attendance = (await AttendanceEndpoints.LoadEventsAsync(db, user.Id, slot)).Select(a => a.EventType).ToList();
        if (!AttendanceRules.IsOnDuty(attendance))
        {
            return ApiResults.Message(
                StatusCodes.Status409Conflict,
                attendance.Contains(AttendanceEvent.ShiftOut) ? "ลงเวลาเลิกงานแล้ว ส่งงานไม่ได้" : "ลงเวลาเข้างานที่ป้ายของ Area คุณก่อน");
        }

        var facts = (await RoundFactsQuery.LoadAsync(db, [point.Id], slot))[point.Id];
        var placed = RoundPlacer.Place(facts.Windows, facts.Submissions, facts.Inspections, now);

        var record = new ScanRecord
        {
            ServicePointId = point.Id,
            SignId = sign.Id,
            UserId = user.Id,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            RoundWindowId = placed.Round?.Id,
            RoundStart = placed.Round?.Start,
            RoundEnd = placed.Round?.End,
            Placement = placed.Placement,
            LateMinutes = placed.LateMinutes,
            Status = status,
            IssueTags = issueTags,
            Note = note,
            CoverAssignmentId = cover?.Id,
            SubmittedAt = now,
        };
        record.StampGps(gps, sign);
        db.ScanRecords.Add(record);
        await db.SaveChangesAsync();

        // The Cleaner sees the point in their own shift; the Dashboard shows the shift running now (they differ only
        // in the hour before or after the Cleaner's shift)
        var mine = (await PointBoardQuery.LoadAsync(db, now, slot, areaIds: null, point.Id)).Single();
        var board = slot == ShiftCalendar.SlotAt(now) ? mine : (await PointBoardQuery.LoadAsync(db, now, point.Id)).Single();
        await hub.Clients.Group(ScanHub.AdminGroup).SendAsync("ScanRecorded", PointDtoMapper.ToDto(board));

        return Results.Created(
            $"/api/scan-records/{record.Id}",
            new ScanRecordCreatedResponse(
                record.Id, point.Id, placed.Placement, placed.LateMinutes, mine.Status.Status,
                record.DistanceM, record.WithinRadius, DateTime.SpecifyKind(now, DateTimeKind.Utc)));
    }
}
