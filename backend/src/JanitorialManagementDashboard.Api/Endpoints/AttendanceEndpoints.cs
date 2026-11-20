using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Geo;
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
/// The Cleaner's own attendance: the scan itself, the state their page reads, and the check a printed sign makes.
/// <para>
/// Task 9 makes the scan read the slot's effective duty Area (spec §7) rather than the mutable <c>User.AreaId</c>, and makes
/// the write speak the slot's lifecycle (spec §8): a Numbered6 slot demands the contract marker and the revision of the page
/// that sent it, and a gap jump is an explicit set of skip markers. A Legacy4 slot keeps its four actions, its old order
/// rule and its old response, and a page that sends none of the new fields keeps working.
/// </para>
/// </summary>
public static class AttendanceEndpoints
{
    private const string CleanersOnly = "การลงเวลาใช้สำหรับแม่บ้านเท่านั้น";
    private const string SignNotFound = "ไม่พบป้ายลงเวลานี้";
    private const string OutsideWindow = "ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ";

    /// <summary>The page's contract no longer matches the slot: it must re-read before writing anything (spec §8).</summary>
    public const string RefreshRequired = "refresh-required";

    /// <summary>The requested action is not one the slot offers next.</summary>
    public const string ActionNotAllowed = "action-not-allowed";

    /// <summary>The confirmed skip markers are not exactly the gaps reaching the requested action.</summary>
    public const string GapsNotConfirmed = "gaps-not-confirmed";

    /// <summary>
    /// Spec §9: an authorised transfer has taken effect and the destination arrival is not confirmed yet, so no actual action
    /// belongs to either Area. The page shows "ยืนยันเข้าพื้นที่ใหม่" and the scan is retried after the confirmation.
    /// </summary>
    public const string TransferPending = "transfer-pending";

    /// <summary>The message the pending-transfer conflict carries, so every consuming route says the same thing.</summary>
    public const string TransferPendingMessage = "มีคำสั่งย้ายพื้นที่ที่มีผลแล้วแต่ยังไม่ได้ยืนยันเข้าพื้นที่ใหม่ กรุณายืนยันที่ป้ายของพื้นที่ปลายทางก่อนลงเวลา";

