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

        var transfers = await store.ReadSlotTransfersAsync(slot, ct);
        var transferredIds = transfers.Where(pair => areaIds.Contains(pair.Value.Instruction.FromAreaId)
                || areaIds.Contains(pair.Value.Instruction.ToAreaId)
                || pair.Value.LastConfirmedAreaId is { } last && areaIds.Contains(last))
            .Select(pair => pair.Key).ToList();
        var relief = await db.ReliefAssignments.AsNoTracking()
            .Where(row => row.ShiftDate == slot.ShiftDate && row.Shift == slot.Shift && !row.IsCancelled)
            .OrderBy(row => row.Id)
            .Select(row => new { row.UserId, row.AreaId }).ToListAsync(ct);
        var reliefAreas = relief.GroupBy(row => row.UserId).ToDictionary(group => group.Key, group => group.Last().AreaId);
        var reliefHere = relief.Where(row => areaIds.Contains(row.AreaId)).Select(row => row.UserId).Distinct().ToList();
        var allActiveCleaners = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive)
            .ToListAsync(ct);
        var effectiveByUser = await CleanerEffectiveAssignment.ResolveForDateAsync(
            db, allActiveCleaners, slot.ShiftDate, useFallbackWeekdayArea: false, includeDatedOverrides: true, ct: ct);
        var areaIdSet = areaIds.ToHashSet();
        var transferredSet = transferredIds.ToHashSet();
        var reliefHereSet = reliefHere.ToHashSet();
        var slotEvents = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync(ct);
        var factsByPerson = slotEvents.ToLookup(fact => fact.UserId);
        var cleaners = allActiveCleaners
            .Where(u => (effectiveByUser.TryGetValue(u.Id, out var state)
                    && state.Shift == slot.Shift
                    && state.AreaId is int effAreaId
                    && areaIdSet.Contains(effAreaId)
                    && (!(state.IsDayOff || state.IsWholeShiftLeave) || factsByPerson[u.Id].Any()))
                || transferredSet.Contains(u.Id)
                || reliefHereSet.Contains(u.Id)
                || factsByPerson[u.Id].Any(fact => areaIdSet.Contains(fact.AreaId)))
            .ToList();
        var covers = await db.CoverAssignments.AsNoTracking()
            .Include(c => c.User!).ThenInclude(u => u.Area)
            .Where(c => c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null
                && c.AreaId != null && areaIds.Contains(c.AreaId.Value))
            .OrderBy(c => c.AssignedAt)
            .ToListAsync(ct);
        var coverEffectiveAreaIds = covers
            .Select(c => reliefAreas.TryGetValue(c.UserId, out var rArea) ? (int?)rArea : effectiveByUser.GetValueOrDefault(c.UserId)?.AreaId)
            .OfType<int>()
            .Distinct()
            .ToList();
        var coverAreaCodes = coverEffectiveAreaIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Areas.AsNoTracking()
                .Where(a => coverEffectiveAreaIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Code, ct);

        // Spec §13 and ADR facility-0216 (Task 11): entry comes from actual facts (initial Shift-In or a confirmed transfer
        // arrival), and current responsibility is a separate answer derived from the same transfer chain Task 10 stores.
        var evidence = await store.ReadEntryEvidenceAsync(slot, ct);
        var enteredAreas = ScheduleEntryEvidence.EnteredAreas(new ScheduleSlot(slot.ShiftDate, slot.Shift), evidence);
        var baseAreas = cleaners.ToDictionary(user => user.Id,
            user => reliefAreas.TryGetValue(user.Id, out var assigned)
                ? (int?)assigned
                : effectiveByUser.TryGetValue(user.Id, out var state)
                    && state.Shift == slot.Shift
                    && state.AreaId is int effAreaId
                    && (!(state.IsDayOff || state.IsWholeShiftLeave) || factsByPerson[user.Id].Any())
                    ? effAreaId
                    : factsByPerson[user.Id].FirstOrDefault(fact => fact.EventType == AttendanceEvent.ShiftIn)?.AreaId
                        ?? factsByPerson[user.Id].FirstOrDefault()?.AreaId);
        var positions = transfers.ToDictionary(
            pair => pair.Key,
            pair => PositionOf(baseAreas.GetValueOrDefault(pair.Key), pair.Value, nowUtc));

        var rows = areas
            .Select(area =>
            {
                // The person shown is whoever is currently responsible here. A pending instruction has no confirmed
                // presence at its destination, so it is assigned there (covered) without papering over the missing arrival.
                var assignedHere = cleaners.Where(user => (positions.TryGetValue(user.Id, out var position)
                    ? position.AreaId : baseAreas[user.Id]) == area.Id).ToList();
                var responsible = assignedHere
                    .Where(user => !(positions.TryGetValue(user.Id, out var position) && position.IsPending))
                    .OrderByDescending(user => baseAreas.GetValueOrDefault(user.Id) == area.Id)
                    .ThenBy(user => user.Id)
                    .ToList();
                var responsibilities = assignedHere
                    .Where(user => positions.TryGetValue(user.Id, out var position)
                        && position.State is TransferStates.Instructed or TransferStates.Confirmed)
                    .Select(user => new BoardResponsibilityDto(
                        user.Id, user.EmployeeId ?? string.Empty, user.DisplayName, positions[user.Id].State,
                        positions[user.Id].LastConfirmedAreaId, positions[user.Id].ConfirmedAt?.UtcDateTime))
                    .ToList();
                var areaCovers = covers.Where(cover => cover.AreaId == area.Id).ToList();
                return Row(area, responsible, areaCovers, factsByPerson, evidence, enteredAreas,
                    areaCovers.Count > 0 || assignedHere.Count > 0, responsibilities, effectiveByUser, reliefAreas, coverAreaCodes);
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
        Area area, IReadOnlyList<User> cleaners, List<CoverAssignment> covers, ILookup<int, ShiftAttendance> facts,
        IReadOnlyList<AreaEntryEvidence> evidence,
        IReadOnlySet<int> enteredAreas, bool isAssigned, List<BoardResponsibilityDto> responsibilities,
        IReadOnlyDictionary<int, CleanerEffectiveState> effectiveByUser,
        IReadOnlyDictionary<int, int> reliefAreas,
        IReadOnlyDictionary<int, string> coverAreaCodes)
    {
        var primary = cleaners.FirstOrDefault();
        var own = primary is null ? [] : facts[primary.Id].Select(ToDto).ToList();
        var additional = cleaners.Skip(1)
            .Select(person => new BoardCleanerAttendanceDto(CleanerDto(person), facts[person.Id].Select(ToDto).ToList()))
            .ToList();
        var states = cleaners.Select(person =>
            AreaAttendance.StateOf(facts[person.Id].Select(fact => fact.EventType).ToList(), false)).ToHashSet();
        var state = states.Contains(AreaAttendanceState.Working) ? AreaAttendanceState.Working
            : states.Contains(AreaAttendanceState.OnBreak) ? AreaAttendanceState.OnBreak
            : states.Contains(AreaAttendanceState.Done) ? AreaAttendanceState.Done
            : covers.Count > 0 ? AreaAttendanceState.Covered
            : AreaAttendanceState.Empty;
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
            primary is null ? null : CleanerDto(primary),
            state,
            own,
            covers.Select(c =>
                {
                    var effAreaId = reliefAreas.TryGetValue(c.UserId, out var rArea)
                        ? (int?)rArea
                        : effectiveByUser.GetValueOrDefault(c.UserId)?.AreaId;
                    var ownAreaCode = effAreaId is int aId ? coverAreaCodes.GetValueOrDefault(aId) ?? c.User!.Area?.Code : c.User!.Area?.Code;
                    return new BoardCoverDto(
                        c.Id,
                        c.UserId,
                        c.User!.EmployeeId ?? string.Empty,
                        c.User.DisplayName,
                        ownAreaCode,
                        Utc(c.AssignedAt),
                        facts[c.UserId].Where(e => e.EventType == AttendanceEvent.ShiftIn)
                            .Select(e => (DateTime?)Utc(e.OccurredAt))
                            .FirstOrDefault());
                })
                .ToList(),
            AreaAttendance.EntryOf(enteredAreas.Contains(area.Id), isAssigned),
            entryEvidence,
            responsibilities.FirstOrDefault(),
            additional.Count > 0 ? additional : null,
            responsibilities.Count > 1 ? responsibilities : null);
    }

    private static AssignedCleanerDto CleanerDto(User cleaner) =>
        new(cleaner.Id, cleaner.EmployeeId ?? string.Empty, cleaner.DisplayName);

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
