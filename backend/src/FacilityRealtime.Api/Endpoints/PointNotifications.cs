using System.Globalization;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0058 rule 3, 0057: a point change goes to the Admins and to the Supervisors of that point's building — its
/// active Supervisors of both shifts, and anyone covering it in the clock's shift. Nobody else.
/// </summary>
internal static class PointNotifications
{
    /// <summary><paramref name="board"/> is the point as the clock's shift sees it: the Dashboard's view and the Supervisor's.</summary>
    public static async Task SendAsync(IHubContext<ScanHub> hub, AppDbContext db, string eventName, PointBoardRow board, DateTime nowUtc)
    {
        var dto = PointDtoMapper.ToDto(board);
        await hub.Clients.Group(ScanHub.AdminGroup).SendAsync(eventName, dto);

        var buildingId = board.Point.Area!.BuildingId;
        var slot = ShiftCalendar.SlotAt(nowUtc);
        var own = await db.Users
            .Where(u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == buildingId)
            .Select(u => u.Id)
            .ToListAsync();
        var covering = await db.CoverAssignments
            .Where(c => c.BuildingId == buildingId && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Where(c => db.Users.Any(u => u.Id == c.UserId && u.IsActive && u.Role == UserRole.Supervisor))
            .Select(c => c.UserId)
            .ToListAsync();
        var userIds = own.Concat(covering).Distinct().Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
        if (userIds.Count > 0)
        {
            await hub.Clients.Users(userIds).SendAsync(eventName, dto);
        }
    }
}
