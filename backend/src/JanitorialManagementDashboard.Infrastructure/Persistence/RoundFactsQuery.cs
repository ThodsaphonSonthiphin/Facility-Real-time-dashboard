using JanitorialManagementDashboard.Application.Rounds;
using JanitorialManagementDashboard.Application.Shifts;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence;

/// <summary>One point's Round Windows, Scan Records and Inspection Records of one shift.</summary>
public sealed record PointShiftFacts(
    IReadOnlyList<RoundWindow> Windows,
    IReadOnlyList<SubmissionFact> Submissions,
    IReadOnlyList<InspectionFact> Inspections);

public static class RoundFactsQuery
{
    /// <summary>Three queries however many points are asked for (phase 1 ran one query per point).</summary>
    public static Task<IReadOnlyDictionary<int, PointShiftFacts>> LoadAsync(
        AppDbContext db, IReadOnlyCollection<int> pointIds, ShiftSlot slot) =>
        LoadAsync(db, pointIds, slot, filterPointIds: true);

    internal static async Task<IReadOnlyDictionary<int, PointShiftFacts>> LoadAsync(
        AppDbContext db, IReadOnlyCollection<int> pointIds, ShiftSlot slot, bool filterPointIds)
    {
        var ids = pointIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, PointShiftFacts>();
        }

        var windowsQuery = db.PointRoundWindows.AsNoTracking()
            .Where(w => w.Shift == slot.Shift);
        if (filterPointIds)
        {
            windowsQuery = windowsQuery.Where(w => ids.Contains(w.ServicePointId));
        }

        var windows = await windowsQuery
            .Select(w => new { w.Id, w.ServicePointId, w.StartTime, w.EndTime })
            .ToListAsync();

        var scansQuery = db.ScanRecords.AsNoTracking()
            .Where(s => s.ShiftDate == slot.ShiftDate && s.Shift == slot.Shift);
        if (filterPointIds)
        {
            scansQuery = scansQuery.Where(s => ids.Contains(s.ServicePointId));
        }

        var scans = await scansQuery
            .Select(s => new { s.Id, s.ServicePointId, s.RoundWindowId, s.SubmittedAt })
            .ToListAsync();

        var inspections = !filterPointIds
            ? await db.InspectionRecords.AsNoTracking()
                .Where(i => i.ScanRecord!.ShiftDate == slot.ShiftDate && i.ScanRecord.Shift == slot.Shift)
                .Select(i => new { i.ScanRecordId, i.ServicePointId, i.Result, i.InspectedAt, i.Defect })
                .ToListAsync()
            : scans.Count == 0
                ? []
                : await (from i in db.InspectionRecords.AsNoTracking()
                         where scans.Select(s => s.Id).Contains(i.ScanRecordId)
                         select new { i.ScanRecordId, i.ServicePointId, i.Result, i.InspectedAt, i.Defect })
                    .ToListAsync();

        var windowsByPoint = new Dictionary<int, List<RoundWindow>>();
        foreach (var w in windows)
        {
            if (!windowsByPoint.TryGetValue(w.ServicePointId, out var list))
            {
                windowsByPoint[w.ServicePointId] = list = [];
            }
            list.Add(RoundWindow.For(w.Id, w.StartTime, w.EndTime, slot));
        }

        var scansByPoint = new Dictionary<int, List<SubmissionFact>>();
        foreach (var s in scans)
        {
            if (!scansByPoint.TryGetValue(s.ServicePointId, out var list))
            {
                scansByPoint[s.ServicePointId] = list = [];
            }
            list.Add(new SubmissionFact(s.Id, s.RoundWindowId, s.SubmittedAt));
        }

        var inspectionsByPoint = new Dictionary<int, List<InspectionFact>>();
        foreach (var i in inspections)
        {
            if (!inspectionsByPoint.TryGetValue(i.ServicePointId, out var list))
            {
                inspectionsByPoint[i.ServicePointId] = list = [];
            }
            list.Add(new InspectionFact(i.ScanRecordId, i.Result, i.InspectedAt, i.Defect));
        }

        var result = new Dictionary<int, PointShiftFacts>(ids.Count);
        foreach (var id in ids)
        {
            result[id] = new PointShiftFacts(
                windowsByPoint.TryGetValue(id, out var wList) ? wList : [],
                scansByPoint.TryGetValue(id, out var sList) ? sList : [],
                inspectionsByPoint.TryGetValue(id, out var iList) ? iList : []);
        }
        return result;
    }
}
