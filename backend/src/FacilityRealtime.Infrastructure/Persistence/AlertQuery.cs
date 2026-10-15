using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>
/// One line of the alert list (wireframe D3). Area is where it happened: the scanned sign's Area, or for a missing
/// event the Area the Cleaner checked in to. CoverSlot is set when the Admin can still give this person a cover there —
/// that Area for a Cleaner, that Area's building for a Supervisor. BlockReason is set on a Blocked Scan.
/// </summary>
public sealed record AlertRow(
    AlertKind Kind,
    long RecordId,
    DateTime At,
    User Person,
    Area Area,
    Sign? Sign,
    short? DistanceM,
    short? AccuracyM,
    IReadOnlyList<AttendanceEvent> MissingEvents,
    AlertReview? Review,
    ShiftSlot? CoverSlot,
    BlockReason? BlockReason = null);

public static class AlertQuery
{
    /// <summary>
    /// Every alert whose time falls in [fromUtc, toUtc), newest first. Sign comes with its Area (and building) and point;
    /// Person with their Area.
    /// </summary>
    public static async Task<IReadOnlyList<AlertRow>> LoadAsync(
        AppDbContext db, DateTime fromUtc, DateTime toUtc, DateTime nowUtc, AttendanceSettings settings)
    {
        // facility-0035/0037: recorded but flagged, for the Admin to talk to the person
        var attendanceFlags = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.WithinRadius == false && a.SignId != null && a.OccurredAt >= fromUtc && a.OccurredAt < toUtc)
            .ToListAsync();
        var submissionFlags = await db.ScanRecords.AsNoTracking()
            .Where(s => s.WithinRadius == false && s.SubmittedAt >= fromUtc && s.SubmittedAt < toUtc)
            .ToListAsync();
        var inspectionFlags = await db.InspectionRecords.AsNoTracking()
            .Where(i => i.WithinRadius == false && i.InspectedAt >= fromUtc && i.InspectedAt < toUtc)
            .ToListAsync();
        var blocked = await db.BlockedScans.AsNoTracking()
            .Where(b => b.ScannedAt >= fromUtc && b.ScannedAt < toUtc)
            .ToListAsync();

