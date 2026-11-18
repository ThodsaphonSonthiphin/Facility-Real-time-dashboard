using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>
/// The attendance page (wireframe D2, facility-0027/0050): every active Area that works the shift, its regular
/// Cleaner's events and the covers assigned to it. Areas with nobody come first.
/// <para>
/// Spec §13 and ADR facility-0216 (Task 11): the row also carries the actual entry evidence of the Area and a separate
/// current-responsibility projection, so a midshift transfer neither hides the origin's historical entry nor claims the
/// destination before the arrival is confirmed. The Cleaner shown is the person currently responsible, which is the
/// transferred person at the destination after a confirmed arrival and nobody at the Area responsibility has left.
/// </para>
/// </summary>
internal static class AttendanceBoard
{
    public static async Task<AttendanceBoardDto> LoadAsync(
        AppDbContext db, ScheduleStore store, ShiftSlot slot, int? buildingId, DateTime nowUtc,
        CancellationToken ct = default)
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
            .Where(c => c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null
                && c.AreaId != null && areaIds.Contains(c.AreaId.Value))
            .OrderBy(c => c.AssignedAt)
            .ToListAsync();
        var userIds = cleaners.Select(u => u.Id).Concat(covers.Select(c => c.UserId)).Distinct().ToList();
        var events = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift && userIds.Contains(a.UserId))
            .OrderBy(a => a.OccurredAt)
            .ToListAsync();

        // Spec §8: the state word is read against the slot's own lifecycle, so a numbered break out is "OnBreak" and never
        // "Working". A slot without a lifecycle row keeps the legacy four actions.
        var version = AttendanceLifecycle.VersionFor(await db.AttendanceSlotLifecycles.AsNoTracking()
            .Where(row => row.ShiftDate == slot.ShiftDate && row.Shift == slot.Shift)
            .Select(row => (AttendanceLifecycleVersion?)row.Version)
            .FirstOrDefaultAsync());

        // Spec §13 and ADR facility-0216 (Task 11): entry comes from actual facts (initial Shift-In or a confirmed transfer
        // arrival), and current responsibility is a separate answer derived from the same transfer chain Task 10 stores.
        var evidence = await store.ReadEntryEvidenceAsync(slot, ct);
        var enteredAreas = ScheduleEntryEvidence.EnteredAreas(new ScheduleSlot(slot.ShiftDate, slot.Shift), evidence);
        var transfers = await store.ReadSlotTransfersAsync(slot, ct);
        var positions = transfers.ToDictionary(
            pair => pair.Key,
            pair => PositionOf(cleaners.FirstOrDefault(u => u.Id == pair.Key)?.AreaId, pair.Value, nowUtc));

        var rows = areas
            .Select(area =>
            {
                // The person shown is whoever is currently responsible here. A pending instruction has no confirmed
                // presence at its destination, so it is assigned there (covered) without papering over the missing arrival.
                var responsible = cleaners.FirstOrDefault(u => AreaOf(u, positions) == area.Id
                    && !(positions.TryGetValue(u.Id, out var position) && position.IsPending));
                var transferred = cleaners.Where(u => positions.ContainsKey(u.Id)).ToList();
                var assigned = covers.Any(c => c.AreaId == area.Id)
                    || cleaners.Any(u => AreaOf(u, positions) == area.Id);
                var responsibility = transferred
                    .Where(u => positions[u.Id].AreaId == area.Id
                        && positions[u.Id].State is TransferStates.Instructed or TransferStates.Confirmed)
                    .Select(u => new BoardResponsibilityDto(
                        u.Id, u.EmployeeId ?? string.Empty, u.DisplayName, positions[u.Id].State,
                        positions[u.Id].LastConfirmedAreaId, positions[u.Id].ConfirmedAt?.UtcDateTime))
                    .FirstOrDefault();

                return Row(area, responsible, covers.Where(c => c.AreaId == area.Id).ToList(), events, version, evidence,
                    enteredAreas, assigned, responsibility);
            })
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
        new(a.Id, a.EventType, Utc(a.OccurredAt), a.Source, a.WithinRadius, a.DistanceM, a.AreaId);

    /// <summary>
    /// facility-0027 rule 5: the attendance page reloads that board; it also polls every 30 s (facility-0061). The event is
    /// already saved, so a failed push is logged and never thrown, as in PointNotifications.
    /// </summary>
    public static async Task NotifyAsync(IHubContext<ScanHub> hub, AppDbContext db, ILogger logger, int areaId, ShiftSlot slot)
    {
        try
        {
            await hub.Clients.Users(await ActiveAdmins.IdsAsync(db))
                .SendAsync("AttendanceRecorded", new AttendanceChangedDto(areaId, slot.ShiftDate, slot.Shift));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Live update AttendanceRecorded for area {AreaId} failed; the record is saved", areaId);
        }
    }

    private static BoardRowDto Row(
        Area area, User? cleaner, List<CoverAssignment> covers, List<ShiftAttendance> events,
        AttendanceLifecycleVersion version, IReadOnlyList<AreaEntryEvidence> evidence,
        IReadOnlySet<int> enteredAreas, bool isAssigned, BoardResponsibilityDto? responsibility)
    {
        var own = cleaner is null ? new List<ShiftAttendance>() : events.Where(e => e.UserId == cleaner.Id).ToList();
        var entryEvidence = evidence
            .Where(row => row.AreaId == area.Id)
            .OrderBy(row => row.RecordedAt)
            .Select(row => new BoardEntryEvidenceDto(row.Kind, row.RecordedAt.UtcDateTime, row.TransferId))
            .ToList();
        return new BoardRowDto(
            area.Id,
            area.Code,
            area.Name,
            area.Building!.Code,
            cleaner is null ? null : new AssignedCleanerDto(cleaner.Id, cleaner.EmployeeId ?? string.Empty, cleaner.DisplayName),
            AreaAttendance.StateOf(own.Select(e => e.EventType).ToList(), covers.Count > 0, version),
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
                .ToList(),
            AreaAttendance.EntryOf(enteredAreas.Contains(area.Id), isAssigned),
            entryEvidence,
            responsibility);
    }

    /// <summary>
    /// The Area a person is currently responsible for: the transfer chain's answer when the slot has one, else their account
    /// Area (the board's pre-existing legacy answer for a person with no transfer).
    /// </summary>
    private static int? AreaOf(User user, IReadOnlyDictionary<int, TransferPosition> positions) =>
        positions.TryGetValue(user.Id, out var position) ? position.AreaId : user.AreaId;

    /// <summary>
    /// Spec §13 and ADR facility-0216: the responsibility a stored transfer chain means at <paramref name="nowUtc"/>. A
    /// confirmed arrival moves it to the destination; an effective, unconfirmed instruction follows the instruction (the
    /// destination is responsible while the arrival is missing) with the last confirmed Area and time kept separately; a
    /// cancelled or not-yet-effective one leaves it where the last confirmed arrival put it, else at the base Area.
    /// <para>
    /// This is not <c>EffectiveDuty</c>'s usable Area: that one is null while the arrival is unconfirmed because no actual
    /// action may be recorded there. The board reports responsibility and evidence, not permission to act.
    /// </para>
    /// </summary>
    private static TransferPosition PositionOf(int? baseAreaId, SlotTransfer transfer, DateTime nowUtc)
    {
        var instruction = transfer.Instruction;
        var state = instruction.IsCancelled ? TransferStates.Cancelled
            : transfer.Confirmation is not null ? TransferStates.Confirmed
            : TransferStates.Instructed;
        var effectiveAt = new DateTimeOffset(DateTime.SpecifyKind(instruction.EffectiveAt, DateTimeKind.Utc), TimeSpan.Zero);
        var confirmedAt = transfer.Confirmation is { } confirmation
            ? new DateTimeOffset(DateTime.SpecifyKind(confirmation.OccurredAt, DateTimeKind.Utc), TimeSpan.Zero)
            : (DateTimeOffset?)null;
        var pending = state == TransferStates.Instructed && nowUtc >= effectiveAt.UtcDateTime;
        var areaId = state == TransferStates.Confirmed || pending
            ? instruction.ToAreaId
            : transfer.LastConfirmedAreaId ?? baseAreaId;
        return new TransferPosition(areaId, transfer.LastConfirmedAreaId, confirmedAt, state, pending);
    }

    /// <summary>One person's current responsibility from the stored chain, with the confirmed evidence kept apart.</summary>
    private sealed record TransferPosition(
        int? AreaId, int? LastConfirmedAreaId, DateTimeOffset? ConfirmedAt, string State, bool IsPending);

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z".</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);
}
