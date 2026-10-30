using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>One Inspection Record with its point, round, Supervisor and GPS verdict. No coordinates (facility-0053).</summary>
public sealed record InspectionExportRow(
    long Id,
    long ScanRecordId,
    int PointId,
    DateOnly ShiftDate,
    Shift Shift,
    string BuildingCode,
    string AreaCode,
    string PointName,
    string SignCode,
    TimeOnly? RoundStart,
    TimeOnly? RoundEnd,
    DateTime InspectedAtUtc,
    InspectionResult Result,
    string? Defect,
    bool IsCover,
    int SupervisorId,
    string EmployeeId,
    string DisplayName,
    short? DistanceM,
    bool? WithinRadius);

/// <summary>A point, active or not, with where it is. Counted: an active point of an active Area. WorksNight: its Area has a night shift.</summary>
public sealed record PointPlace(
    int Id, int BuildingId, string BuildingCode, string AreaCode, string PointName, string SignCode, bool Counted, bool WorksNight);

/// <summary>
/// What the export's inspection sheet and the Inspection Summary read, by Thai shift date (both ends included) and
/// optionally the point's building. In Infrastructure so a [MySqlFact] can run it on the Oracle provider.
/// </summary>
public static class InspectionHistoryQuery
{
    /// <summary>Scan Records placed in a round (not Off-Round), with the round copied onto them.</summary>
    public static async Task<List<RoundSubmission>> LoadRoundSubmissionsAsync(AppDbContext db, DateOnly first, DateOnly last, int? buildingId, IReadOnlyCollection<int>? buildingIds = null)
    {
        var rows = await db.ScanRecords.AsNoTracking()
            .Where(s => s.ShiftDate >= first && s.ShiftDate <= last && s.Placement != Placement.OffRound
                && s.RoundStart != null && s.RoundEnd != null
                && (buildingId == null || s.ServicePoint!.Area!.BuildingId == buildingId)
                && (buildingIds == null || buildingIds.Contains(s.ServicePoint!.Area!.BuildingId)))
            .Select(s => new { s.Id, s.ServicePointId, s.ShiftDate, s.Shift, s.RoundStart, s.RoundEnd, s.SubmittedAt })
            .ToListAsync();
        return rows
            .Select(s => new RoundSubmission(
                s.Id,
                s.ServicePointId,
                new ShiftSlot(s.ShiftDate, s.Shift),
                s.RoundStart!.Value,
                s.RoundEnd!.Value,
                DateTime.SpecifyKind(s.SubmittedAt, DateTimeKind.Utc)))
            .ToList();
    }

    /// <summary>Inspections of the Scan Records in the range, oldest first.</summary>
    public static Task<List<InspectionExportRow>> LoadInspectionsAsync(AppDbContext db, DateOnly first, DateOnly last, int? buildingId, IReadOnlyCollection<int>? buildingIds = null) =>
        db.InspectionRecords.AsNoTracking()
            .Where(i => i.ScanRecord!.ShiftDate >= first && i.ScanRecord.ShiftDate <= last
                && (buildingId == null || i.ScanRecord.ServicePoint!.Area!.BuildingId == buildingId)
                && (buildingIds == null || buildingIds.Contains(i.ScanRecord.ServicePoint!.Area!.BuildingId)))
            .OrderBy(i => i.InspectedAt).ThenBy(i => i.Id)
            .Select(i => new InspectionExportRow(
                i.Id,
                i.ScanRecordId,
                i.ServicePointId,
                i.ScanRecord!.ShiftDate,
                i.ScanRecord.Shift,
                i.ScanRecord.ServicePoint!.Area!.Building!.Code,
                i.ScanRecord.ServicePoint.Area.Code,
                i.ScanRecord.ServicePoint.Name,
                i.ScanRecord.Sign!.Code,
                i.ScanRecord.RoundStart,
                i.ScanRecord.RoundEnd,
                i.InspectedAt,
                i.Result,
                i.Defect,
                i.CoverAssignmentId != null,
                i.SupervisorId,
                i.Supervisor!.EmployeeId ?? string.Empty,
                i.Supervisor.DisplayName,
                i.DistanceM,
                i.WithinRadius))
            .ToListAsync();

    /// <summary>Every point of the building (or of all), active or not, so deactivated points still name their history.</summary>
    public static async Task<List<PointPlace>> LoadPointPlacesAsync(AppDbContext db, int? buildingId, IReadOnlyCollection<int>? buildingIds = null)
    {
        var points = await db.ServicePoints.AsNoTracking()
            .Where(p => (buildingId == null || p.Area!.BuildingId == buildingId)
                && (buildingIds == null || buildingIds.Contains(p.Area!.BuildingId)))
            .Select(p => new
            {
                p.Id,
                p.Area!.BuildingId,
                BuildingCode = p.Area.Building!.Code,
                AreaCode = p.Area.Code,
                p.Name,
                Counted = p.IsActive && p.Area.IsActive,
                p.Area.ShiftPattern,
            })
            .ToListAsync();
        var ids = points.Select(p => p.Id).ToList();
        var signCodes = await db.Signs.AsNoTracking()
            .Where(s => s.ServicePointId != null && ids.Contains(s.ServicePointId.Value))
            .ToDictionaryAsync(s => s.ServicePointId!.Value, s => s.Code);

        return points
            .Select(p => new PointPlace(
                p.Id, p.BuildingId, p.BuildingCode, p.AreaCode, p.Name, signCodes.GetValueOrDefault(p.Id, string.Empty), p.Counted, p.ShiftPattern == ShiftPattern.DayAndNight))
            .ToList();
    }
}
