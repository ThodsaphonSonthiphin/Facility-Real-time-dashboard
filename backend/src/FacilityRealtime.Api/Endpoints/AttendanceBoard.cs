using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// The attendance page (wireframe D2, facility-0027/0050): every active Area that works the shift, its regular
/// Cleaner's events and the covers assigned to it. Areas with nobody come first.
/// </summary>
internal static class AttendanceBoard
{
    public static async Task<AttendanceBoardDto> LoadAsync(AppDbContext db, ShiftSlot slot, int? buildingId, DateTime nowUtc)
    {
        // A day-only Area has no night shift, so it is not "ต้องมีคน" at night
        var areas = (await db.Areas.AsNoTracking()
                .Include(a => a.Building)
                .Where(a => a.IsActive && (buildingId == null || a.BuildingId == buildingId))
                .ToListAsync())
            .Where(a => a.HasShift(slot.Shift))
            .ToList();
        var areaIds = areas.Select(a => a.Id).ToList();

        var cleaners = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.Shift == slot.Shift
                && u.AreaId != null && areaIds.Contains(u.AreaId.Value))
            .ToListAsync();
        var covers = await db.CoverAssignments.AsNoTracking()
            .Include(c => c.User!).ThenInclude(u => u.Area)
            .Where(c => c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null && areaIds.Contains(c.AreaId))
            .OrderBy(c => c.AssignedAt)
            .ToListAsync();
        var userIds = cleaners.Select(u => u.Id).Concat(covers.Select(c => c.UserId)).Distinct().ToList();
        var events = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift && userIds.Contains(a.UserId))
            .OrderBy(a => a.OccurredAt)
            .ToListAsync();

        var rows = areas
            .Select(area => Row(area, cleaners.FirstOrDefault(u => u.AreaId == area.Id), covers.Where(c => c.AreaId == area.Id).ToList(), events))
            .OrderBy(r => r.State == AreaAttendanceState.Empty ? 0 : 1)
            .ThenBy(r => r.AreaCode)
            .ToList();

        return new AttendanceBoardDto(
            slot.ShiftDate,
            slot.Shift,
            nowUtc >= ShiftCalendar.EndUtc(slot),
            new BoardSummaryDto(
                rows.Count,
                rows.Count(r => r.State is AreaAttendanceState.Working or AreaAttendanceState.OnBreak or AreaAttendanceState.Done),
                rows.Count(r => r.State == AreaAttendanceState.Empty),
                rows.Count(r => r.State == AreaAttendanceState.Covered)),
            rows);
    }

    public static BoardEventDto ToDto(ShiftAttendance a) =>
        new(a.Id, a.EventType, Utc(a.OccurredAt), a.Source, a.WithinRadius, a.DistanceM);

    /// <summary>facility-0027 rule 5: the attendance page reloads that board; it also polls every 30 s (facility-0061).</summary>
    public static Task NotifyAsync(IHubContext<ScanHub> hub, int areaId, ShiftSlot slot) =>
        hub.Clients.Group(ScanHub.AdminGroup).SendAsync("AttendanceRecorded", new AttendanceChangedDto(areaId, slot.ShiftDate, slot.Shift));

    private static BoardRowDto Row(Area area, User? cleaner, List<CoverAssignment> covers, List<ShiftAttendance> events)
    {
        var own = cleaner is null ? new List<ShiftAttendance>() : events.Where(e => e.UserId == cleaner.Id).ToList();
        return new BoardRowDto(
            area.Id,
            area.Code,
            area.Name,
            area.Building!.Code,
            cleaner is null ? null : new AssignedCleanerDto(cleaner.Id, cleaner.EmployeeId ?? string.Empty, cleaner.DisplayName),
            AreaAttendance.StateOf(own.Select(e => e.EventType).ToList(), covers.Count > 0),
            own.Select(ToDto).ToList(),
            covers.Select(c => new BoardCoverDto(
                    c.Id,
                    c.UserId,
                    c.User!.EmployeeId ?? string.Empty,
                    c.User.DisplayName,
                    c.User.Area?.Code,
                    Utc(c.AssignedAt),
                    events.Where(e => e.UserId == c.UserId && e.EventType == AttendanceEvent.ShiftIn)
                        .Select(e => (DateTime?)Utc(e.OccurredAt))
                        .FirstOrDefault()))
                .ToList());
    }

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z".</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);
}
