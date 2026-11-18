using System.Globalization;
using JanitorialManagementDashboard.Api.Endpoints;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Geo;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §9 and ADRs facility-0204/0212/0213 (Task 10): the transferred person's one arrival confirmation at the destination
/// Area's Check-In Sign — the actual evidence that responsibility has moved. It answers the state word, the actual server
/// time and the revision of the instruction it belongs to.
/// <para>
/// It writes exactly one row, <see cref="AreaTransferConfirmation"/>, and nothing else. It is <em>not</em> a Shift-In, so it
/// never resets breaks or restarts the shift, and it never becomes a Break-In or Break-Out: the break state afterwards is the
/// break state before, which is the one thing <see cref="TransferRules.BreakStateAfterArrival"/> documents. It also never
/// enters the repeat-ignore window of an actual attendance action — the confirmation and the attendance clock are separate
/// facts, and a person who has just arrived must still be able to record the action that is legitimately next.
/// </para>
/// <para>
/// Two properties are enforced here rather than trusted from the client. A repeat of the same instruction returns the stored
/// fact before any revision, sign or GPS check, so a phone that retries (or a page reloaded after a lost response) sees the
/// arrival it already has. And cancel-versus-confirm is decided inside one locked read: the caller's transaction holds the
/// instruction row, and the confirmation row's unique <c>transfer_id</c> index is what arbitrates two racing confirmations —
/// the loser catches the duplicate, re-reads and answers the stored fact.
/// </para>
/// </summary>
public sealed class TransferCommandService(
    AppDbContext db, ScheduleStore store, TimeProvider clock, AttendanceSettings settings)
{
    /// <summary>The conflict code the arrival route answers with when the instruction cannot be confirmed as asked.</summary>
    public const string TransferRefused = "transfer-conflict";

    /// <summary>
    /// The answer a refusal carries: a machine code a page branches on and a message a person reads, with the transfer's own
    /// state when one is stored so the screen can re-render from the server's facts instead of guessing.
    /// </summary>
    public sealed record Refusal(string Code, string Message);

    public sealed record Confirmation(TransferResult? Result, Refusal? Refusal, AreaTransfer? NewArrival = null);

    /// <summary>
    /// Confirm arrival under <paramref name="transferId"/> as <paramref name="actorId"/>. A stored confirmation answers the
    /// same fact whatever else the request says; everything else is refused with <see cref="Refusal"/>, which the route maps to
    /// the status the situation deserves.
    /// </summary>
    public async Task<Confirmation> ConfirmAsync(
        int actorId, int transferId, string? expectedRevision, string? qrToken, GpsInputDto gps,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gps);

        var instruction = await db.AreaTransfers.AsNoTracking().FirstOrDefaultAsync(row => row.Id == transferId, ct);
        if (instruction is null)
        {
            return new(null, new Refusal("transfer-not-found", "ไม่พบคำสั่งย้ายพื้นที่นี้"));
        }

        // Authenticated as the transfer's own person: nobody else can turn another person's instruction into evidence, and a
        // stranger is refused before anything about the instruction is described.
        if (instruction.PersonId != actorId)
        {
            return new(null, new Refusal("transfer-not-yours", "คำสั่งย้ายนี้ไม่ใช่ของคุณ"));
        }

        // Idempotence next, before any revision, sign or GPS check: the confirmation is identified by its instruction, so a
        // repeat is the same fact and not a second arrival (spec §9: "Repeated same confirmation คืนผลเดิม").
        if (await StoredAsync(transferId, ct) is { } stored)
        {
            return new(Result(stored.Instruction, stored.Confirmation), null);
        }

        var slot = new ShiftSlot(instruction.ShiftDate, instruction.Shift);
        var now = clock.GetUtcNow();

        // A cancelled instruction holds no responsibility: there is nothing left to arrive under.
        if (instruction.IsCancelled)
        {
            return new(null, new Refusal("transfer-cancelled", "คำสั่งย้ายพื้นที่นี้ถูกยกเลิกแล้ว"));
        }

        // The page binds the confirmation to the revision it read (spec §9: "ผูก transferId/current revision"), so a client
        // cannot arrive against a state it never saw. A repeat is answered above by the stored fact, not here.
        var current = instruction.Revision.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(expectedRevision))
        {
            return new(null, new Refusal("revision-required",
                "ต้องแนบเวอร์ชันของคำสั่งย้ายที่อ่านมาด้วย กรุณาโหลดหน้าใหม่แล้วลองอีกครั้ง"));
        }

        if (!string.Equals(expectedRevision.Trim(), current, StringComparison.Ordinal))
        {
            return new(null, new Refusal("stale-revision",
                "คำสั่งย้ายมีการเปลี่ยนแปลง กรุณาโหลดข้อมูลใหม่แล้วตรวจอีกครั้ง"));
        }

        // Before its effective time the destination is not usable at all (spec §9): the move is announced, not in force.
        var effectiveAt = new DateTimeOffset(DateTime.SpecifyKind(instruction.EffectiveAt, DateTimeKind.Utc), TimeSpan.Zero);
        if (now < effectiveAt)
        {
            return new(null, new Refusal("transfer-not-effective", "ยังไม่ถึงเวลาที่คำสั่งย้ายมีผล"));
        }

        // The slot must still be the person's own receiving window: an arrival confirmed the next morning belongs to no slot.
        var (opens, closes) = AttendanceCalendar.Window(slot, settings);
        if (now.UtcDateTime < opens || now.UtcDateTime > closes)
        {
            return new(null, new Refusal("transfer-outside-window", "ตอนนี้ไม่อยู่ในช่วงเวลาของกะนี้"));
        }

        // GPS keeps the existing policy exactly: a missing location refuses the scan, a coarse one is stored and flagged.
        if (!GpsInput.TryRead(gps.Latitude, gps.Longitude, gps.AccuracyM, out var reading, out var gpsError))
        {
            return new(null, new Refusal("gps-required", gpsError));
        }

        var sign = await db.Signs.Include(entry => entry.Area)
            .FirstOrDefaultAsync(entry => entry.QrToken == qrToken && entry.ServicePointId == null && entry.Area!.IsActive,
                ct);
        if (sign is null)
        {
            return new(null, new Refusal("sign-not-found", "ไม่พบป้ายลงเวลานี้"));
        }

        // The scanned sign has to be the destination's own Check-In Sign: no other Area's sign is arrival evidence, and a
        // Service Point sign is work, never an entry.
        if (sign.AreaId != instruction.ToAreaId)
        {
            return new(null, new Refusal("sign-not-destination", "ป้ายที่สแกนไม่ใช่ป้ายของพื้นที่ปลายทาง"));
        }

        // One locked read decides cancel-versus-confirm: the instruction row is held for the rest of this transaction, so a
        // cancellation that commits first is seen here and a confirmation that commits first blocks the cancellation.
        await using var transaction = await store.BeginCommandTransactionAsync(ct);
        await store.LockSlotAsync(instruction.PersonId, instruction.ShiftDate, instruction.Shift, now.UtcDateTime, ct);

        instruction = await db.AreaTransfers.FirstAsync(row => row.Id == transferId, ct);
        if (instruction.IsCancelled)
        {
            await transaction.RollbackAsync(ct);
            return new(null, new Refusal("transfer-cancelled", "คำสั่งย้ายพื้นที่นี้ถูกยกเลิกแล้ว"));
        }

        if (await StoredAsync(transferId, ct) is { } raced)
        {
            // Another connection confirmed this very instruction while this one waited for the lock: its fact is the answer.
            await transaction.RollbackAsync(ct);
            return new(Result(raced.Instruction, raced.Confirmation), null);
        }

        if (await HasFinalOutAsync(db, instruction.PersonId, slot, ct))
        {
            return new(null, new Refusal(TransferRefused, "ลงเวลาเลิกงานแล้ว จึงยืนยันเข้าพื้นที่ใหม่ไม่ได้"));
        }

        var confirmation = new AreaTransferConfirmation
        {
            TransferId = instruction.Id,
            AreaId = instruction.ToAreaId,
            SignId = sign.Id,
            ConfirmedById = actorId,
            OccurredAt = now.UtcDateTime,
            Source = AttendanceSource.Scan,
            CreatedAt = now.UtcDateTime,
        };
        confirmation.StampGps(reading, sign);
        db.AreaTransferConfirmations.Add(confirmation);

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index on transfer_id is what arbitrates two racing confirmations: the loser writes nothing and
            // answers the winner's fact, which is exactly what the idempotent repeat would have returned.
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            if (await StoredAsync(transferId, ct) is { } winner)
            {
                return new(Result(winner.Instruction, winner.Confirmation), null);
            }

            throw;
        }

        return new(Result(instruction, confirmation), null, instruction);
    }

    internal static Task<bool> HasFinalOutAsync(AppDbContext db, int personId, ShiftSlot slot, CancellationToken ct) =>
        db.ShiftAttendances.AnyAsync(fact => fact.UserId == personId && fact.ShiftDate == slot.ShiftDate
            && fact.Shift == slot.Shift && fact.EventType == AttendanceEvent.ShiftOut, ct);

    /// <summary>The stored arrival under one instruction, with the instruction it belongs to, or null.</summary>
    private async Task<(AreaTransfer Instruction, AreaTransferConfirmation Confirmation)?> StoredAsync(
        int transferId, CancellationToken ct)
    {
        var confirmation = await db.AreaTransferConfirmations.AsNoTracking()
            .FirstOrDefaultAsync(row => row.TransferId == transferId, ct);
        if (confirmation is null)
        {
            return null;
        }

        var instruction = await db.AreaTransfers.AsNoTracking().FirstOrDefaultAsync(row => row.Id == transferId, ct);
        return instruction is null ? null : (instruction, confirmation);
    }

    private static TransferResult Result(AreaTransfer instruction, AreaTransferConfirmation confirmation) =>
        new(instruction.Id,
            TransferStates.Confirmed,
            new DateTimeOffset(DateTime.SpecifyKind(confirmation.OccurredAt, DateTimeKind.Utc), TimeSpan.Zero),
            instruction.Revision.ToString(CultureInfo.InvariantCulture));
}
