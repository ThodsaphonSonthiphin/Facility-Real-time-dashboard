using JanitorialManagementDashboard.Application.Rounds;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence;

/// <summary>One card: the point (with Area and building), its Point Status, and its latest Scan Record in any shift (with the cleaner).</summary>
public sealed record PointBoardRow(ServicePoint Point, PointStatusResult Status, ScanRecord? LastScan);

public static class PointBoardQuery
{
    /// <summary>The Dashboard: every active point of an active Area, as seen from the shift running now.</summary>
    public static Task<IReadOnlyList<PointBoardRow>> LoadAsync(AppDbContext db, DateTime nowUtc, int? servicePointId = null) =>
        LoadAsync(db, nowUtc, ShiftCalendar.SlotAt(nowUtc), areaIds: null, servicePointId);

    /// <summary>Points as seen from <paramref name="slot"/>; <paramref name="areaIds"/> narrows to those Areas (My Work).</summary>
    public static async Task<IReadOnlyList<PointBoardRow>> LoadAsync(
        AppDbContext db, DateTime nowUtc, ShiftSlot slot, IReadOnlyCollection<int>? areaIds, int? servicePointId = null)
    {
        var query = db.ServicePoints.AsNoTracking()
            .Include(p => p.Area!).ThenInclude(a => a.Building)
            .Where(p => p.IsActive && p.Area!.IsActive);
        if (servicePointId is int onlyId)
        {
            query = query.Where(p => p.Id == onlyId);
        }

        if (areaIds is not null)
        {
            var onlyAreas = areaIds.ToList();
            query = query.Where(p => onlyAreas.Contains(p.AreaId));
        }

        var rawPoints = await query
            .OrderBy(p => p.Area!.Code)
            .ThenBy(p => p.SortOrder)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.AreaId,
                p.SortOrder,
                AreaCode = p.Area!.Code,
                AreaName = p.Area.Name,
                p.Area.BuildingId,
                BuildingCode = p.Area.Building!.Code,
                p.Area.ShiftPattern,
            })
            .ToListAsync();
        if (rawPoints.Count == 0)
        {
            return [];
        }

        var buildingCache = new Dictionary<int, Building>();
        var areaCache = new Dictionary<int, Area>();
        var points = new List<ServicePoint>(rawPoints.Count);
        foreach (var p in rawPoints)
        {
            if (!buildingCache.TryGetValue(p.BuildingId, out var building))
            {
                buildingCache[p.BuildingId] = building = new Building { Id = p.BuildingId, Code = p.BuildingCode };
            }
            if (!areaCache.TryGetValue(p.AreaId, out var area))
            {
                areaCache[p.AreaId] = area = new Area
                {
                    Id = p.AreaId,
                    Code = p.AreaCode,
                    Name = p.AreaName,
                    BuildingId = p.BuildingId,
                    ShiftPattern = p.ShiftPattern,
                    IsActive = true,
                    Building = building,
                };
            }
            points.Add(new ServicePoint
            {
                Id = p.Id,
                Name = p.Name,
                AreaId = p.AreaId,
                SortOrder = p.SortOrder,
                IsActive = true,
                Area = area,
            });
        }

        var allActivePoints = areaIds is null && servicePointId is null;
        var ids = points.Select(p => p.Id).ToList();
        var facts = await RoundFactsQuery.LoadAsync(db, ids, slot, filterPointIds: !allActivePoints);

        // facility-0046: the issue tag follows the point's latest Scan Record, whatever shift it was in.
        // Join against the per-point max scan IDs in a single MySQL primary-key lookup instead of sending 1,200 IDs in SQL IN (...).
        var maxIdsQuery = allActivePoints
            ? db.ScanRecords.AsNoTracking().GroupBy(x => x.ServicePointId).Select(g => g.Max(x => x.Id))
            : db.ScanRecords.AsNoTracking().Where(x => ids.Contains(x.ServicePointId)).GroupBy(x => x.ServicePointId).Select(g => g.Max(x => x.Id));
        var lastScans = (await db.ScanRecords.AsNoTracking()
            .Join(
                maxIdsQuery,
                s => s.Id,
                maxId => maxId,
                (s, _) => new
                {
                    s.Id,
                    s.ServicePointId,
                    s.SubmittedAt,
                    s.Placement,
                    s.Status,
                    s.IssueTags,
                    s.Note,
                    CleanerName = s.User != null ? s.User.DisplayName : string.Empty,
                })
            .ToListAsync())
            .Select(s => new ScanRecord
            {
                Id = s.Id,
                ServicePointId = s.ServicePointId,
                SubmittedAt = s.SubmittedAt,
                Placement = s.Placement,
                Status = s.Status,
                IssueTags = s.IssueTags,
                Note = s.Note,
                User = new User { DisplayName = s.CleanerName },
            })
            .ToDictionary(s => s.ServicePointId);

        return points.Select(point =>
        {
            var pointFacts = facts[point.Id];
            var status = PointStatusCalculator.Calculate(
                point.Area!.HasShift(slot.Shift), pointFacts.Windows, pointFacts.Submissions, pointFacts.Inspections, nowUtc);
            return new PointBoardRow(point, status, lastScans.GetValueOrDefault(point.Id));
        }).ToList();
    }
}