    private const string RefreshRequiredMessage = "หน้านี้ไม่ตรงกับการลงเวลาล่าสุด โหลดหน้าใหม่แล้วลองอีกครั้ง";

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/attendance", RecordAsync).RequireAuthorization();
        app.MapGet("/api/attendance/me", GetMineAsync).RequireAuthorization();
        app.MapGet("/api/attendance/signs/{token}", CheckSignAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> RecordAsync(
        RecordAttendanceRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        ScheduleStore store,
        TimeProvider clock,
        AttendanceSettings settings,
        IHubContext<ScanHub> hub,
        ILogger<ScanHub> logger,
        CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.Role != UserRole.Cleaner)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, CleanersOnly);
        }

        if (request.EventType is not { } eventType || !Enum.IsDefined(eventType))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุประเภทการลงเวลา");
        }

        if (!GpsInput.TryRead(request.Latitude, request.Longitude, request.AccuracyM, out var gps, out var gpsError))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, gpsError);
        }

        var sign = await FindCheckInSignAsync(db, request.QrToken);
        if (sign is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, SignNotFound);
        }

        var area = sign.Area!;

        var now = clock.GetUtcNow().UtcDateTime;

        var slot = user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, now, settings) : null;
        if (slot is null)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, OutsideWindow);
        }

        await using var transaction = await store.BeginCommandTransactionAsync(ct);
        await store.LockSlotAsync(user.Id, slot.ShiftDate, slot.Shift, now, ct);

        // Spec §7 and ruling A: the sign's Area must be the slot's effective duty Area — a live relief assignment's Area, a
        // dated plan's Area, or the compatibility projection for a person with no dated identity. Home building alone grants
        // nothing, so a relief with no assignment is refused exactly as another Area's sign is refused today.
        var duty = await EffectiveDutyReader.ReadAsync(db, store, user, slot, new DateTimeOffset(now, TimeSpan.Zero), ct);

        // Spec §9 + pending-transfer final departure override: an unconfirmed transfer blocks every break action and
        // every other Area's sign, except an explicitly confirmed final ShiftOut at the origin Area's sign. The pending
        // transfer stays instructed (unconfirmed) for Supervisor visibility and is never auto-cancelled.
        var confirmedPendingTransferShiftOut = duty.IsTransferPending
            && eventType == AttendanceEvent.ShiftOut
            && duty.Transfer is { } pendingTransfer
            && area.Id == pendingTransfer.FromAreaId
            && request.ConfirmPendingTransfer;

        if (duty.IsTransferPending && !confirmedPendingTransferShiftOut)
        {
            return TransferPendingRefusal();
        }

        if (!confirmedPendingTransferShiftOut && area.Id != duty.AreaId)
        {
            var blocked = await BlockedScans.BlockAsync(db, user, sign, area, BlockReason.OtherArea, gps, now);
            await transaction.CommitAsync(ct);
            return blocked;
        }

        if (!area.HasShift(slot.Shift))
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, OutsideWindow);
        }

        var state = await AttendanceStateView.LoadAsync(db, user.Id, slot, duty, now, ct);

        // Spec §8 Cutover: a numbered slot only accepts a new write from a page that knows the contract and the slot state
        // it read. Check the contract marker first, then let an already-recorded action replay idempotently before checking
        // its revision. The successful first write necessarily changed that revision.
        if (state.Version == AttendanceLifecycleVersion.Numbered6)
        {
            if (!string.Equals(request.LifecycleVersion, nameof(AttendanceLifecycleVersion.Numbered6), StringComparison.Ordinal))
            {
                return Refused(RefreshRequired, RefreshRequiredMessage, state);
            }
        }
        else
        {
            // A legacy slot has no skip markers and no numbered contract: a page claiming either is a stale page.
            var claimsAnotherContract = request.LifecycleVersion is { } claimed
                && !string.Equals(claimed, nameof(AttendanceLifecycleVersion.Legacy4), StringComparison.Ordinal);
            if (claimsAnotherContract
                || request.ConfirmGaps is { Count: > 0 }
                || (request.StateRevision is { } supplied && !string.Equals(supplied, state.Revision, StringComparison.Ordinal)))
            {
                return Refused(RefreshRequired, RefreshRequiredMessage, state);
            }
        }

        if (state.Events.Any(entry => entry.EventType == eventType))
        {
            return Results.Ok(state.ToDto(duty, AttendanceOutcome.AlreadyRecorded));
        }

        if (state.Version == AttendanceLifecycleVersion.Numbered6
            && !string.Equals(request.StateRevision, state.Revision, StringComparison.Ordinal))
        {
            return Refused(RefreshRequired, RefreshRequiredMessage, state);
        }

        var latest = state.Events.Count == 0 ? null : state.Events.MaxBy(entry => entry.OccurredAt)!;
        if (latest is not null && now - latest.OccurredAt < TimeSpan.FromMinutes(settings.RepeatIgnoreMinutes))
        {
            return Results.Ok(state.ToDto(duty, AttendanceOutcome.TooSoon));
        }

        var choice = state.NextActions.FirstOrDefault(candidate => candidate.Action == eventType);
        if (choice is null)
        {
            return Refused(ActionNotAllowed, "ลงเวลาไม่ตรงลำดับ", state);
        }

        var confirmed = (request.ConfirmGaps ?? []).Distinct().ToList();
        if (!confirmed.ToHashSet().SetEquals(choice.RequiredGaps))
        {
            return Refused(GapsNotConfirmed,
                $"ต้องยืนยันรายการที่ข้ามก่อน: {string.Join(", ", choice.RequiredGaps.Select(ThaiLabels.EventName))}",
                state, choice.RequiredGaps);
        }

        var entry = new ShiftAttendance
        {
            UserId = user.Id,
            AreaId = area.Id,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            EventType = eventType,
            OccurredAt = now,
            Source = AttendanceSource.Scan,
            SignId = sign.Id,
            CreatedAt = now,
        };
        entry.StampGps(gps, sign);

        var written = await AttendanceWrites.RecordAsync(
            db, entry, choice.RequiredGaps, area.Id, user.Id, now, also: null, ct);

        await transaction.CommitAsync(ct);
        if (!written.Stored)
        {
            // The unique index keeps the first press; the loser reads the facts as they now stand.
            var current = await AttendanceStateView.LoadAsync(db, user.Id, slot, duty, now, ct);
            return Results.Ok(current.ToDto(duty, AttendanceOutcome.AlreadyRecorded));
        }

        await AttendanceBoard.NotifyAsync(hub, db, logger, area.Id, slot);
        var after = await AttendanceStateView.LoadAsync(db, user.Id, slot, duty, now, ct);
        return Results.Ok(after.ToDto(duty, AttendanceOutcome.Recorded));
    }

    private static async Task<IResult> GetMineAsync(
        ClaimsPrincipal principal, AppDbContext db, ScheduleStore store, TimeProvider clock, AttendanceSettings settings,
        CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        return user.Role != UserRole.Cleaner
            ? ApiResults.Message(StatusCodes.Status403Forbidden, CleanersOnly)
            : Results.Ok(await StateForAsync(db, store, user, clock.GetUtcNow().UtcDateTime, settings, ct));
    }

    /// <summary>
    /// facility-0082: the check-in page asks on opening whether its sign still works. It records nothing, and it never
    /// says whose sign it is. For the caller's pending transfer, it can also say whether this sign accepts arrival.
    /// </summary>
    private static async Task<IResult> CheckSignAsync(
        string token, int? transferId, ClaimsPrincipal principal, AppDbContext db, ScheduleStore store,
        TimeProvider clock, AttendanceSettings settings, CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.Role != UserRole.Cleaner)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, CleanersOnly);
        }

        if (await FindCheckInSignAsync(db, token) is not { } sign)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, SignNotFound);
        }

        if (transferId is null)
        {
            return Results.NoContent();
        }

        var now = clock.GetUtcNow();
        var canConfirmTransfer = false;
        var canShiftOutOrigin = false;
        if (user.Shift is { } shift && AttendanceCalendar.SlotFor(shift, now.UtcDateTime, settings) is { } slot)
        {
            var duty = await EffectiveDutyReader.ReadAsync(db, store, user, slot, now, ct);
            if (duty.Transfer is { IsPending: true } transfer && transfer.Id == transferId
                && !await TransferCommandService.HasFinalOutAsync(db, user.Id, slot, ct))
            {
                canConfirmTransfer = transfer.ToAreaId == sign.AreaId;
                canShiftOutOrigin = transfer.FromAreaId == sign.AreaId;
            }
        }

        return Results.Ok(new { canConfirmTransfer, canShiftOutOrigin });
    }

    /// <summary>An Area's check-in sign by its QR Token, with its Area, while the Area is active; otherwise null.</summary>
    private static Task<Sign?> FindCheckInSignAsync(AppDbContext db, string token) =>
        db.Signs.Include(s => s.Area)
            .FirstOrDefaultAsync(s => s.QrToken == token && s.ServicePointId == null && s.Area!.IsActive);

    /// <summary>The Cleaner's own state: the slot their own shift is in on the clock, with its effective duty (spec §7).</summary>
    internal static async Task<AttendanceStateDto> StateForAsync(
        AppDbContext db, ScheduleStore store, User user, DateTime nowUtc, AttendanceSettings settings,
        CancellationToken ct = default)
    {
        var slot = user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, nowUtc, settings) : null;
        if (slot is null)
        {
            return new AttendanceStateDto(null, null, [], [], null);
        }

        var duty = await EffectiveDutyReader.ReadAsync(db, store, user, slot, new DateTimeOffset(nowUtc, TimeSpan.Zero), ct);
        return (await AttendanceStateView.LoadAsync(db, user.Id, slot, duty, nowUtc, ct)).ToDto(duty, null);
    }

    /// <summary>The noted slot's state for a person, as the scoped correction answers it.</summary>
    internal static async Task<AttendanceStateDto> StateForSlotAsync(
        AppDbContext db, ScheduleStore store, User user, ShiftSlot slot, DateTime nowUtc, CancellationToken ct = default)
    {
        var duty = await EffectiveDutyReader.ReadAsync(db, store, user, slot, new DateTimeOffset(nowUtc, TimeSpan.Zero), ct);
        return (await AttendanceStateView.LoadAsync(db, user.Id, slot, duty, nowUtc, ct)).ToDto(duty, null);
    }

    internal static Task<List<ShiftAttendance>> LoadEventsAsync(AppDbContext db, int userId, ShiftSlot slot) =>
        db.ShiftAttendances.AsNoTracking()
            .Where(a => a.UserId == userId && a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync();

    /// <summary>
    /// A refused write: 409 with the machine word a page branches on and the actions it may record now, so a page written
    /// before these words existed still shows the list it always showed.
    /// </summary>
    private static IResult Refused(
        string code, string message, AttendanceSlotState state, IReadOnlyList<AttendanceEvent>? requiredGaps = null) =>
        Results.Json(
            new AttendanceRefusedDto(message, state.NextActions.Select(choice => choice.Action).ToList(), code,
                requiredGaps),
            statusCode: StatusCodes.Status409Conflict);

    /// <summary>
    /// Spec §9: a pending transfer refuses every actual action, in either Area, with no allowed next actions — the only way
    /// forward is the destination confirmation, which is its own route.
    /// </summary>
    private static IResult TransferPendingRefusal() =>
        Results.Json(new AttendanceRefusedDto(TransferPendingMessage, [], TransferPending),
            statusCode: StatusCodes.Status409Conflict);
}
