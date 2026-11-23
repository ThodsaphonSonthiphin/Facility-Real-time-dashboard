using System.Globalization;
using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Rounds;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>
/// Spec §8 and §11: <c>POST /api/team-attendance/missing</c> — the scoped way a Supervisor fills a missing actual time that
/// was confirmed as a fact, with a reason. It is deliberately not a second Admin correction route: the Admin keeps its
/// existing wider rights at <c>PUT /api/admin/attendance</c>, and this route never overwrites a scan, never invents a time
/// for a skip marker, and only answers for a person whose slot's effective duty Area falls inside the actor's dated scope.
/// <para>
/// The group carries <see cref="AuthSetup.ScheduleManager"/>, so an anonymous caller is a 401 and a Cleaner a 403 before the
/// body is bound; who may be corrected is decided per slot afterwards by <see cref="ScheduleAuthority.CanManage"/>.
/// </para>
/// </summary>
public static class TeamAttendanceEndpoints
{
    /// <summary>Spec §11: the scoped missing-time route.</summary>
    public const string MissingPath = "/api/team-attendance/missing";

    /// <summary>
    /// Spec §4 and §8: the scoped attendance state of one person's slot for the date the manager selected, carrying the
    /// state revision the missing-time fill must send back. It answers only inside the actor's dated scope, exactly as the
    /// write does, so the form reads the same contract the write enforces.
    /// </summary>
    public const string StatePath = "/api/team-attendance/{userId:int}";

    private const string NotAuthorised = "ไม่มีสิทธิ์บันทึกเวลาของแม่บ้านคนนี้";

    /// <summary>One answer for every reason the actor may not touch this person: it never reveals their schedule.</summary>
    private const string AlreadyRecorded = "มีการลงเวลารายการนี้แล้ว ไม่เขียนทับเวลาที่สแกนไว้";

