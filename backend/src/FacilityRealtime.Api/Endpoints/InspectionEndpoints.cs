using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
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
}
