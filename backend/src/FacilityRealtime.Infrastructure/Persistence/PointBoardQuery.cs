using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>One card: the point (with Area and building), its Point Status, and its latest Scan Record in any shift (with the cleaner).</summary>
public sealed record PointBoardRow(ServicePoint Point, PointStatusResult Status, ScanRecord? LastScan);

public static class PointBoardQuery
{
    /// <summary>The Dashboard: every active point as seen from the shift running now.</summary>
    public static Task<IReadOnlyList<PointBoardRow>> LoadAsync(AppDbContext db, DateTime nowUtc, int? servicePointId = null) =>
        LoadAsync(db, nowUtc, ShiftCalendar.SlotAt(nowUtc), areaIds: null, servicePointId);

    /// <summary>Points as seen from <paramref name="slot"/>; <paramref name="areaIds"/> narrows to those Areas (My Work).</summary>
    public static async Task<IReadOnlyList<PointBoardRow>> LoadAsync(
        AppDbContext db, DateTime nowUtc, ShiftSlot slot, IReadOnlyCollection<int>? areaIds, int? servicePointId = null)
    {
        var query = db.ServicePoints.AsNoTracking()
            .Include(p => p.Area!).ThenInclude(a => a.Building)
            .Where(p => p.IsActive);
        if (servicePointId is int onlyId)
        {
            query = query.Where(p => p.Id == onlyId);
        }

        if (areaIds is not null)
        {
            var onlyAreas = areaIds.ToList();
            query = query.Where(p => onlyAreas.Contains(p.AreaId));
        }

        var points = await query.OrderBy(p => p.Area!.Code).ThenBy(p => p.SortOrder).ToListAsync();
        var ids = points.Select(p => p.Id).ToList();
        var facts = await RoundFactsQuery.LoadAsync(db, ids, slot);

        // facility-0046: the issue tag follows the point's latest Scan Record, whatever shift it was in
        var latestIds = db.ScanRecords
            .Where(x => ids.Contains(x.ServicePointId))
            .GroupBy(x => x.ServicePointId)
            .Select(g => g.Max(x => x.Id));
        var lastScans = await db.ScanRecords.AsNoTracking()
            .Include(s => s.User)
            .Where(s => latestIds.Contains(s.Id))
            .ToListAsync();

        return points.Select(point =>
        {
            var pointFacts = facts[point.Id];
            var status = PointStatusCalculator.Calculate(
                point.Area!.HasShift(slot.Shift), pointFacts.Windows, pointFacts.Submissions, pointFacts.Inspections, nowUtc);
            return new PointBoardRow(point, status, lastScans.FirstOrDefault(s => s.ServicePointId == point.Id));
        }).ToList();
    }
}
