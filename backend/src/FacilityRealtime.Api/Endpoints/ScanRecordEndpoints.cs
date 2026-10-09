using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
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
        // Any logged-in account for now; the scan-rules plan adds check-in, GPS and own-Area checks (facility-0026, 0037, 0041)
        app.MapPost("/api/scan-records", CreateAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateScanRecordRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IHubContext<ScanHub> hub,
        TimeProvider clock)
    {
        // facility-0017: the scanner is the account in the access token, never a field in the body
        var user = int.TryParse(principal.FindFirstValue(AuthClaims.UserId), out var userId) ? await db.Users.FindAsync(userId) : null;
        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        var issueTags = request.IssueTags is { Count: > 0 } tags ? string.Join(",", tags) : null;
        var note = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (issueTags?.Length > MaxIssueTagsLength || note?.Length > MaxNoteLength)
        {
            return Results.BadRequest(new { message = "แท็กปัญหาหรือหมายเหตุยาวเกินไป" });
        }

        if (request.Status is not { } status || !Enum.IsDefined(status))
        {
            return Results.BadRequest(new { message = "ต้องระบุสถานะการทำความสะอาด" });
        }

        var sign = await db.Signs
            .Include(s => s.ServicePoint).ThenInclude(p => p!.Area)
            .FirstOrDefaultAsync(s => s.QrToken == request.QrToken && s.ServicePointId != null);
        if (sign?.ServicePoint is not { IsActive: true, Area.IsActive: true } point)
        {
            return Results.NotFound(new { message = "ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว" });
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var slot = ShiftCalendar.SlotAt(now);
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
            SubmittedAt = now,
        };
        db.ScanRecords.Add(record);
        await db.SaveChangesAsync();

        var row = (await PointBoardQuery.LoadAsync(db, now, point.Id)).Single();
        await hub.Clients.Group(ScanHub.AdminGroup).SendAsync("ScanRecorded", PointDtoMapper.ToDto(row));

        return Results.Created(
            $"/api/scan-records/{record.Id}",
            new ScanRecordCreatedResponse(record.Id, point.Id, placed.Placement, placed.LateMinutes, row.Status.Status, DateTime.SpecifyKind(now, DateTimeKind.Utc)));
    }
}
