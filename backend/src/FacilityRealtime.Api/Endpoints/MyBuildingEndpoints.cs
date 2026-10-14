using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0057 rule 1 (wireframe M15): where a Supervisor should go next. It only points the way: an inspection still
/// needs the scan at the point. It opens outside the shift too, with CanInspect false (facility-0048).
/// </summary>
public static class MyBuildingEndpoints
{
    public static IEndpointRouteBuilder MapMyBuildingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/my-building", GetAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.Role != UserRole.Supervisor)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, "หน้าตึกของฉันใช้สำหรับหัวหน้าเท่านั้น");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var slot = ShiftCalendar.SlotAt(now);
        var coveredIds = await db.CoverAssignments
            .Where(c => c.UserId == user.Id && c.BuildingId != null && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.BuildingId!.Value)
            .ToListAsync();
        var buildingIds = (user.BuildingId is int own ? coveredIds.Prepend(own) : coveredIds).Distinct().ToList();
        var buildings = await db.Buildings.AsNoTracking()
            .Where(b => buildingIds.Contains(b.Id) && b.IsActive)
            .ToDictionaryAsync(b => b.Id);
        var areaIds = await db.Areas.Where(a => a.IsActive && buildingIds.Contains(a.BuildingId)).Select(a => a.Id).ToListAsync();
        var rows = await PointBoardQuery.LoadAsync(db, now, slot, areaIds);

        var sections = buildingIds
            .Where(buildings.ContainsKey)
            .Select(id => new MyBuildingSectionDto(
                id,
                buildings[id].Code,
                buildings[id].Name,
                id != user.BuildingId,
                coveredIds.Contains(id) || (id == user.BuildingId && user.Shift == slot.Shift),
                Sorted(rows.Where(r => r.Point.Area!.BuildingId == id)).Select(PointDtoMapper.ToDto).ToList()))
            .ToList();
        return Results.Ok(new MyBuildingDto(slot.ShiftDate, slot.Shift, sections));
    }

    /// <summary>รอตรวจ by submit time, then ต้องแก้ไข by the time it was asked, then the rest in card order (Area code, then point).</summary>
    private static IEnumerable<PointBoardRow> Sorted(IEnumerable<PointBoardRow> rows) =>
        rows.Select((row, index) => (Row: row, Index: index))
            .OrderBy(x => x.Row.Status.Status switch { PointStatus.PendingInspection => 0, PointStatus.Rework => 1, _ => 2 })
            .ThenBy(x => x.Row.Status.Status switch
            {
                PointStatus.PendingInspection => x.Row.LastScan?.SubmittedAt ?? DateTime.MaxValue,
                PointStatus.Rework => x.Row.Status.Rework?.InspectedAt ?? DateTime.MaxValue,
                _ => DateTime.MaxValue,
            })
            .ThenBy(x => x.Index)
            .Select(x => x.Row);
}
