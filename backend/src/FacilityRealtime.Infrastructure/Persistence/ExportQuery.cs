using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>One attendance event with its person and place. No coordinates: distance and verdict only (facility-0053).</summary>
public sealed record AttendanceExportRow(
    int UserId,
    string EmployeeId,
    string DisplayName,
    string BuildingCode,
    string AreaCode,
    DateOnly ShiftDate,
    Shift Shift,
    AttendanceEvent EventType,
    DateTime OccurredAtUtc,
    AttendanceSource Source,
    short? DistanceM,
    bool? WithinRadius);

/// <summary>One Scan Record with its point, sign, round and Cleaner. No coordinates.</summary>
public sealed record SubmissionExportRow(
    DateOnly ShiftDate,
    Shift Shift,
    string BuildingCode,
    string AreaCode,
    string PointName,
    string SignCode,
    int? RoundWindowId,
    TimeOnly? RoundStart,
    TimeOnly? RoundEnd,
    DateTime SubmittedAtUtc,
    Placement Placement,
    int? LateMinutes,
    CleaningStatus Status,
    string? IssueTags,
    bool IsCover,
    string EmployeeId,
    string DisplayName,
    short? DistanceM,
    bool? WithinRadius);

public sealed record ExportWindow(int Id, Shift Shift, TimeOnly Start, TimeOnly End, DateTime CreatedAtUtc);

/// <summary>An active point of an active Area with its Round Windows, for the "ไม่ได้ทำ" rows.</summary>
public sealed record ExportPoint(
    int Id, string BuildingCode, string AreaCode, ShiftPattern ShiftPattern, string PointName, string SignCode, IReadOnlyList<ExportWindow> Windows);

/// <summary>
/// facility-0053: what the Excel export reads, by Thai shift date (both ends included) and optionally one building.
/// In Infrastructure so a [MySqlFact] can run it on the Oracle provider.
/// </summary>
public static class ExportQuery
{
    public static Task<List<AttendanceExportRow>> LoadAttendanceAsync(AppDbContext db, DateOnly first, DateOnly last, int? buildingId) =>
        (from a in db.ShiftAttendances.AsNoTracking()
         join area in db.Areas on a.AreaId equals area.Id
         where a.ShiftDate >= first && a.ShiftDate <= last && (buildingId == null || area.BuildingId == buildingId)
         orderby a.ShiftDate, a.Shift, area.Code, a.UserId, a.OccurredAt
         select new AttendanceExportRow(
             a.UserId,
             a.User!.EmployeeId ?? string.Empty,
             a.User.DisplayName,
             area.Building!.Code,
             area.Code,
             a.ShiftDate,
             a.Shift,
             a.EventType,
             a.OccurredAt,
             a.Source,
             a.DistanceM,
             a.WithinRadius))
        .ToListAsync();

    public static Task<List<SubmissionExportRow>> LoadSubmissionsAsync(AppDbContext db, DateOnly first, DateOnly last, int? buildingId) =>
        db.ScanRecords.AsNoTracking()
            .Where(s => s.ShiftDate >= first && s.ShiftDate <= last
                && (buildingId == null || s.ServicePoint!.Area!.BuildingId == buildingId))
            .OrderBy(s => s.ShiftDate).ThenBy(s => s.Shift).ThenBy(s => s.SubmittedAt)
            .Select(s => new SubmissionExportRow(
                s.ShiftDate,
                s.Shift,
                s.ServicePoint!.Area!.Building!.Code,
                s.ServicePoint.Area.Code,
                s.ServicePoint.Name,
                s.Sign!.Code,
                s.RoundWindowId,
                s.RoundStart,
                s.RoundEnd,
                s.SubmittedAt,
                s.Placement,
                s.LateMinutes,
                s.Status,
                s.IssueTags,
                s.CoverAssignmentId != null,
                s.User!.EmployeeId ?? string.Empty,
                s.User.DisplayName,
                s.DistanceM,
                s.WithinRadius))
            .ToListAsync();

    /// <summary>Points as they are set up now (decision 12): active points of active Areas, by Area code then card order.</summary>
    public static async Task<List<ExportPoint>> LoadPointsAsync(AppDbContext db, int? buildingId)
    {
        var points = await db.ServicePoints.AsNoTracking()
            .Where(p => p.IsActive && p.Area!.IsActive && (buildingId == null || p.Area.BuildingId == buildingId))
            .Select(p => new { p.Id, BuildingCode = p.Area!.Building!.Code, AreaCode = p.Area.Code, p.Area.ShiftPattern, p.Name, p.SortOrder })
            .ToListAsync();
        var ids = points.Select(p => p.Id).ToList();
        var signs = await db.Signs.AsNoTracking()
            .Where(s => s.ServicePointId != null && ids.Contains(s.ServicePointId.Value))
            .Select(s => new { PointId = s.ServicePointId!.Value, s.Code })
            .ToListAsync();
        var windows = await db.PointRoundWindows.AsNoTracking().Where(w => ids.Contains(w.ServicePointId)).ToListAsync();

        return points
            .OrderBy(p => p.AreaCode, StringComparer.Ordinal).ThenBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new ExportPoint(
                p.Id,
                p.BuildingCode,
                p.AreaCode,
                p.ShiftPattern,
                p.Name,
                signs.Single(s => s.PointId == p.Id).Code,
                windows.Where(w => w.ServicePointId == p.Id)
                    .Select(w => new ExportWindow(w.Id, w.Shift, w.StartTime, w.EndTime, DateTime.SpecifyKind(w.CreatedAt, DateTimeKind.Utc)))
                    .ToList()))
            .ToList();
    }
}
