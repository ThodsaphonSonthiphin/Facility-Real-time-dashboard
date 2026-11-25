using JanitorialManagementDashboard.Application.Alerts;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Inspections;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence;

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
    BlockReason? BlockReason = null,
    string? PersonAreaCode = null);

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
                g.Key.Slot,
                Departure: g.FirstOrDefault(a => a.EventType == AttendanceEvent.ShiftOut),
                End: ShiftCalendar.EndUtc(g.Key.Slot),
                ShiftIn: g.FirstOrDefault(a => a.EventType == AttendanceEvent.ShiftIn),
                Missing: AttendanceRules.MissingAtShiftEnd(g.Select(a => a.EventType).ToList())))
            .Where(s => s.End <= nowUtc && s.End >= fromUtc && s.End < toUtc && s.ShiftIn is not null && s.Missing.Count > 0)
            .ToList();

        var departures = missing.Where(row => row.Departure is not null).Select(row => row.UserId).Distinct().ToArray();
        if (departures.Length > 0)
        {
            var leaves = (await db.ScheduleExceptions.AsNoTracking()
                .Where(row => row.Kind == ScheduleExceptionKind.Leave && !row.IsCancelled
                    && departures.Contains(row.UserId) && row.ShiftDate >= firstDate && row.ShiftDate <= lastDate)
                .ToListAsync())
                .ToLookup(row => (row.UserId, row.ShiftDate, row.Shift));
            var store = new ScheduleStore(db);
            for (var i = missing.Count - 1; i >= 0; i--)
            {
                var row = missing[i];
                if (row.Departure is null) continue;
                var intervals = leaves[(row.UserId, row.Slot.ShiftDate, row.Slot.Shift)];
                if (!intervals.Any()) continue;

                int? breakGroup = null;
                if (row.Missing.Contains(AttendanceEvent.Break1In))
                {
                    var relief = await store.LiveReliefAsync(row.UserId, row.Slot.ShiftDate, row.Slot.Shift);
                    breakGroup = relief?.BreakGroup
                        ?? (await store.ReadPersonDayAsync(row.UserId, row.Slot.ShiftDate, row.Slot.Shift))?.BreakGroup.Value;
                }
                var expected = AttendanceExpectations.ForSlot(row.Slot, breakGroup);
                var remaining = row.Missing.Where(action => !expected.TryGetValue(action, out var due)
                    || !intervals.Any(leave => AttendanceApplicability.NotApplicable(due, AsUtc(row.Departure.OccurredAt),
                        AsUtc(leave.StartsAt!.Value), AsUtc(leave.EndsAt!.Value)))).ToList();
                if (remaining.Count == 0)
                {
                    missing.RemoveAt(i);
                }
                else
                {
                    missing[i] = row with { Missing = remaining };
                }
            }
        }

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
        var blockedSupervisors = blockedUserIds
            .Select(id => users[id])
            .Where(user => user.Role == UserRole.Supervisor)
            .ToList();
        var businessDate = ScheduleAuthority.BusinessDate(nowUtc);
        var supervisorPeriods = await SupervisorScopes.ReadManyAsync(
            db, blockedSupervisors, businessDate);

        var allAlertCleaners = users.Values
            .Where(user => user.Role == UserRole.Cleaner)
            .ToList();

        // Collect every ShiftSlot that could still be open for a blocked Cleaner scan (including 06:00-06:59 Thai
        // pre-shift scans whose ShiftDate is ahead of businessDate), plus every alert date for effective PersonAreaCode.
        var openBlockedSlots = new HashSet<ShiftSlot>();
        var alertDates = new HashSet<DateOnly>();
        foreach (var a in attendanceFlags) alertDates.Add(a.ShiftDate);
        foreach (var s in submissionFlags) alertDates.Add(s.ShiftDate);
        foreach (var m in missing) alertDates.Add(m.Slot.ShiftDate);
        foreach (var b in blocked)
        {
            alertDates.Add(ScheduleAuthority.BusinessDate(b.ScannedAt));
            if (users[b.UserId] is { Role: UserRole.Cleaner, IsActive: true })
            {
                foreach (var candidateShift in new[] { Shift.Day, Shift.Night })
                {
                    if (AttendanceCalendar.SlotFor(candidateShift, b.ScannedAt, settings) is { } candidate
                        && ShiftCalendar.EndUtc(candidate) > nowUtc)
                    {
                        openBlockedSlots.Add(candidate);
                        alertDates.Add(candidate.ShiftDate);
                    }
                }
            }
        }

        var cleanerStatesByDate = new Dictionary<DateOnly, Dictionary<int, CleanerEffectiveState>>(alertDates.Count);
        if (allAlertCleaners.Count > 0)
        {
            foreach (var date in alertDates)
            {
                cleanerStatesByDate[date] = await CleanerEffectiveAssignment.ResolveForDateAsync(
                    db, allAlertCleaners, date, useFallbackWeekdayArea: false, includeDatedOverrides: true);
            }
        }

        var cleanerIds = allAlertCleaners.Select(u => u.Id).ToList();
        var reliefBySlot = new Dictionary<(int UserId, DateOnly ShiftDate, Shift Shift), int>();
        if (cleanerIds.Count > 0 && alertDates.Count > 0)
        {
            var minDate = alertDates.Min();
            var maxDate = alertDates.Max();
            var reliefRows = await db.ReliefAssignments.AsNoTracking()
                .Where(r => cleanerIds.Contains(r.UserId) && r.ShiftDate >= minDate && r.ShiftDate <= maxDate && !r.IsCancelled)
                .OrderBy(r => r.Id)
                .Select(r => new { r.UserId, r.ShiftDate, r.Shift, r.AreaId })
                .ToListAsync();
            foreach (var row in reliefRows)
            {
                reliefBySlot[(row.UserId, row.ShiftDate, row.Shift)] = row.AreaId;
            }
        }

        var transfersBySlot = new Dictionary<ShiftSlot, IReadOnlyDictionary<int, SlotTransfer>>();
        if (openBlockedSlots.Count > 0)
        {
            var scheduleStore = new ScheduleStore(db);
            foreach (var openSlot in openBlockedSlots)
            {
                transfersBySlot[openSlot] = await scheduleStore.ReadSlotTransfersAsync(openSlot);
            }
        }

        var effectiveAreaIds = cleanerStatesByDate.Values
            .SelectMany(map => map.Values)
            .Select(state => state.AreaId)
            .OfType<int>()
            .Concat(reliefBySlot.Values)
            .Concat(transfersBySlot.Values.SelectMany(map => map.Values)
                .SelectMany(t => new int?[] { t.Instruction.ToAreaId, t.LastConfirmedAreaId })
                .OfType<int>())
            .Distinct()
            .ToList();
        var effectiveAreaCodes = effectiveAreaIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Areas.AsNoTracking()
                .Where(a => effectiveAreaIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Code);

        var activeCovers = await db.CoverAssignments.AsNoTracking()
            .Where(c => c.CancelledAt == null && c.ShiftDate >= firstDate && blockedUserIds.Contains(c.UserId))
            .ToListAsync();

        AlertReview? ReviewOf(AlertType type, long id) => reviews.FirstOrDefault(r => r.AlertType == type && r.RecordId == id);

        int? ConfirmedTransferAreaOf(int userId, ShiftSlot slot)
        {
            if (!transfersBySlot.TryGetValue(slot, out var map) || !map.TryGetValue(userId, out var transfer))
            {
                return null;
            }

            return !transfer.Instruction.IsCancelled && transfer.Confirmation is not null
                ? transfer.Instruction.ToAreaId
                : transfer.LastConfirmedAreaId;
        }

        string? PersonAreaCodeOf(User user, DateOnly date, Shift? shift)
        {
            if (user.Role != UserRole.Cleaner)
            {
                return null;
            }

            if (shift is { } s)
            {
                var slot = new ShiftSlot(date, s);
                if (ConfirmedTransferAreaOf(user.Id, slot) is int transferredAreaId
                    && effectiveAreaCodes.TryGetValue(transferredAreaId, out var transferredCode))
                {
                    return transferredCode;
                }

                if (reliefBySlot.TryGetValue((user.Id, date, s), out var reliefAreaId)
                    && effectiveAreaCodes.TryGetValue(reliefAreaId, out var reliefCode))
                {
                    return reliefCode;
                }
            }

            if (cleanerStatesByDate.TryGetValue(date, out var states) && states.TryGetValue(user.Id, out var state))
            {
                if (state.AreaId is int effAreaId && effectiveAreaCodes.TryGetValue(effAreaId, out var effCode))
                {
                    return effCode;
                }

                return state.HasDatedIdentity ? null : user.Area?.Code;
            }

            return user.Area?.Code;
        }

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
            if (user is not { Role: UserRole.Cleaner, IsActive: true })
            {
                return null;
            }

            var matching = new List<ShiftSlot>(2);
            foreach (var candidateShift in new[] { Shift.Day, Shift.Night })
            {
                var slot = AttendanceCalendar.SlotFor(candidateShift, scan.ScannedAt, settings);
                if (slot is null || ShiftCalendar.EndUtc(slot) <= nowUtc)
                {
                    continue;
                }

                if (!cleanerStatesByDate.TryGetValue(slot.ShiftDate, out var states)
                    || !states.TryGetValue(user.Id, out var state)
                    || state.Shift != slot.Shift)
                {
                    continue;
                }

                if (!area.HasShift(slot.Shift))
                {
                    continue;
                }

                int? reliefAreaId = reliefBySlot.TryGetValue((user.Id, slot.ShiftDate, slot.Shift), out var rId) ? rId : null;
                var effectiveAreaId = ConfirmedTransferAreaOf(user.Id, slot) ?? reliefAreaId ?? state.AreaId;
                if (effectiveAreaId is not int ownAreaId || ownAreaId == area.Id || state.AreaId == area.Id)
                {
                    continue;
                }

                if (!activeCovers.Any(c => c.UserId == user.Id && c.AreaId == area.Id && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift))
                {
                    matching.Add(slot);
                }
            }

            if (matching.Count <= 1)
            {
                return matching.Count == 1 ? matching[0] : null;
            }

            var scannedAtUtc = DateTime.SpecifyKind(scan.ScannedAt, DateTimeKind.Utc);
            return matching.FirstOrDefault(s =>
                scannedAtUtc >= ShiftCalendar.StartUtc(s) && scannedAtUtc < ShiftCalendar.EndUtc(s))
                ?? matching[^1];
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
                && SupervisorScope.WithoutCover(supervisorPeriods, user.Id, area.BuildingId, slot.Shift, slot.ShiftDate) is not null
                && !activeCovers.Any(c => c.UserId == user.Id && c.BuildingId == area.BuildingId && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift)
                    ? slot
                    : null;
        }

        var rows = new List<AlertRow>();
        rows.AddRange(attendanceFlags.Select(a => new AlertRow(
            AlertKind.AttendanceGps, a.Id, a.OccurredAt, users[a.UserId], signs[a.SignId!.Value].Area!, signs[a.SignId!.Value],
            a.DistanceM, a.AccuracyM, [], ReviewOf(AlertType.AttendanceGps, a.Id), null,
            PersonAreaCode: PersonAreaCodeOf(users[a.UserId], a.ShiftDate, a.Shift))));
        rows.AddRange(submissionFlags.Select(s => new AlertRow(
            AlertKind.SubmissionGps, s.Id, s.SubmittedAt, users[s.UserId], signs[s.SignId].Area!, signs[s.SignId],
            s.DistanceM, s.AccuracyM, [], ReviewOf(AlertType.SubmissionGps, s.Id), null,
            PersonAreaCode: PersonAreaCodeOf(users[s.UserId], s.ShiftDate, s.Shift))));
        rows.AddRange(inspectionFlags.Select(i => new AlertRow(
            AlertKind.InspectionGps, i.Id, i.InspectedAt, users[i.SupervisorId], signOfPoint[i.ServicePointId].Area!, signOfPoint[i.ServicePointId],
            i.DistanceM, i.AccuracyM, [], ReviewOf(AlertType.InspectionGps, i.Id), null)));
        rows.AddRange(blocked.Select(b =>
        {
            var coverSlot = CoverSlotFor(b);
            var personDate = coverSlot?.ShiftDate ?? ScheduleAuthority.BusinessDate(b.ScannedAt);
            var personShift = coverSlot?.Shift ?? (cleanerStatesByDate.TryGetValue(personDate, out var st) && st.TryGetValue(b.UserId, out var cs) ? cs.Shift : users[b.UserId].Shift);
            return new AlertRow(
                AlertKind.BlockedScan, b.Id, b.ScannedAt, users[b.UserId], signs[b.SignId].Area!, signs[b.SignId],
                b.DistanceM, b.AccuracyM, [], ReviewOf(AlertType.BlockedScan, b.Id), coverSlot, b.Reason,
                PersonAreaCode: PersonAreaCodeOf(users[b.UserId], personDate, personShift));
        }));
        rows.AddRange(missing.Select(m => new AlertRow(
            AlertKind.MissingAttendance, m.ShiftIn!.Id, m.End, users[m.UserId], areas[m.ShiftIn.AreaId], null,
            null, null, m.Missing, null, null,
            PersonAreaCode: PersonAreaCodeOf(users[m.UserId], m.Slot.ShiftDate, m.Slot.Shift))));

        return rows.OrderByDescending(r => r.At).ThenByDescending(r => r.RecordId).ToList();
    }

    private static DateTimeOffset AsUtc(DateTime stored) => new(DateTime.SpecifyKind(stored, DateTimeKind.Utc));
}