        // facility-0051 rule 2: shifts that ended in the range, of Cleaners who checked in but did not finish scanning.
        // A night shift ends the morning after its ShiftDate, so look one day earlier.
        var firstDate = DateOnly.FromDateTime(ThaiTime.FromUtc(fromUtc)).AddDays(-1);
        var lastDate = DateOnly.FromDateTime(ThaiTime.FromUtc(toUtc));
        var shiftEvents = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.ShiftDate >= firstDate && a.ShiftDate <= lastDate)
            .ToListAsync();
        var missing = shiftEvents
            .GroupBy(a => (a.UserId, Slot: new ShiftSlot(a.ShiftDate, a.Shift)))
            .Select(g => (
                g.Key.UserId,
                End: ShiftCalendar.EndUtc(g.Key.Slot),
                ShiftIn: g.FirstOrDefault(a => a.EventType == AttendanceEvent.ShiftIn),
                Missing: AttendanceRules.MissingAtShiftEnd(g.Select(a => a.EventType).ToList())))
            .Where(s => s.End <= nowUtc && s.End >= fromUtc && s.End < toUtc && s.ShiftIn is not null && s.Missing.Count > 0)
            .ToList();

        // An inspection names its point, not its sign; each point has exactly one sign
        var flaggedPointIds = inspectionFlags.Select(i => i.ServicePointId).Distinct().ToList();
        var signIds = attendanceFlags.Select(a => a.SignId!.Value)
            .Concat(submissionFlags.Select(s => s.SignId))
            .Concat(blocked.Select(b => b.SignId))
            .Distinct()
            .ToList();
        var loadedSigns = await db.Signs.AsNoTracking()
            .Include(s => s.Area!).ThenInclude(a => a.Building)
            .Include(s => s.ServicePoint)
            .Where(s => signIds.Contains(s.Id) || (s.ServicePointId != null && flaggedPointIds.Contains(s.ServicePointId.Value)))
            .ToListAsync();
        var signs = loadedSigns.ToDictionary(s => s.Id);
        var signOfPoint = loadedSigns.Where(s => s.ServicePointId != null).ToDictionary(s => s.ServicePointId!.Value);

        var userIds = attendanceFlags.Select(a => a.UserId)
            .Concat(submissionFlags.Select(s => s.UserId))
            .Concat(inspectionFlags.Select(i => i.SupervisorId))
            .Concat(blocked.Select(b => b.UserId))
            .Concat(missing.Select(m => m.UserId))
            .Distinct()
            .ToList();
        var users = await db.Users.AsNoTracking()
            .Include(u => u.Area)
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);
        var missingAreaIds = missing.Select(m => m.ShiftIn!.AreaId).Distinct().ToList();
        var areas = await db.Areas.AsNoTracking().Where(a => missingAreaIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id);

        var recordIds = attendanceFlags.Select(a => a.Id)
            .Concat(submissionFlags.Select(s => s.Id))
            .Concat(inspectionFlags.Select(i => i.Id))
            .Concat(blocked.Select(b => b.Id))
            .Distinct()
            .ToList();
        var reviews = await db.AlertReviews.AsNoTracking().Where(r => recordIds.Contains(r.RecordId)).ToListAsync();
        var blockedUserIds = blocked.Select(b => b.UserId).Distinct().ToList();
        var activeCovers = await db.CoverAssignments.AsNoTracking()
            .Where(c => c.CancelledAt == null && c.ShiftDate >= firstDate && blockedUserIds.Contains(c.UserId))
            .ToListAsync();

        AlertReview? ReviewOf(AlertType type, long id) => reviews.FirstOrDefault(r => r.AlertType == type && r.RecordId == id);

        // facility-0041 rule 4, 0048 rule 2: offer a cover for the scan's own shift while that shift has not ended and none exists yet
        ShiftSlot? CoverSlotFor(BlockedScan scan)
        {
            var user = users[scan.UserId];
            var area = signs[scan.SignId].Area!;
            return user.Role == UserRole.Supervisor ? SupervisorCoverSlot(user, area, scan) : CleanerCoverSlot(user, area, scan);
        }

        // Not for a Cleaner without an Area (no sign to check in at, decision 10); HasShift is false for a deactivated Area.
        ShiftSlot? CleanerCoverSlot(User user, Area area, BlockedScan scan)
        {
            if (user is not { Role: UserRole.Cleaner, IsActive: true, Shift: { } shift } || user.AreaId is not int ownAreaId || ownAreaId == area.Id || !area.HasShift(shift))
            {
                return null;
            }

            var slot = AttendanceCalendar.SlotFor(shift, scan.ScannedAt, settings);
            return slot is not null
                && ShiftCalendar.EndUtc(slot) > nowUtc
                && !activeCovers.Any(c => c.UserId == user.Id && c.AreaId == area.Id && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift)
                    ? slot
                    : null;
        }

        // A Supervisor's shift is the clock's at the scan; the cover is for the sign's building (plan 6 decision 7)
        ShiftSlot? SupervisorCoverSlot(User user, Area area, BlockedScan scan)
        {
            if (!user.IsActive || area.Building is not { IsActive: true })
            {
                return null;
            }

            var slot = ShiftCalendar.SlotAt(scan.ScannedAt);
            return ShiftCalendar.EndUtc(slot) > nowUtc
                && SupervisorScope.WithoutCover(user.BuildingId, user.Shift, area.BuildingId, slot.Shift) is not null
                && !activeCovers.Any(c => c.UserId == user.Id && c.BuildingId == area.BuildingId && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift)
                    ? slot
                    : null;
        }

        var rows = new List<AlertRow>();
        rows.AddRange(attendanceFlags.Select(a => new AlertRow(
            AlertKind.AttendanceGps, a.Id, a.OccurredAt, users[a.UserId], signs[a.SignId!.Value].Area!, signs[a.SignId!.Value],
            a.DistanceM, a.AccuracyM, [], ReviewOf(AlertType.AttendanceGps, a.Id), null)));
        rows.AddRange(submissionFlags.Select(s => new AlertRow(
            AlertKind.SubmissionGps, s.Id, s.SubmittedAt, users[s.UserId], signs[s.SignId].Area!, signs[s.SignId],
            s.DistanceM, s.AccuracyM, [], ReviewOf(AlertType.SubmissionGps, s.Id), null)));
        rows.AddRange(inspectionFlags.Select(i => new AlertRow(
            AlertKind.InspectionGps, i.Id, i.InspectedAt, users[i.SupervisorId], signOfPoint[i.ServicePointId].Area!, signOfPoint[i.ServicePointId],
            i.DistanceM, i.AccuracyM, [], ReviewOf(AlertType.InspectionGps, i.Id), null)));
        rows.AddRange(blocked.Select(b => new AlertRow(
            AlertKind.BlockedScan, b.Id, b.ScannedAt, users[b.UserId], signs[b.SignId].Area!, signs[b.SignId],
            b.DistanceM, b.AccuracyM, [], ReviewOf(AlertType.BlockedScan, b.Id), CoverSlotFor(b), b.Reason)));
        rows.AddRange(missing.Select(m => new AlertRow(
            AlertKind.MissingAttendance, m.ShiftIn!.Id, m.End, users[m.UserId], areas[m.ShiftIn.AreaId], null,
            null, null, m.Missing, null, null)));

        return rows.OrderByDescending(r => r.At).ThenByDescending(r => r.RecordId).ToList();
    }
}