    public static IEndpointRouteBuilder MapTeamAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(MissingPath, AddMissingAsync).RequireAuthorization(AuthSetup.ScheduleManager);
        app.MapGet(StatePath, ReadAsync).RequireAuthorization(AuthSetup.ScheduleManager);
        return app;
    }

    private static async Task<IResult> ReadAsync(
        int userId,
        string? shiftDate,
        string? shift,
        ClaimsPrincipal principal,
        AppDbContext db,
        ScheduleStore store,
        ScheduleScopeReader scopes,
        TimeProvider clock,
        AttendanceSettings settings,
        CancellationToken ct)
    {
        var actor = await CurrentUser.LoadAsync(principal, db);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!DateOnly.TryParseExact(shiftDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || !Enum.TryParse<Shift>(shift, out var slotShift) || !Enum.IsDefined(slotShift))
        {
            return Bad("ต้องระบุวันที่และกะให้ถูกต้อง");
        }

        var person = await db.Users.Include(user => user.Area)
            .FirstOrDefaultAsync(user => user.Id == userId && user.Role == UserRole.Cleaner && user.IsActive, ct);
        if (person is null)
        {
            return UnavailablePerson(actor);
        }

        var slot = new ShiftSlot(date, slotShift);
        var now = clock.GetUtcNow();
        var duty = await EffectiveDutyReader.ReadAsync(db, store, person, slot, now, ct);
        var effectiveShift = await CleanerShiftResolver.ResolveShiftAsync(db, person, slot.ShiftDate, ct);
        var shiftMatches = duty.Source == EffectiveDuty.ReliefSource || effectiveShift == slot.Shift;
        var periods = actor.Role == UserRole.Admin ? [] : await scopes.ReadAsync(actor.Id, ct);
        bool CanReadBuilding(int building) =>
            InScope(actor, building, slot, now.UtcDateTime, periods);

        var current = duty.Transfer;
        var areaIds = new[] { duty.AreaId, current?.FromAreaId, current?.ToAreaId }
            .OfType<int>().Distinct().ToList();
        var areas = await db.Areas.AsNoTracking().Where(area => areaIds.Contains(area.Id))
            .ToDictionaryAsync(area => area.Id, ct);
        bool CanReadArea(int? areaId) => areaId is { } id && areas.TryGetValue(id, out var area)
            && CanReadBuilding(area.BuildingId);
        var today = ScheduleAuthority.BusinessDate(now.UtcDateTime);
        bool DutyAreaHasShift(int? areaId) => areaId is { } id && areas.TryGetValue(id, out var area)
            && AreaSupportsShift(area, slot, today);

        // A pending move has no writable duty Area. Its origin can still see and cancel the instruction, and a former
        // team can read the transfer facts that concern it without gaining access to the person's later attendance.
        var canReadAttendance = shiftMatches && DutyAreaHasShift(duty.AreaId) && CanReadArea(duty.AreaId);
        var canReadCurrent = current is not null &&
            (CanReadArea(current.FromAreaId) || CanReadArea(current.ToAreaId));
        var history = await ReadTransferHistoryAsync(db, person.Id, slot, CanReadBuilding, ct);
        if (!canReadAttendance && !canReadCurrent && history.Count == 0)
        {
            return NoDutyAccess(actor);
        }

        var state = canReadAttendance
            ? (await AttendanceStateView.LoadAsync(db, person.Id, slot, duty, now.UtcDateTime, ct)).ToDto(duty, null)
            : new AttendanceStateDto(date, slotShift, [], null, Transfer: current is { } transfer && canReadCurrent
                ? AttendanceSlotState.TransferDto(transfer)
                : null);

        return Results.Ok(state with
        {
            Transfer = canReadCurrent ? state.Transfer : null,
            TransferHistory = history,
            FillableActions = FillableActions(state),
            CanCancelTransfer = current is { State: TransferStates.Instructed } && CanReadArea(current.FromAreaId),
        });
    }

    private static IReadOnlyList<AttendanceEvent> FillableActions(AttendanceStateDto state) =>
        state.Actions?
            .Where(action => action.State is AttendanceStateView.Missing or AttendanceStateView.Skipped)
            .Select(action => action.EventType).ToList() ?? [];

    private static async Task<List<AttendanceTransferDto>> ReadTransferHistoryAsync(
        AppDbContext db, int personId, ShiftSlot slot, Func<int, bool> canReadBuilding, CancellationToken ct)
    {
        var confirmed = await (
            from instruction in db.AreaTransfers.AsNoTracking()
            where instruction.PersonId == personId && instruction.ShiftDate == slot.ShiftDate
                && instruction.Shift == slot.Shift && !instruction.IsCancelled
            join arrival in db.AreaTransferConfirmations.AsNoTracking() on instruction.Id equals arrival.TransferId
            join origin in db.Areas.AsNoTracking() on instruction.FromAreaId equals origin.Id
            join destination in db.Areas.AsNoTracking() on instruction.ToAreaId equals destination.Id
            orderby instruction.Id
            select new { instruction, arrival.OccurredAt, FromBuilding = origin.BuildingId, ToBuilding = destination.BuildingId })
            .ToListAsync(ct);
        return confirmed
            .Where(row => canReadBuilding(row.FromBuilding) || canReadBuilding(row.ToBuilding))
            .Select(row => new AttendanceTransferDto(
                row.instruction.Id, row.instruction.FromAreaId, row.instruction.ToAreaId,
                DateTime.SpecifyKind(row.instruction.EffectiveAt, DateTimeKind.Utc), TransferStates.Confirmed,
                DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc),
                row.instruction.Revision.ToString(CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static bool AreaSupportsShift(Area area, ShiftSlot slot, DateOnly today) =>
        slot.ShiftDate < today
            ? slot.Shift == Shift.Day || area.ShiftPattern == ShiftPattern.DayAndNight
            : area.HasShift(slot.Shift);

    private static bool InScope(User actor, int buildingId, ShiftSlot slot, DateTime nowUtc,
        IReadOnlyCollection<ScopePeriod> periods)
    {
        var today = ScheduleAuthority.BusinessDate(nowUtc);
        return slot.ShiftDate < today
            ? ScheduleAuthority.CanCorrectScheduleHistory(actor.Role, actor.Id, buildingId, slot.ShiftDate, today, periods)
            : ScheduleAuthority.CanManageSchedule(actor.Role, actor.Id, buildingId, slot.ShiftDate, periods);
    }

    private static IResult UnavailablePerson(User actor) => actor.Role == UserRole.Admin
        ? Bad("ต้องเลือกแม่บ้านที่ใช้งานอยู่")
        : ApiResults.Message(StatusCodes.Status403Forbidden, NotAuthorised);

    private static IResult NoDutyAccess(User actor) => actor.Role == UserRole.Admin
        ? Bad("แม่บ้านคนนี้ยังไม่มีพื้นที่")
        : ApiResults.Message(StatusCodes.Status403Forbidden, NotAuthorised);

    private static async Task<IResult> AddMissingAsync(
        AddMissingAttendanceRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        ScheduleStore store,
        ScheduleScopeReader scopes,
        TimeProvider clock,
        AttendanceSettings settings,
        ScheduleNotifications notifications,
        CancellationToken ct)
    {
        var actor = await CurrentUser.LoadAsync(principal, db);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (request.ShiftDate is not { } shiftDate
            || request.Shift is not { } shift || !Enum.IsDefined(shift)
            || request.EventType is not { } eventType || !Enum.IsDefined(eventType))
        {
            return Bad("ต้องระบุวันที่ กะ และรายการที่จะเพิ่ม");
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return Bad("ต้องกรอกเหตุผล");
        }

        if (reason.Length > AdminAttendanceEndpoints.MaxReasonLength)
        {
            return Bad($"เหตุผลยาวได้ไม่เกิน {AdminAttendanceEndpoints.MaxReasonLength} ตัวอักษร");
        }

        if (!RoundWindowRules.TryParseTime(request.Time, out var time))
        {
            return Bad("กรอกชั่วโมงและนาทีให้ครบ เช่น 06:58");
        }

        var expected = request.ExpectedRevision?.Trim();
        if (string.IsNullOrEmpty(expected))
        {
            return Bad("ต้องส่งรุ่นข้อมูลล่าสุดที่อ่านมากับคำขอ");
        }

        var person = await db.Users.Include(user => user.Area)
            .FirstOrDefaultAsync(user => user.Id == request.UserId && user.Role == UserRole.Cleaner && user.IsActive, ct);
        if (person is null)
        {
            return UnavailablePerson(actor);
        }

        var slot = new ShiftSlot(shiftDate, shift);

        // The actual fact belongs to the effective duty Area. A pending transfer permits no attendance write.
        var nowOffset = clock.GetUtcNow();
        await using var transaction = await store.BeginCommandTransactionAsync(ct);
        await store.LockSlotAsync(person.Id, slot.ShiftDate, slot.Shift, nowOffset.UtcDateTime, ct);
        var duty = await EffectiveDutyReader.ReadAsync(db, store, person, slot, nowOffset, ct);
        if (duty.AreaId is not { } areaId)
        {
            return NoDutyAccess(actor);
        }

        var dutyArea = await db.Areas.AsNoTracking().FirstOrDefaultAsync(area => area.Id == areaId, ct);
        if (dutyArea is null)
        {
            return NoDutyAccess(actor);
        }

        if (actor.Role != UserRole.Admin)
        {
            var periods = await scopes.ReadAsync(actor.Id, ct);
            if (!InScope(actor, dutyArea.BuildingId, slot, nowOffset.UtcDateTime, periods))
            {
                return NoDutyAccess(actor);
            }
        }

        // facility-0069: missing attendance may only be added for the Cleaner's effective shift on that date
        // (or a live relief assignment in that slot), and only when the Area supports that shift (past slots allow an
        // Area deactivated after the shift while still blocking DayOnly Areas on Night shift).
        var today = ScheduleAuthority.BusinessDate(nowOffset.UtcDateTime);
        var effectiveShift = await CleanerShiftResolver.ResolveShiftAsync(db, person, slot.ShiftDate, ct);
        if ((duty.Source != EffectiveDuty.ReliefSource && effectiveShift != slot.Shift)
            || !AreaSupportsShift(dutyArea, slot, today))
        {
            return NoDutyAccess(actor);
        }

        var now = nowOffset.UtcDateTime;
        var state = await AttendanceStateView.LoadAsync(db, person.Id, slot, duty, now, ct);

        // Spec §8: an existing actual scan is never overwritten here; the Admin edit route stays the only overwrite path.
        if (state.Events.Any(fact => fact.EventType == eventType))
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, AlreadyRecorded);
        }

        if (AttendanceCalendar.PlaceTime(slot, time, settings) is not { } at)
        {
            return Bad($"เวลานี้อยู่นอกช่วงลงเวลาของกะนี้ ({settings.OpensMinutesBeforeShift} นาทีก่อนกะถึง {settings.ClosesMinutesAfterShift} นาทีหลังกะ)");
        }

        if (at > now)
        {
            return Bad("เวลาที่ใส่ยังมาไม่ถึง");
        }

        var proposed = state.Events.ToDictionary(fact => fact.EventType, fact => fact.OccurredAt);
        proposed[eventType] = at;
        if (AttendanceRules.CheckTimes(proposed) is { } orderError)
        {
            return Bad(orderError);
        }

        // Actual revisions survive plan changes. Recheck eligibility against the current plan before adding a fact.
        var current = state.ToDto(duty, null);
        var fillable = FillableActions(current);
        if (!string.Equals(expected, state.Revision, StringComparison.Ordinal) || !fillable.Contains(eventType))
        {
            return Results.Json(
                new AttendanceStateConflictDto(
                    "ข้อมูลการลงเวลาเปลี่ยนไปหรือรายการนี้เติมเวลาไม่ได้ กรุณาตรวจข้อมูลล่าสุด",
                    AttendanceEndpoints.RefreshRequired,
                    current with { FillableActions = fillable }),
                statusCode: StatusCodes.Status409Conflict);
        }

        var areaCode = await db.Areas.Where(area => area.Id == areaId).Select(area => area.Code).FirstOrDefaultAsync(ct);
        var summary = $"เพิ่มเวลา{ThaiLabels.EventName(eventType)}ของ {person.DisplayName} {areaCode} "
            + $"กะ{ThaiLabels.ShiftName(shift)} {ThaiLabels.Date(shiftDate)}: – → {ThaiLabels.Clock(at)}";

        var row = new ShiftAttendance
        {
            UserId = person.Id,
            AreaId = areaId,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            EventType = eventType,
            OccurredAt = at,
            Source = AttendanceSource.SupervisorAdd,
            SignId = null,
            CreatedAt = now,
        };

        var written = await AttendanceWrites.RecordAsync(
            db, row, [], areaId, actor.Id, now,
            async saved =>
            {
                AuditTrail.Add(db, actor.Id, now, "ATTENDANCE_ADD", "shift_attendances", saved.Id, summary,
                    before: null, after: Snapshot(saved), reason);
                await db.SaveChangesAsync(ct);
            },
            ct);

        if (!written.Stored)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, AlreadyRecorded);
        }

        await transaction.CommitAsync(ct);
        await notifications.ActualChangedAsync(person.Id, areaId, slot, ct);
        return Results.Ok(await AttendanceEndpoints.StateForSlotAsync(db, store, person, slot, now, ct));
    }

    private static object Snapshot(ShiftAttendance row) => new
    {
        EventType = row.EventType.ToString(),
        OccurredAt = DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc),
        Source = row.Source.ToString(),
    };

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
