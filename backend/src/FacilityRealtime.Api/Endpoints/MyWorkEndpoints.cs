using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

public static class MyWorkEndpoints
{
    public static IEndpointRouteBuilder MapMyWorkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/my-work", GetAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
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
        var slot = (user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, now, settings) : null) ?? ShiftCalendar.SlotAt(now);

        var coveredAreaIds = await db.CoverAssignments
            .Where(c => c.UserId == user.Id && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.AreaId)
            .ToListAsync();
        var areaIds = (user.AreaId is int own ? coveredAreaIds.Prepend(own) : coveredAreaIds).Distinct().ToList();

        var rows = await PointBoardQuery.LoadAsync(db, now, slot, areaIds);
        var areas = areaIds
            .Select(id => rows.Where(r => r.Point.AreaId == id).ToList())
            .Where(points => points.Count > 0)
            .Select(points =>
            {
                var area = points[0].Point.Area!;
                return new MyWorkAreaDto(area.Id, area.Code, area.Name, area.Id != user.AreaId, points.Select(PointDtoMapper.ToDto).ToList());
            })
            .ToList();

        return Results.Ok(new MyWorkDto(await AttendanceEndpoints.StateForAsync(db, user, now, settings), areas));
    }
}
