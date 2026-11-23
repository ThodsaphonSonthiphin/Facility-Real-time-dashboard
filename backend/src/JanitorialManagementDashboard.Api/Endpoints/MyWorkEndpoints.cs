using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

public static class MyWorkEndpoints
{
    public static IEndpointRouteBuilder MapMyWorkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/my-work", GetAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal principal, AppDbContext db, ScheduleStore store, TimeProvider clock, AttendanceSettings settings,
        CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.Role != UserRole.Cleaner)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, "หน้างานของฉันใช้สำหรับแม่บ้านเท่านั้น");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var ownSlot = await CleanerShiftResolver.ResolveSlotAsync(db, user, now, settings, ct);
        var slot = ownSlot ?? ShiftCalendar.SlotAt(now);

        // Spec §7 and §9: the Areas this person works are the slot's effective duty Area — a relief assignment, the dated
        // plan, the compatibility projection, or the destination of a confirmed midshift transfer — plus the live cover
        // areas of this shift. The account's own Area is not a separate answer any more: after a transfer the person sees the
        // destination's points, and while an arrival is still unconfirmed there is no duty Area at all.
        var duty = await EffectiveDutyReader.ReadAsync(db, store, user, slot, new DateTimeOffset(now, TimeSpan.Zero), ct);
        var coveredAreaIds = await db.CoverAssignments
            .Where(c => c.UserId == user.Id && c.AreaId != null && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.AreaId!.Value)
            .ToListAsync(ct);
        var areaIds = (duty.AreaId is int own ? coveredAreaIds.Prepend(own) : coveredAreaIds).Distinct().ToList();

        var rows = await PointBoardQuery.LoadAsync(db, now, slot, areaIds);
        var areas = areaIds
            .Select(id => rows.Where(r => r.Point.AreaId == id).ToList())
            .Where(points => points.Count > 0)
            .Select(points =>
            {
                var area = points[0].Point.Area!;
                return new MyWorkAreaDto(area.Id, area.Code, area.Name, area.Id != duty.AreaId,
                    points.Select(PointDtoMapper.ToDto).ToList());
            })
            .ToList();

        var ownShiftDate = ownSlot?.ShiftDate ?? DateOnly.FromDateTime(ThaiTime.FromUtc(now));
        var resolvedShift = ownSlot?.Shift ?? await CleanerShiftResolver.ResolveShiftAsync(db, user, ownShiftDate, ct);
        var ownShift = resolvedShift is { } regular ? OwnShiftOf(regular, settings) : null;
        var attendanceState = await AttendanceEndpoints.StateForDutyAsync(db, user, ownSlot, duty, now, ct);
        return Results.Ok(new MyWorkDto(attendanceState, areas, ownShift));
    }

    private static OwnShiftDto OwnShiftOf(Shift shift, AttendanceSettings settings)
    {
        var (opens, closes) = AttendanceCalendar.WindowClock(shift, settings);
        return new OwnShiftDto(shift, opens, closes);
    }
}
