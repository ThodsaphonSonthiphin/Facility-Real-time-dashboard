using JanitorialManagementDashboard.Api.Endpoints;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §6 and ADRs facility-0137–0139, 0144, 0145, 0152, 0155, 0206, 0207 and 0211 (Task 7): the destination actor records
/// one relief duty in one Area on one date — a standalone assignment or cover for a named regular Cleaner, including a loan
/// from another building that the two Supervisors already agreed outside the web.
/// <para>
/// What it deliberately is <em>not</em>: an approval workflow. <see cref="ReliefAssignment.AgreementReported"/> is the
/// destination actor's own attestation; the server never claims a source Supervisor pressed anything here. When the source
/// building is somebody else's, the row must name that dated Supervisor and carry the attestation, so an unexplained loan
/// cannot be recorded — and when the actor is that Supervisor (one person holding both buildings) no second approver is
/// invented at all.
/// </para>
/// <para>
/// Conflicts never mutate the source: another live relief assignment, an agreed leave interval or a regular duty the person
/// already holds refuses the whole command and leaves every stored row exactly as it was. There is no automatic cancel, no
/// automatic move, no conversion of a legacy cover row, and no separate cancellation kind in this task — a slot holds one
/// live assignment.
/// </para>
/// </summary>
public sealed class AssignReliefHandler(TimeProvider clock, ScheduleStore store) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.AssignRelief;

    public override async Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadRelief(request.Payload, out var relief))
        {
            return ScheduleCommandLockSet.None;
        }

        // Spec §11: the person/slot rows this command creates or contends with — the relief's own slot, and the replaced
        // person's slot because their applicable plan decides which group is inherited and which duty is being covered. The
        // two building/shift scope anchors are what two recorders of the same loan actually share.
        var slots = new List<ScheduleSlotKey> { new(relief.UserId, relief.ShiftDate, relief.Shift) };
        if (relief.ReplacedUserId is int replaced && replaced != relief.UserId)
        {
            slots.Add(new ScheduleSlotKey(replaced, relief.ShiftDate, relief.Shift));
        }

        var scopes = new List<ScheduleScopeKey> { new(relief.SourceBuildingId, relief.Shift) };
        var destination = await db.Areas.AsNoTracking()
            .Where(area => area.Id == relief.AreaId)
            .Select(area => (int?)area.BuildingId)
            .FirstOrDefaultAsync(ct);
        if (destination is int building)
        {
            scopes.Add(new ScheduleScopeKey(building, relief.Shift));
        }

        return new ScheduleCommandLockSet(slots, [], scopes);
    }

    public override async Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadRelief(request.Payload, out var relief))
        {
            return Refused(new Dictionary<string, string>(), "payload", "ข้อมูลการมอบหมายงานสแปร์ไม่ครบถ้วน");
        }

        var actor = await ReadActorAsync(request, db, ct);
        if (actor is null)
        {
            return Refused(new Dictionary<string, string>(), "actor", "บัญชีนี้ไม่มีสิทธิ์จัดงานสแปร์");
        }

        var key = ScheduleRevisionKeys.Relief(relief.UserId, relief.ShiftDate, relief.Shift);
        var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal);

        // 1. The destination. A relief duty is planned by the actor responsible for the building and shift it happens in
        // (spec §3), and on a past date only by one who still holds that responsibility today.
        var area = await db.Areas.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == relief.AreaId, ct);
        if (area is null || !area.IsActive)
        {
            return Refused(revisions, key, "ไม่พบพื้นที่ปลายทางที่ยังใช้งานอยู่");
        }

        if (!area.HasShift(relief.Shift))
        {
            return Refused(revisions, key, $"พื้นที่ {area.Code} ไม่มีกะ{ThaiLabels.ShiftName(relief.Shift)}");
        }

        if (!actor.CanChange(area.BuildingId, relief.Shift, relief.ShiftDate))
        {
            return Refused(revisions, key,
                $"ไม่มีสิทธิ์จัดคนในพื้นที่ {area.Code} วันที่ {Iso(relief.ShiftDate)}");
        }

        // 2. The slot as it stood when the actor asked, read once the destination right is established: every refusal below
        // names the revision the client should compare against instead of making it guess (spec §11), and a stale read is
        // answered before anything else is decided about the row.
        var live = await store.LiveReliefAsync(relief.UserId, relief.ShiftDate, relief.Shift, ct);
        var current = Revision(live?.Revision);
        revisions[key] = current;
        if (IsStale(request, key, current, out var expected))
        {
            return new ScheduleCommandApplication(revisions, [Stale(key, expected, current)]);
        }

        // 3. The person who works it: an active Cleaner whose dated profile on that date says "relief", in that shift, from
        // a known home building. A regular Cleaner is not a relief, and a relief never has a regular Area to fall back on.
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == relief.UserId, ct);
        if (person is not { IsActive: true } || person.Role != UserRole.Cleaner)
        {
            return Refused(revisions, key, "เป้าหมายต้องเป็นบัญชีแม่บ้านที่ยังใช้งานอยู่");
        }

        var profile = await store.ReadProfileAsync(relief.UserId, relief.ShiftDate, ct);
        if (profile is null)
        {
            return Refused(revisions, key,
                $"คนนี้ยังไม่มีข้อมูลสแปร์ในวันที่ {Iso(relief.ShiftDate)} จึงยืนยันตัวตนไม่ได้");
        }

        if (profile.Kind != CleanerKind.Relief)
        {
            return Refused(revisions, key, "คนนี้ไม่ใช่สแปร์ในวันที่เลือก จึงมอบหมายงานข้ามตึกไม่ได้");
        }

        if (profile.Shift != relief.Shift)
        {
            return Refused(revisions, key, "กะที่ส่งมาไม่ตรงกับกะของคนนี้ในวันนั้น");
        }

        if (profile.HomeBuildingId is not int homeBuildingId)
        {
            return Refused(revisions, key, "ยังไม่ทราบตึกต้นทางของสแปร์คนนี้ จึงยืนยันการยืมไม่ได้");
        }

        // A replacement identifies the destination duty. The source is always the borrowed person's home.
        if (relief.SourceBuildingId != homeBuildingId)
        {
            return Refused(revisions, key, "ตึกต้นทางที่ส่งมาต้องเป็นตึกต้นทางของสแปร์คนนี้");
        }

        int? inheritedGroup = null;
        if (relief.ReplacedUserId is int replacedUserId)
        {
            if (replacedUserId == relief.UserId)
            {
                return Refused(revisions, key, "มอบหมายให้ตัวเองแทนตัวเองไม่ได้");
            }

            var replaced = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == replacedUserId, ct);
            if (replaced is not { IsActive: true } || replaced.Role != UserRole.Cleaner)
            {
                return Refused(revisions, key, "คนที่ถูกแทนต้องเป็นบัญชีแม่บ้านที่ยังใช้งานอยู่");
            }

            var replacedProfile = await store.ReadProfileAsync(replacedUserId, relief.ShiftDate, ct);
            if (replacedProfile is { Kind: CleanerKind.Relief })
            {
                return Refused(revisions, key, "คนที่ถูกแทนต้องเป็นแม่บ้านประจำ ไม่ใช่สแปร์");
            }

            var day = await store.ReadPersonDayAsync(replacedUserId, relief.ShiftDate, relief.Shift, ct);
            if (day is null || day.Area.Value != relief.AreaId)
            {
                return Refused(revisions, key,
                    $"แผนของคนที่ถูกแทนในวันที่ {Iso(relief.ShiftDate)} ไม่ใช่พื้นที่ {area.Code} จึงไม่มีงานให้แทน");
            }

            inheritedGroup = day.BreakGroup.Value;
        }

        // 5. Spec §5 and A08: a working row needs a group, and nothing may be guessed for standalone work.
        var group = ReliefAssignmentRules.ValidateGroup(inheritedGroup, relief.BreakGroup);
        if (!group.Accepted)
        {
            return Refused(revisions, key, relief.ReplacedUserId is null
                ? "งานสแปร์ที่ไม่ได้แทนใครต้องเลือกกลุ่มพัก 1 หรือ 2 ก่อนบันทึก"
                : "กลุ่มพักต้องเป็น 1 หรือ 2");
        }

        // 6. Spec §6 and A09: the loan gate. The attestation is the destination actor's own note. When the source building
        // belongs to a different responsible Supervisor that day, the row has to name them and carry the note; when the actor
        // is that Supervisor (including one person holding both buildings) nothing extra is asked, and no second approver is
        // invented. Nothing here claims the server verified a conversation.
        var responsible = await store.ResponsibleSupervisorAsync(relief.SourceBuildingId, relief.Shift, relief.ShiftDate, ct);
        if (responsible is not int responsibleId)
        {
            // Ruling: no Supervisor resolves for the source building, shift and date (a vacant or deactivated one, or dated
            // rows that no longer give it to anybody). There is nobody whose agreement could be named, so a named
            // sourceSupervisorId is a row problem rather than an unvalidated fact, and the loan still needs the note.
            if (relief.SourceSupervisorId is not null)
            {
                return Refused(revisions, key, "ตึกต้นทางยังไม่มีหัวหน้าผู้รับผิดชอบในวันนั้น จึงระบุชื่อหัวหน้าตึกต้นทางไม่ได้");
            }

            if (!relief.AgreementReported)
            {
                return Refused(revisions, key,
                    "ตึกต้นทางยังไม่มีหัวหน้าผู้รับผิดชอบ ต้องยืนยันว่าตึกต้นทางตกลงปล่อยคนนอกเว็บแล้ว");
            }
        }
        else if (responsibleId == actor.Id)
        {
            // The actor is the responsible source Supervisor: naming anybody else would describe a person who does not hold
            // the building that day, and no second approver exists to be asked.
            if (relief.SourceSupervisorId is int namedSelf && namedSelf != responsibleId)
            {
                return Refused(revisions, key, "หัวหน้าตึกต้นทางที่ระบุไม่ใช่ผู้รับผิดชอบตึกนั้นในวันนั้น");
            }
        }
        else
        {
            if (relief.SourceSupervisorId != responsibleId)
            {
                return Refused(revisions, key, "ต้องระบุหัวหน้าตึกต้นทางที่รับผิดชอบวันนั้นให้ตรงกับข้อมูลจริง");
            }

            if (!relief.AgreementReported)
            {
                return Refused(revisions, key,
                    "ตึกต้นทางเป็นของหัวหน้าคนอื่น ต้องยืนยันว่าตึกต้นทางตกลงปล่อยคนนอกเว็บแล้ว");
            }
        }

        // The row names the Supervisor it was attested for only when one resolves: nobody else is stored for a loan whose
        // source building had no responsible Supervisor that day.
        var attestedSupervisorId = responsible is null ? null : relief.SourceSupervisorId;

        // 7. The reason: a plan date that has already passed is a retroactive edit and needs one (spec §5).
        var reason = ReadReason(relief.Reason, required: relief.ShiftDate < actor.BusinessDate, out var reasonProblem);
        if (reasonProblem is not null)
        {
            return Refused(revisions, key, reasonProblem);
        }

        // 8. Spec §6: the stored facts that block this slot, asked through the very rule the candidate lookup answers with
        // (ScheduleStore.ReliefConflictsAsync), so a person the list offers is never refused here for a reason the list did
        // not name. Any of them refuses the whole command and leaves every stored row exactly as it was: no automatic
        // cancel, no automatic move, no conversion of a legacy cover row.
        if (await store.ReliefConflictsAsync(relief.UserId, relief.ShiftDate, relief.Shift, ct) is { Count: > 0 } conflicts)
        {
            return Refused(revisions, key, ConflictMessage(conflicts[0]));
        }

        // 9. One stored row, its audit line, and the revision the client can echo for its next edit.
        var created = new ReliefAssignment
        {
            UserId = relief.UserId,
            ShiftDate = relief.ShiftDate,
            Shift = relief.Shift,
            AreaId = relief.AreaId,
            BreakGroup = group.Group,
            ReplacedUserId = relief.ReplacedUserId,
            SourceBuildingId = relief.SourceBuildingId,
            SourceSupervisorId = attestedSupervisorId,
            AgreementReported = relief.AgreementReported,
            Reason = reason,
            CreatedById = request.ActorId,
            CreatedAt = request.Now.UtcDateTime,
            UpdatedAt = request.Now.UtcDateTime,
        };
        db.ReliefAssignments.Add(created);
        db.ScheduleChanges.Add(Change(request, receipt, "ReliefAssignment", null, "create", null,
            Json(new
            {
                revision = Revision(created.Revision),
                userId = created.UserId,
                areaId = created.AreaId,
                shiftDate = Iso(created.ShiftDate),
                shift = created.Shift.ToString(),
                breakGroup = created.BreakGroup,
                replacedUserId = created.ReplacedUserId,
                sourceBuildingId = created.SourceBuildingId,
                sourceSupervisorId = created.SourceSupervisorId,
                agreementReported = created.AgreementReported,

                // Whose note the agreement is: the actor who recorded it. Never a source signature, and never a claim that a
                // conversation was verified on this server (spec §6).
                attestationOf = created.AgreementReported ? request.ActorId : (int?)null,
            }), reason));
        revisions[key] = Revision(created.Revision);
        return new ScheduleCommandApplication(revisions, []);
    }

    /// <summary>
    /// The Thai row message for one machine code <see cref="ScheduleStore.ReliefConflictsAsync"/> answers with. The codes are
    /// the lookup's, so the command and the candidate list cannot drift apart about which stored fact blocks the slot; only
    /// the wording a person sees lives here.
    /// </summary>
    private static string ConflictMessage(string code) => code switch
    {
        ScheduleStore.ReliefExistingAssignment =>
            "มีงานสแปร์ของคนนี้ในกะนี้อยู่แล้ว ต้องจัดงานเดิมให้ถูกก่อนจึงจะบันทึกใหม่ได้",
        ScheduleStore.ReliefOnLeave => "คนนี้มีบันทึกการลาของกะนี้อยู่ จึงยังมอบหมายงานสแปร์ไม่ได้",
        ScheduleStore.ReliefExistingDuty => "คนนี้มีงานประจำในกะนี้อยู่แล้ว ต้องจัดงานเดิมให้ถูกก่อน",
        ScheduleStore.ReliefShiftMismatch => "กะที่ส่งมาไม่ตรงกับกะของคนนี้ในวันนั้น",
        _ => "มีข้อมูลที่ขัดกับการมอบหมายงานสแปร์นี้ ต้องจัดงานเดิมให้ถูกก่อน",
    };
}
