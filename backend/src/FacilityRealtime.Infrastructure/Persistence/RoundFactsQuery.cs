using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>One point's Round Windows, Scan Records and Inspection Records of one shift.</summary>
public sealed record PointShiftFacts(
    IReadOnlyList<RoundWindow> Windows,
    IReadOnlyList<SubmissionFact> Submissions,
    IReadOnlyList<InspectionFact> Inspections);

public static class RoundFactsQuery
{
    /// <summary>Three queries however many points are asked for (phase 1 ran one query per point).</summary>
    public static async Task<IReadOnlyDictionary<int, PointShiftFacts>> LoadAsync(
        AppDbContext db, IReadOnlyCollection<int> pointIds, ShiftSlot slot)
    {
        var ids = pointIds.ToList();

        var windows = await db.PointRoundWindows.AsNoTracking()
            .Where(w => ids.Contains(w.ServicePointId) && w.Shift == slot.Shift)
            .ToListAsync();

        var scans = await db.ScanRecords.AsNoTracking()
            .Where(s => ids.Contains(s.ServicePointId) && s.ShiftDate == slot.ShiftDate && s.Shift == slot.Shift)
            .Select(s => new { s.Id, s.ServicePointId, s.RoundWindowId, s.SubmittedAt })
            .ToListAsync();

        var scanIds = scans.Select(s => s.Id).ToList();
        var inspections = await db.InspectionRecords.AsNoTracking()
            .Where(i => scanIds.Contains(i.ScanRecordId))
            .Select(i => new { i.ScanRecordId, i.ServicePointId, i.Result, i.InspectedAt })
            .ToListAsync();

        return ids.ToDictionary(id => id, id => new PointShiftFacts(
            windows.Where(w => w.ServicePointId == id)
                .Select(w => RoundWindow.For(w.Id, w.StartTime, w.EndTime, slot))
                .ToList(),
            scans.Where(s => s.ServicePointId == id)
                .Select(s => new SubmissionFact(s.Id, s.RoundWindowId, s.SubmittedAt))
                .ToList(),
            inspections.Where(i => i.ServicePointId == id)
                .Select(i => new InspectionFact(i.ScanRecordId, i.Result, i.InspectedAt))
                .ToList()));
    }
}
