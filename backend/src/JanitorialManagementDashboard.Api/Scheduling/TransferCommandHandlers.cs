using JanitorialManagementDashboard.Api.Endpoints;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §9 and ADRs facility-0203/0204/0213 (Task 10): the responsible Supervisor records one authorised Area move for a
/// person who has already entered work, with the plan time it takes effect.
/// <para>
/// What the instruction is <em>not</em>: a departure. The person's actual Shift-In stays where and when it was scanned, the
/// source Area keeps every fact it holds, no Shift-Out is written, and the breaks are untouched — <c>effectiveAt</c> is when
/// responsibility is planned to move, not a measured moment. Arrival is a separate fact the person confirms once at the
/// destination sign (<see cref="TransferCommandService"/>), and until then neither Area may record an actual action.
/// </para>
/// <para>
/// Two writers can never interleave: the person's own slot anchor is locked, and it is the same anchor the cancellation and
/// the arrival confirmation take, so an instruction cannot be read as "live" by a confirmation that is about to be cancelled.
/// The row lock is what makes "at most one unresolved instruction per slot" a property of the database rather than of a
/// client's timing.
/// </para>
/// </summary>
public sealed class InstructTransferHandler(TimeProvider clock, ScheduleStore store) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.InstructTransfer;

    public override Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadTransfer(request.Payload, out var transfer))
        {
            return Task.FromResult(ScheduleCommandLockSet.None);
        }

        // Spec §11: the slot anchor, not the instruction row — a command that creates the slot's first instruction and one
        // that cancels or arrives under it all contend on this row.
        return Task.FromResult(new ScheduleCommandLockSet(
            [new ScheduleSlotKey(transfer.UserId, transfer.ShiftDate, transfer.Shift)], [], []));
    }

    public override async Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadTransfer(request.Payload, out var transfer))
        {
            return Refused(new Dictionary<string, string>(), "payload", "ข้อมูลคำสั่งย้ายพื้นที่ไม่ครบถ้วน");
        }

        var actor = await ReadActorAsync(request, db, ct);
        if (actor is null)
        {
            return Refused(new Dictionary<string, string>(), "actor", "บัญชีนี้ไม่มีสิทธิ์สั่งย้ายพื้นที่");
        }

        var key = ScheduleRevisionKeys.Transfer(transfer.UserId, transfer.ShiftDate, transfer.Shift);
        var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal);

        // 1. The slot as the records give it: an active Cleaner whose dated identity works this shift on this date.
        var (target, targetProblem) = await ReadTargetAsync(db, transfer.UserId, transfer.ShiftDate, ct);
        if (target is null)
        {
            return Refused(revisions, key, targetProblem!);
        }

        if (target.Shift != transfer.Shift)
        {
            return Refused(revisions, key, "กะที่ส่งมาไม่ตรงกับกะของคนนี้ในวันนั้น");
        }

        var slot = new ShiftSlot(transfer.ShiftDate, transfer.Shift);

        // 2. The origin is the slot's current effective duty Area, read here rather than taken from the client: a stale page
        // that still shows the Area responsibility has already left must not be able to instruct a move out of it.
        var duty = await EffectiveDutyReader.ReadAsync(db, store, target.Person, slot, request.Now, ct);
        if (duty.AreaId is not int dutyArea)
        {
            return Refused(revisions, key, "คนนี้ยังไม่มีพื้นที่ปฏิบัติงานในกะนี้ จึงสั่งย้ายพื้นที่ไม่ได้");
        }

        if (dutyArea != transfer.FromAreaId)
        {
            return Refused(revisions, key,
                "พื้นที่ต้นทางที่ส่งมาไม่ตรงกับพื้นที่ปฏิบัติงานปัจจุบัน กรุณาโหลดข้อมูลใหม่แล้วตรวจอีกครั้ง");
        }

        // 3. The actor's authority is the FROM Area's building on that date and shift (spec §3): moving somebody out of an
        // Area is a decision of the team that holds it, and the destination grant is never inferred from the move.
        var fromArea = await db.Areas.AsNoTracking().FirstOrDefaultAsync(area => area.Id == dutyArea, ct);
        if (fromArea is null)
        {
            return Refused(revisions, key, "ไม่พบพื้นที่ต้นทางของกะนี้");
        }

        if (!actor.CanManage(fromArea.BuildingId, transfer.Shift, transfer.ShiftDate))
        {
            return Refused(revisions, key, $"ไม่มีสิทธิ์สั่งย้ายคนในพื้นที่ {fromArea.Code} วันที่ {Iso(transfer.ShiftDate)}");
        }

        // 4. Entered, and still on duty. ADR facility-0203: a person who has not checked in uses the ordinary initial
        // check-in at their Area instead, and a person who has already left cannot be moved mid-shift at all.
        var facts = await AttendanceEndpoints.LoadEventsAsync(db, target.Person.Id, slot);
        var shiftIn = facts.FirstOrDefault(fact => fact.EventType == AttendanceEvent.ShiftIn);
        if (shiftIn is null)
        {
            return Refused(revisions, key, "คนนี้ยังไม่ได้ลงเวลาเข้างาน จึงสั่งย้ายพื้นที่ไม่ได้");
        }

        if (facts.Any(fact => fact.EventType == AttendanceEvent.ShiftOut))
        {
            return Refused(revisions, key, "คนนี้ลงเวลาเลิกงานแล้ว จึงสั่งย้ายพื้นที่ไม่ได้");
        }

        // 5. The destination: an active Area that serves this shift, and never the one responsibility is already in.
        var toArea = await db.Areas.AsNoTracking().FirstOrDefaultAsync(area => area.Id == transfer.ToAreaId, ct);
        if (toArea is not { IsActive: true })
        {
            return Refused(revisions, key, "ไม่พบพื้นที่ปลายทางที่ยังใช้งานอยู่");
        }

        if (toArea.Id == dutyArea)
        {
            return Refused(revisions, key, "พื้นที่ปลายทางต้องต่างจากพื้นที่ต้นทาง");
        }

        if (!toArea.HasShift(transfer.Shift))
        {
            return Refused(revisions, key, $"พื้นที่ {toArea.Code} ไม่มีกะ{ThaiLabels.ShiftName(transfer.Shift)}");
        }

        // 6. The plan time belongs to this shift and cannot precede the arrival that already happened: a move effective
        // before the person entered would describe a transfer of somebody who was not there.
        var shiftStart = new DateTimeOffset(ShiftCalendar.StartUtc(slot), TimeSpan.Zero);
        var shiftEnd = new DateTimeOffset(ShiftCalendar.EndUtc(slot), TimeSpan.Zero);
        if (transfer.EffectiveAt < shiftStart || transfer.EffectiveAt >= shiftEnd)
        {
            return Refused(revisions, key, "เวลาที่มีผลต้องอยู่ภายในกะของวันนั้น");
        }

        var enteredAt = new DateTimeOffset(DateTime.SpecifyKind(shiftIn.OccurredAt, DateTimeKind.Utc), TimeSpan.Zero);
        if (transfer.EffectiveAt < enteredAt)
        {
            return Refused(revisions, key, "เวลาที่มีผลต้องไม่ก่อนเวลาที่ลงเวลาเข้างานจริง");
        }

        // 7. A move is a reasoned change of what a person is responsible for, so the reason is required (spec §5).
        var reason = ReadReason(transfer.Reason, required: true, out var reasonProblem,
            "ต้องระบุเหตุผลในการสั่งย้ายพื้นที่");
        if (reasonProblem is not null)
        {
            return Refused(revisions, key, reasonProblem);
        }

        // 8. The revision the client read, and the one-instruction rule. The slot's transfer revision is the latest row's
        // own revision whatever its state — a cancelled row is still what the screen shows, so a genuine "read, then
        // instruct again" must be able to echo it — while a confirmed one is history and the next move is a new forward
        // record whose origin is the destination (ADR facility-0213). Only a live unconfirmed instruction is already the
        // answer.
        var current = duty.Transfer?.Revision ?? ScheduleResolver.NoRevision;
        revisions[key] = current;
        if (IsStale(request, key, current, out var expected))
        {
            return new ScheduleCommandApplication(revisions, [Stale(key, expected, current)]);
        }

        if (duty.Transfer is { State: TransferStates.Instructed })
        {
            return Refused(revisions, key,
                "มีคำสั่งย้ายพื้นที่ที่ยังไม่ยืนยันในกะนี้อยู่แล้ว ต้องยกเลิกหรือรอการยืนยันก่อนจึงจะสั่งย้ายใหม่ได้");
        }

        // 9. One instruction row, its audit line, and the revision the client echoes for the confirmation or cancellation.
        var created = new AreaTransfer
        {
            PersonId = transfer.UserId,
            ShiftDate = transfer.ShiftDate,
            Shift = transfer.Shift,
            FromAreaId = dutyArea,
            ToAreaId = toArea.Id,
            EffectiveAt = transfer.EffectiveAt.UtcDateTime,
            Reason = reason!,
            CreatedById = request.ActorId,
            CreatedAt = request.Now.UtcDateTime,
            UpdatedAt = request.Now.UtcDateTime,
        };
        db.AreaTransfers.Add(created);
        db.ScheduleChanges.Add(Change(request, receipt, "AreaTransfer", null, "create", null,
            Json(new
            {
                revision = Revision(created.Revision),
                personId = created.PersonId,
                shiftDate = Iso(created.ShiftDate),
                shift = created.Shift.ToString(),
                fromAreaId = created.FromAreaId,
                toAreaId = created.ToAreaId,
                effectiveAt = created.EffectiveAt,
            }), reason));
        revisions[key] = Revision(created.Revision);
        return new ScheduleCommandApplication(revisions, []);
    }
}

