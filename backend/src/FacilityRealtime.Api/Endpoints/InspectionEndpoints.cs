using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0029, 0047 rule 7, 0048: a Supervisor scans a point's sign, sees the current round's submission and records
/// สะอาด or ไม่สะอาด. Another building, or the own building outside the own shift by the clock, is a Blocked Scan unless an
/// Admin gave a cover for it.
/// </summary>
public static class InspectionEndpoints
{
    private const string SupervisorsOnly = "การตรวจงานใช้สำหรับหัวหน้าเท่านั้น";

    public static IEndpointRouteBuilder MapInspectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/inspections/scan", ScanAsync).RequireAuthorization();
        app.MapPost("/api/inspections", CreateAsync).RequireAuthorization();
        return app;
    }

    /// <summary>A Supervisor at a point's sign who may inspect there now; Cover is set when only a cover allows it.</summary>
    private sealed record Visit(User Supervisor, Sign Sign, ServicePoint Point, ShiftSlot Slot, CoverAssignment? Cover, GpsReading Gps, DateTime Now);

    /// <summary>The Visit, or the answer to stop with: 401, 403 (another role, or a Blocked Scan it has just written), 400 or 404.</summary>
    private static async Task<(Visit? Visit, IResult? Stop)> VisitAsync(
        ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, string? qrToken, double? latitude, double? longitude, double? accuracyM)
    {
        // facility-0017: the inspector is the account in the access token
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return (null, Results.Unauthorized());
        }

        if (user.Role != UserRole.Supervisor)
        {
            return (null, ApiResults.Message(StatusCodes.Status403Forbidden, SupervisorsOnly));
        }

        if (!GpsInput.TryRead(latitude, longitude, accuracyM, out var gps, out var gpsError))
        {
            return (null, ApiResults.Message(StatusCodes.Status400BadRequest, gpsError));
        }

        var sign = await db.Signs
            .Include(s => s.ServicePoint).ThenInclude(p => p!.Area)
            .FirstOrDefaultAsync(s => s.QrToken == qrToken && s.ServicePointId != null);
        if (sign?.ServicePoint is not { IsActive: true, Area.IsActive: true } point)
        {
            return (null, ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว"));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        // facility-0048 amendment: a Supervisor has no attendance, so the shift is the clock's
        var slot = ShiftCalendar.SlotAt(now);
        var buildingId = point.Area!.BuildingId;

        CoverAssignment? cover = null;
        if (SupervisorScope.WithoutCover(user.BuildingId, user.Shift, buildingId, slot.Shift) is { } reason)
        {
            cover = await db.CoverAssignments.FirstOrDefaultAsync(c =>
                c.UserId == user.Id && c.BuildingId == buildingId && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null);
            if (cover is null)
            {
                return (null, await BlockedScans.BlockAsync(db, user, sign, point.Area, reason, gps, now));
            }
        }

        return (new Visit(user, sign, point, slot, cover, gps, now), null);
    }

    /// <summary>The scan itself: nothing is stored unless it is blocked.</summary>
    private static async Task<IResult> ScanAsync(InspectionScanRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var (visit, stop) = await VisitAsync(principal, db, clock, request.QrToken, request.Latitude, request.Longitude, request.AccuracyM);
        if (visit is null)
        {
            return stop!;
        }

        var target = await TargetAsync(db, visit);
        var board = (await PointBoardQuery.LoadAsync(db, visit.Now, visit.Point.Id)).Single();
        var submission = target.Submission is { } fact
            ? await db.ScanRecords.AsNoTracking().Include(s => s.User).SingleAsync(s => s.Id == fact.Id)
            : null;
        var verdict = Geofence.Evaluate(visit.Gps, (double?)visit.Sign.Latitude, (double?)visit.Sign.Longitude, visit.Sign.RadiusM);

        return Results.Ok(new InspectionScanDto(
            PointDtoMapper.ToDto(board),
            submission is null
                ? null
                : new InspectionSubmissionDto(
                    submission.Id,
                    DateTime.SpecifyKind(submission.SubmittedAt, DateTimeKind.Utc),
                    submission.User?.DisplayName ?? string.Empty,
                    submission.Placement,
                    submission.Status,
                    PointDtoMapper.SplitTags(submission.IssueTags),
                    submission.Note),
            RefusalText(target.Refusal),
            visit.Cover is not null,
            verdict.DistanceM,
            verdict.WithinRadius));
    }

    /// <summary>facility-0047 rule 7, in the Supervisor's (the clock's) shift.</summary>
    private static async Task<InspectionTarget> TargetAsync(AppDbContext db, Visit visit)
    {
        var facts = (await RoundFactsQuery.LoadAsync(db, [visit.Point.Id], visit.Slot))[visit.Point.Id];
        return InspectionRules.Find(visit.Point.Area!.HasShift(visit.Slot.Shift), facts.Windows, facts.Submissions, facts.Inspections, visit.Now);
    }

    private static string? RefusalText(InspectionRefusal? refusal) => refusal switch
    {
        InspectionRefusal.NothingSubmitted => "ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน",
        InspectionRefusal.AwaitingRework => "รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ",
        _ => null,
    };

    /// <summary>The verdict. Every check of the scan runs again, so a direct call is blocked the same way.</summary>
    private static async Task<IResult> CreateAsync(
        CreateInspectionRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, IHubContext<ScanHub> hub)
    {
        var (visit, stop) = await VisitAsync(principal, db, clock, request.QrToken, request.Latitude, request.Longitude, request.AccuracyM);
        if (visit is null)
        {
            return stop!;
        }

        if (request.Result is not { } result || !Enum.IsDefined(result))
        {
            return Bad("ต้องเลือกผลการตรวจ: สะอาด หรือ ไม่สะอาด");
        }

        var defect = string.IsNullOrWhiteSpace(request.Defect) ? null : request.Defect.Trim();
        if (result == InspectionResult.Rework && defect is null)
        {
            return Bad("เลือกไม่สะอาดต้องระบุข้อบกพร่อง");
        }

        if (result == InspectionResult.Passed && defect is not null)
        {
            return Bad("ข้อบกพร่องใส่ได้เฉพาะเมื่อเลือกไม่สะอาด");
        }

        if (defect is { Length: > InspectionRules.MaxDefectLength })
        {
            return Bad($"ข้อบกพร่องยาวได้ไม่เกิน {InspectionRules.MaxDefectLength} ตัวอักษร");
        }

        if (request.ScanRecordId is not { } scanRecordId)
        {
            return Bad("ต้องระบุงานที่ตรวจ (scanRecordId จากการสแกน)");
        }

        var target = await TargetAsync(db, visit);
        if (target.Refusal is { } refusal)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, RefusalText(refusal)!);
        }

        // Decision 2: never judge work the Supervisor was not shown
        if (target.Submission!.Id != scanRecordId)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "มีการส่งงานใหม่หลังจากที่คุณสแกน ให้สแกนป้ายอีกครั้ง");
        }

        var inspection = new InspectionRecord
        {
            ScanRecordId = scanRecordId,
            ServicePointId = visit.Point.Id,
            SupervisorId = visit.Supervisor.Id,
            Result = result,
            Defect = defect,
            CoverAssignmentId = visit.Cover?.Id,
            InspectedAt = visit.Now,
        };
        inspection.StampGps(visit.Gps, visit.Sign);
        db.InspectionRecords.Add(inspection);
        await db.SaveChangesAsync();

        // The Supervisor's shift is the clock's, which is also the Dashboard's
        var board = (await PointBoardQuery.LoadAsync(db, visit.Now, visit.Point.Id)).Single();
        await hub.Clients.Group(ScanHub.AdminGroup).SendAsync("InspectionRecorded", PointDtoMapper.ToDto(board));

        return Results.Created(
            $"/api/inspections/{inspection.Id}",
            new InspectionCreatedResponse(
                inspection.Id, visit.Point.Id, scanRecordId, result, board.Status.Status,
                inspection.DistanceM, inspection.WithinRadius, DateTime.SpecifyKind(visit.Now, DateTimeKind.Utc)));
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
