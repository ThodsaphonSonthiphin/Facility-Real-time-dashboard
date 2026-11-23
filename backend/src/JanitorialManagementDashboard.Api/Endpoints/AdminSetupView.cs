using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Application.Rounds;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>What the Admin's Area, point and sign pages show, deactivated rows included.</summary>
internal static class AdminSetupView
{
    public static async Task<List<AreaSummaryDto>> LoadAreasAsync(AppDbContext db, int? onlyAreaId = null, DateOnly? businessDate = null)
    {
        var areas = await db.Areas.AsNoTracking()
            .Include(a => a.Building)
            .Where(a => onlyAreaId == null || a.Id == onlyAreaId)
            .OrderBy(a => a.Code)
            .ToListAsync();
        var ids = areas.Select(a => a.Id).ToList();

        List<(User User, int AreaId, Shift Shift)> assignedCleaners;
        if (businessDate is DateOnly date)
        {
            var allCleaners = await db.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Cleaner && u.IsActive)
                .ToListAsync();
            var effective = await CleanerEffectiveAssignment.ResolveForDateAsync(
                db, allCleaners, date, useFallbackWeekdayArea: true);
            var idSet = ids.ToHashSet();
            assignedCleaners = allCleaners
                .Where(u => effective.TryGetValue(u.Id, out var state)
                    && state.AreaId is int aId && idSet.Contains(aId)
                    && state.Shift is Shift)
                .Select(u => (u, effective[u.Id].AreaId!.Value, effective[u.Id].Shift!.Value))
                .ToList();
        }
        else
        {
            var legacy = await db.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.AreaId != null && u.Shift != null && ids.Contains(u.AreaId.Value))
                .ToListAsync();
            assignedCleaners = legacy.Select(u => (u, u.AreaId!.Value, u.Shift!.Value)).ToList();
        }

        var activePointAreaIds = await db.ServicePoints.AsNoTracking()
            .Where(p => p.IsActive && ids.Contains(p.AreaId))
            .Select(p => p.AreaId)
            .ToListAsync();
        var signs = await db.Signs.AsNoTracking()
            .Where(s => ids.Contains(s.AreaId) && (s.ServicePointId == null || s.ServicePoint!.IsActive))
            .Select(s => new { s.AreaId, s.LocationSource })
            .ToListAsync();

        return areas.Select(a => new AreaSummaryDto(
                a.Id,
                a.Code,
                a.Name,
                a.BuildingId,
                a.Building!.Code,
                a.ShiftPattern,
                a.IsActive,
                CleanerOf(assignedCleaners, a.Id, Shift.Day),
                CleanerOf(assignedCleaners, a.Id, Shift.Night),
                activePointAreaIds.Count(id => id == a.Id),
                signs.Count(s => s.AreaId == a.Id && s.LocationSource != LocationSource.Site)))
            .ToList();
    }

    public static async Task<AreaDetailDto?> LoadAreaAsync(AppDbContext db, int areaId, DateOnly? businessDate = null)
    {
        var summary = (await LoadAreasAsync(db, areaId, businessDate)).SingleOrDefault();
        if (summary is null)
        {
            return null;
        }

        var signs = await db.Signs.AsNoTracking().Where(s => s.AreaId == areaId).ToListAsync();
        var points = await db.ServicePoints.AsNoTracking()
            .Where(p => p.AreaId == areaId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();
        var pointIds = points.Select(p => p.Id).ToList();
        var windows = await db.PointRoundWindows.AsNoTracking().Where(w => pointIds.Contains(w.ServicePointId)).ToListAsync();

        return new AreaDetailDto(
            summary,
            ToDto(signs.Single(s => s.ServicePointId == null)),
            points.Select(p => ToDto(p, signs.Single(s => s.ServicePointId == p.Id), windows)).ToList());
    }

    public static AdminSignDto ToDto(Sign sign) => new(
        sign.Id,
        sign.Code,
        sign.QrToken,
        Utc(sign.QrIssuedAt),
        sign.Latitude,
        sign.Longitude,
        sign.LocationAccuracyM,
        sign.LocationSource,
        sign.LocatedAt is { } locatedAt ? Utc(locatedAt) : (DateTime?)null,
        sign.RadiusM);

    /// <summary>Windows in the order the shift meets them: Day before Night, a night 03:00 after 20:00.</summary>
    public static AdminPointDto ToDto(ServicePoint point, Sign sign, IEnumerable<PointRoundWindow> windows) => new(
        point.Id,
        point.Name,
        point.SortOrder,
        point.IsActive,
        ToDto(sign),
        windows.Where(w => w.ServicePointId == point.Id)
            .OrderBy(w => w.Shift)
            .ThenBy(w => RoundWindowRules.MinutesIntoShift(w.Shift, w.StartTime))
            .Select(w => new AdminRoundWindowDto(w.Id, w.Shift, RoundWindowRules.Format(w.StartTime), RoundWindowRules.Format(w.EndTime)))
            .ToList());

    private static AssignedCleanerDto? CleanerOf(List<(User User, int AreaId, Shift Shift)> cleaners, int areaId, Shift shift) =>
        cleaners.Where(c => c.AreaId == areaId && c.Shift == shift)
            .Select(c => new AssignedCleanerDto(c.User.Id, c.User.EmployeeId ?? string.Empty, c.User.DisplayName))
            .FirstOrDefault();

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z".</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);

    public static async Task<AdminPointDto?> LoadPointAsync(AppDbContext db, int pointId)
    {
        var point = await db.ServicePoints.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pointId);
        if (point is null)
        {
            return null;
        }

        var sign = await db.Signs.AsNoTracking().SingleAsync(s => s.ServicePointId == pointId);
        var windows = await db.PointRoundWindows.AsNoTracking().Where(w => w.ServicePointId == pointId).ToListAsync();
        return ToDto(point, sign, windows);
    }
}