/// <summary>
/// Spec §9 and ADR facility-0213 (Task 10): cancelling an instruction nobody has arrived under. The row is kept and marked
/// cancelled with the cancelling actor, the moment and the reason, because "it was instructed, then cancelled" is history a
/// screen has to be able to show; nothing is deleted and no actual fact is touched.
/// <para>
/// A confirmed arrival can never be removed here — <see cref="TransferRules.CanCancel"/> refuses it inside the same locked
/// state read that a racing confirmation takes, so exactly one of the two state transitions can win.
/// </para>
/// </summary>
public sealed class CancelTransferHandler(TimeProvider clock) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.CancelTransfer;

    public override async Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadTransferCancel(request.Payload, out var cancel))
        {
            return ScheduleCommandLockSet.None;
        }

        // The instruction names its own slot, and that slot anchor is what a concurrent confirmation locks too.
        var instruction = await db.AreaTransfers.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == cancel.TransferId, ct);
        return instruction is null
            ? ScheduleCommandLockSet.None
            : new ScheduleCommandLockSet(
                [new ScheduleSlotKey(instruction.PersonId, instruction.ShiftDate, instruction.Shift)], [], []);
    }

    public override async Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadTransferCancel(request.Payload, out var cancel))
        {
            return Refused(new Dictionary<string, string>(), "payload", "ข้อมูลการยกเลิกคำสั่งย้ายพื้นที่ไม่ครบถ้วน");
        }

        var actor = await ReadActorAsync(request, db, ct);
        if (actor is null)
        {
            return Refused(new Dictionary<string, string>(), "actor", "บัญชีนี้ไม่มีสิทธิ์ยกเลิกคำสั่งย้ายพื้นที่");
        }

        var instruction = await db.AreaTransfers.FirstOrDefaultAsync(row => row.Id == cancel.TransferId, ct);
        if (instruction is null)
        {
            return Refused(new Dictionary<string, string>(), "transferId", "ไม่พบคำสั่งย้ายพื้นที่นี้");
        }

        var key = ScheduleRevisionKeys.Transfer(instruction.PersonId, instruction.ShiftDate, instruction.Shift);
        var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal);

        // The same from-Area authority the instruction needed: the team that holds the Area cancels the move out of it.
        var fromArea = await db.Areas.AsNoTracking().FirstOrDefaultAsync(area => area.Id == instruction.FromAreaId, ct);
        if (fromArea is null || !actor.CanManage(fromArea.BuildingId, instruction.Shift, instruction.ShiftDate))
        {
            return Refused(revisions, key,
                $"ไม่มีสิทธิ์ยกเลิกคำสั่งย้ายในพื้นที่ {fromArea?.Code} วันที่ {Iso(instruction.ShiftDate)}");
        }

        if (instruction.IsCancelled)
        {
            return Refused(revisions, key, "คำสั่งย้ายพื้นที่นี้ถูกยกเลิกแล้ว");
        }

        // ADR facility-0213 through the pure rule, never from a client's claim: a confirmed arrival is evidence history keeps.
        if (!TransferRules.CanCancel(await HasConfirmationAsync(db, instruction.Id, ct)))
        {
            return Refused(revisions, key,
                "ยืนยันเข้าพื้นที่ใหม่แล้ว จึงยกเลิกคำสั่งย้ายไม่ได้ ต้องบันทึกการย้ายใหม่แทน");
        }

        var reason = ReadReason(cancel.Reason, required: true, out var reasonProblem,
            "ต้องระบุเหตุผลในการยกเลิกคำสั่งย้ายพื้นที่");
        if (reasonProblem is not null)
        {
            return Refused(revisions, key, reasonProblem);
        }

        var current = Revision(instruction.Revision);
        revisions[key] = current;
        if (IsStale(request, key, current, out var expected))
        {
            return new ScheduleCommandApplication(revisions, [Stale(key, expected, current)]);
        }

        var before = Json(new { revision = Revision(instruction.Revision), cancelled = false });
        instruction.IsCancelled = true;
        instruction.CancelledAt = request.Now.UtcDateTime;
        instruction.CancelledById = request.ActorId;
        instruction.CancellationReason = reason;
        instruction.Revision += 1;
        instruction.UpdatedAt = request.Now.UtcDateTime;

        db.ScheduleChanges.Add(Change(request, receipt, "AreaTransfer", instruction.Id, "cancel", before,
            Json(new { revision = Revision(instruction.Revision), cancelled = true }), reason));
        revisions[key] = Revision(instruction.Revision);
        return new ScheduleCommandApplication(revisions, []);
    }

    /// <summary>The one confirmation of an instruction, if somebody already arrived under it.</summary>
    internal static Task<bool> HasConfirmationAsync(AppDbContext db, int transferId, CancellationToken ct) =>
        db.AreaTransferConfirmations.AsNoTracking().AnyAsync(row => row.TransferId == transferId, ct);
}
