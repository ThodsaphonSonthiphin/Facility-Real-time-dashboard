using System.Globalization;
using System.Text.Json;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §11: the command kinds this task owns. Each one is a DI-resolved <see cref="IScheduleCommandHandler"/>, so the
/// engine can run any of them inside its one transaction without knowing what they store.
/// </summary>
public static class ScheduleCommandKinds
{
    /// <summary>Spec §5 and A04: the day-off choices of one ISO week for the submitted people only.</summary>
    public const string SetWeeklyDaysOff = "set-weekly-days-off";

    /// <summary>ADR facility-0187: a regular repeating week that starts on a chosen future date.</summary>
    public const string SetRegularBaseline = "set-regular-baseline";

    /// <summary>Spec §5 and ADR facility-0220: a relief repeating week of Area/day-off/unset cells.</summary>
    public const string SetReliefBaseline = "set-relief-baseline";

    /// <summary>Spec §5: the Area of one person's one slot on one date.</summary>
    public const string SetDatedArea = "set-dated-area";

    /// <summary>Spec §5 and ADR facility-0189: the first-break group of one person's one slot on one date.</summary>
    public const string SetDatedBreak = "set-dated-break";

    /// <summary>Spec §7: an agreed leave interval, stored apart from any actual scan.</summary>
    public const string RecordLeave = "record-leave";

    /// <summary>Spec §7 and ADR facility-0202: an explicit cancellation that keeps the leave row and its history.</summary>
    public const string CancelLeave = "cancel-leave";

    /// <summary>ADR facility-0214: a reasoned correction of a plan date that has already been planned.</summary>
    public const string CorrectPlan = "correct-plan";
}

/// <summary>
/// Spec §11: the keys a client's expected revisions and a command's answer use. They name the resource a screen read, in
/// the same shape everywhere — the person and entity it concerns, never a bare row number the client cannot see — and every
/// value is the stored revision as a decimal string, so the contract has no second number format. The read DTO exposes the
/// same values (<c>weeklyRevision</c>, <c>regularBaselineRevision</c>, <c>reliefBaselineRevision</c> and each day's
/// <c>area.revision</c>/<c>breakGroup.revision</c>) so a client never has to invent one.
/// </summary>
public static class ScheduleRevisionKeys
{
    /// <summary>The week header of one person's ISO week: 0 when nobody has chosen yet.</summary>
    public static string Week(int userId, DateOnly weekStart) =>
        $"week:{userId}:{Iso(weekStart)}";

    /// <summary>The head version of one person's regular or relief repeating week: 0 when there is no version.</summary>
    public static string Baseline(int userId, BaselineKind kind) =>
        $"baseline:{userId}:{kind.ToString().ToLowerInvariant()}";

    /// <summary>The live dated Area or break-group override of one person's slot: 0 when the slot has none.</summary>
    public static string DatedOverride(int userId, DateOnly date, Shift shift, ScheduleExceptionKind kind) =>
        $"exception:{userId}:{Iso(date)}:{shift}:{kind.ToString().ToLowerInvariant()}";

    /// <summary>The live leave row of one person's slot: 0 when nobody has recorded leave for it.</summary>
    public static string Leave(int userId, DateOnly date, Shift shift) =>
        $"leave:{userId}:{Iso(date)}:{shift}";

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// Spec §11: what every kind in <c>ScheduleCommandHandlers</c> shares — the actor's dated authority, the target roster
/// check, the revision comparison and the audit row. A handler never saves and never commits (the engine owns the one
/// transaction), never writes a second copy of the scope predicate (it asks
/// <see cref="ScheduleAuthority.CanManage"/>/<see cref="ScheduleAuthority.CanCorrectHistory"/>) and answers every refusal a
/// person can fix as a <see cref="RowProblem"/> instead of an exception.
/// </summary>
public abstract class ScheduleCommandHandler(TimeProvider clock) : IScheduleCommandHandler
{
    internal TimeProvider Clock { get; } = clock;

    public abstract string Kind { get; }

    public virtual Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
        Task.FromResult(ScheduleCommandLockSet.None);

    public abstract Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default);

    /// <summary>The actor as it stands now: an active Admin, or an active Supervisor with its dated periods.</summary>
    internal async Task<Actor?> ReadActorAsync(ScheduleCommandRequest request, AppDbContext db, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == request.ActorId, ct);
        if (actor is not { IsActive: true })
        {
            return null;
        }

        var businessDate = ScheduleAuthority.BusinessDate(Clock.GetUtcNow().UtcDateTime);
        if (actor.Role == UserRole.Admin)
        {
            return new Actor(actor.Id, UserRole.Admin, businessDate, []);
        }

        return actor.Role == UserRole.Supervisor
            ? new Actor(actor.Id, UserRole.Supervisor, businessDate, await SupervisorScopes.ReadAsync(db, actor, Clock, ct))
            : null;
    }

    /// <summary>
    /// The target person and the slot their dated identity gives them on <paramref name="date"/>. A target that is not an
    /// active Cleaner, or has no dated identity covering the date, is a row problem: a Supervisor may only write inside the
    /// current authorised target roster, never a person the client named.
    /// </summary>
    internal async Task<(PersonTarget? Target, string? Problem)> ReadTargetAsync(
        AppDbContext db, int userId, DateOnly date, CancellationToken ct)
    {
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId, ct);
        if (person is not { IsActive: true } || person.Role != UserRole.Cleaner)
        {
            return (null, "เป้าหมายต้องเป็นบัญชีแม่บ้านที่ยังใช้งานอยู่");
        }

        var shift = await SlotShiftAsync(db, person, date, ct);
        return shift is null
            ? (null, "ไม่พบกะของคนนี้ในวันที่เลือก จึงยังแก้ตารางให้ไม่ได้")
            : (shift, null);
    }

    /// <summary>
    /// The person's dated identity on a date: the profile period covering it, else — only when the person has no dated
    /// row at all — the compatibility projection of spec §14. Never a guessed history.
    /// </summary>
    private static async Task<PersonTarget?> SlotShiftAsync(AppDbContext db, User person, DateOnly date, CancellationToken ct)
    {
        var profiles = await db.CleanerProfilePeriods.AsNoTracking()
            .Where(profile => profile.UserId == person.Id)
            .OrderBy(profile => profile.EffectiveFrom).ThenBy(profile => profile.Id)
            .ToListAsync(ct);
        var profile = profiles.LastOrDefault(entry =>
            ScheduleResolver.Applies(entry.EffectiveFrom, entry.EffectiveUntil, date));
        if (profile is not null)
        {
            return new PersonTarget(person, profile.Shift,
                profile.HomeBuildingId ?? await BaselineBuildingAsync(db, person.Id, date, ct));
        }

        if (profiles.Count > 0 || person.Shift is not Shift shift)
        {
            return null;
        }

        var building = person.AreaId is int areaId
            ? await db.Areas.AsNoTracking().Where(area => area.Id == areaId)
                .Select(area => (int?)area.BuildingId).FirstOrDefaultAsync(ct)
            : person.BuildingId;
        return new PersonTarget(person, shift, building);
    }

    /// <summary>The building of the Area the person's applicable baseline version works in, when their identity has none.</summary>
    private static async Task<int?> BaselineBuildingAsync(AppDbContext db, int userId, DateOnly date, CancellationToken ct)
    {
        var areaId = await db.ScheduleBaselines.AsNoTracking()
            .Where(baseline => baseline.UserId == userId
                && baseline.EffectiveFrom <= date
                && (baseline.EffectiveUntil == null || date < baseline.EffectiveUntil.Value))
            .OrderBy(baseline => baseline.EffectiveFrom).ThenBy(baseline => baseline.Id)
            .SelectMany(baseline => baseline.Days)
            .Where(day => day.AreaId != null)
            .OrderBy(day => day.Weekday)
            .Select(day => day.AreaId)
            .FirstOrDefaultAsync(ct);
        return areaId is int id
            ? await db.Areas.AsNoTracking().Where(area => area.Id == id)
                .Select(area => (int?)area.BuildingId).FirstOrDefaultAsync(ct)
            : null;
    }

    internal static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A stored revision as the decimal string the whole contract uses (0 when nothing is stored).</summary>
    internal static string Revision(long? revision) =>
        (revision ?? 0).ToString(CultureInfo.InvariantCulture);

    internal static ScheduleCommandApplication Refused(
        IReadOnlyDictionary<string, string> revisions, string rowId, string message) =>
        new(revisions, [new RowProblem(rowId, ScheduleCommandErrorCode.RowInvalid, message)]);

    internal static RowProblem Stale(string rowId, string expected, string current) =>
        new(rowId, ScheduleCommandErrorCode.StaleRevision,
            $"เวอร์ชันที่อ่านไว้ ({expected}) ไม่ตรงกับปัจจุบัน ({current}) กรุณาโหลดใหม่แล้วตรวจอีกครั้ง");

    /// <summary>True when the client supplied this key and its value is not what the row holds now (A18).</summary>
    internal static bool IsStale(ScheduleCommandRequest request, string key, string current, out string expected) =>
        request.ExpectedRevisions.TryGetValue(key, out expected!) && !string.Equals(expected, current, StringComparison.Ordinal);

    /// <summary>One audit row of the command, written in the same transaction as the fact it describes (ADR 0214).</summary>
    internal static ScheduleChange Change(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, string entityType, int? entityId,
        string operation, string? beforeJson, string? afterJson, string? reason = null) =>
        new()
        {
            Receipt = receipt,
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            Reason = reason,
            CreatedAt = request.Now.UtcDateTime,
        };

    internal static string Json(object value) => JsonSerializer.Serialize(value);

    /// <summary>
    /// A reason a person typed: trimmed, 1–500 characters when it is required, and never longer than the stored column when
    /// it is optional. Returns the trimmed text, or null with a message when it is unusable.
    /// </summary>
    internal static string? ReadReason(string? reason, bool required, out string? problem)
    {
        var trimmed = reason?.Trim();
        problem = trimmed switch
        {
            null or "" when required => "ต้องระบุเหตุผลประกอบการแก้ไขแผนย้อนหลัง",
            { Length: > 500 } => "เหตุผลยาวเกิน 500 ตัวอักษร",
            _ => null,
        };
        return problem is null ? (string.IsNullOrEmpty(trimmed) ? null : trimmed) : null;
    }

    /// <summary>
    /// The actor's dated authority as it stands now (spec §3). <see cref="CanChange"/> asks the stricter question for a date
    /// that has already happened: the responsibility of that date <em>and</em> the one held today, so a Supervisor who has
    /// just received a building never reaches backwards.
    /// </summary>
    internal sealed record Actor(int Id, UserRole Role, DateOnly BusinessDate, IReadOnlyList<ScopePeriod> Periods)
    {
        public bool CanManage(int buildingId, Shift shift, DateOnly date) =>
            ScheduleAuthority.CanManage(Role, Id, buildingId, shift, date, Periods);

        public bool CanChange(int buildingId, Shift shift, DateOnly date) =>
            date < BusinessDate
                ? ScheduleAuthority.CanCorrectHistory(Role, Id, buildingId, shift, date, BusinessDate, Periods)
                : CanManage(buildingId, shift, date);
    }

    /// <summary>The target person and the shift/building their dated identity gives them on the edited date.</summary>
    internal sealed record PersonTarget(User Person, Shift Shift, int? BuildingId);
}

/// <summary>
/// Spec §5, §11 and A04: one atomic batch of weekly day-off choices for the people the client submitted. A person who is not
/// submitted keeps whatever they had — unset stays unset — and only the submitted choices change.
/// </summary>
public sealed class SetWeeklyDaysOffHandler(TimeProvider clock) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.SetWeeklyDaysOff;

    public override async Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadWeekly(request.Payload, out var batch))
        {
            return ScheduleCommandLockSet.None;
        }

        var weekStart = ScheduleRules.WeekStart(batch.WeekStart);
        var slots = new List<ScheduleSlotKey>();
        var scopes = new List<ScheduleScopeKey>();
        foreach (var row in batch.Rows)
        {
            var (target, _) = await ReadTargetAsync(db, row.UserId, weekStart, ct);
            if (target is null)
            {
                continue;
            }

            // Controller ruling: the week header has no anchor of its own, so the slot of every submitted date and the
            // person's regular building/shift are what two Supervisors of the same roster actually contend on.
            slots.AddRange(row.OffDates.Select(date => new ScheduleSlotKey(row.UserId, date, target.Shift)));
            if (target.BuildingId is int buildingId)
            {
                scopes.Add(new ScheduleScopeKey(buildingId, target.Shift));
            }
        }

        return new ScheduleCommandLockSet(slots, [], scopes);
    }

    public override async Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadWeekly(request.Payload, out var batch))
        {
            return Refused(new Dictionary<string, string>(), "payload", "ข้อมูลคำสั่งไม่ครบถ้วน");
        }

        var actor = await ReadActorAsync(request, db, ct);
        if (actor is null)
        {
            return Refused(new Dictionary<string, string>(), "actor", "บัญชีนี้ไม่มีสิทธิ์จัดตารางงาน");
        }

        var weekStart = ScheduleRules.WeekStart(batch.WeekStart);
        var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var planned = new List<(int UserId, DateOnly WeekStart, SortedSet<DateOnly> OffDates, WeeklyDayOff? Header)>();
        var seen = new HashSet<int>();
        foreach (var row in batch.Rows)
        {
            var key = ScheduleRevisionKeys.Week(row.UserId, weekStart);
            var (target, problem) = await ReadTargetAsync(db, row.UserId, weekStart, ct);
            if (target is null)
            {
                return Refused(revisions, key, problem!);
            }

            if (!seen.Add(row.UserId))
            {
                return Refused(revisions, key, "มีคนเดิมซ้ำในคำสั่งเดียว");
            }

            var offDates = new SortedSet<DateOnly>();
            foreach (var date in row.OffDates)
            {
                if (date < weekStart || date >= weekStart.AddDays(7))
                {
                    return Refused(revisions, key, $"วันที่ {Iso(date)} อยู่นอกสัปดาห์ที่ส่งมา");
                }

                if (target.BuildingId is not int buildingId)
                {
                    return Refused(revisions, key, "ยังไม่ทราบตึกของคนนี้ในสัปดาห์นี้ จึงยืนยันสิทธิ์ไม่ได้");
                }

                if (!actor.CanChange(buildingId, target.Shift, date))
                {
                    return Refused(revisions, key, $"ไม่มีสิทธิ์แก้ตารางของคนนี้ในวันที่ {Iso(date)}");
                }

                offDates.Add(date);
            }

            var header = await db.WeeklyDayOffs.Include(week => week.Choices)
                .FirstOrDefaultAsync(week => week.UserId == row.UserId && week.WeekStart == weekStart, ct);
            var current = Revision(header?.Revision);
            revisions[key] = current;
            if (IsStale(request, key, current, out var expected))
            {
                return new ScheduleCommandApplication(revisions, [Stale(key, expected, current)]);
            }

            planned.Add((row.UserId, weekStart, offDates, header));
        }

        foreach (var row in planned)
        {
            var key = ScheduleRevisionKeys.Week(row.UserId, row.WeekStart);
            var submitted = row.OffDates.ToList();
            if (row.Header is { } header)
            {
                // Ruling: a submitted row whose stored fact is already the submitted value leaves history untouched.
                if (header.Choices.Select(choice => choice.OffDate).ToHashSet().SetEquals(submitted))
                {
                    continue;
                }

                var before = WeekJson(header.Revision, header.Choices.Select(choice => choice.OffDate));
                header.Choices.Clear();
                foreach (var date in submitted)
                {
                    header.Choices.Add(new WeeklyDayOffChoice { OffDate = date });
                }

                // ADR facility-0190 and controller ruling 4: the week digest moves only when the header revision moves.
                header.Revision += 1;
                header.UpdatedAt = request.Now.UtcDateTime;
                db.ScheduleChanges.Add(Change(request, receipt, "WeeklyDayOff", header.Id, "update",
                    before, WeekJson(header.Revision, submitted)));
                revisions[key] = Revision(header.Revision);
                continue;
            }

            var created = new WeeklyDayOff
            {
                UserId = row.UserId,
                WeekStart = row.WeekStart,
                CreatedById = request.ActorId,
                CreatedAt = request.Now.UtcDateTime,
                UpdatedAt = request.Now.UtcDateTime,
            };
            foreach (var date in submitted)
            {
                created.Choices.Add(new WeeklyDayOffChoice { OffDate = date });
            }

            db.WeeklyDayOffs.Add(created);
            db.ScheduleChanges.Add(Change(request, receipt, "WeeklyDayOff", null, "create",
                null, WeekJson(created.Revision, submitted)));
            revisions[key] = Revision(created.Revision);
        }

        return new ScheduleCommandApplication(revisions, []);
    }

    private static string WeekJson(long revision, IEnumerable<DateOnly> offDates) => Json(new
    {
        revision = Revision(revision),
        offDates = offDates.OrderBy(date => date).Select(Iso).ToArray(),
    });
}

/// <summary>
/// ADR facility-0187: the regular repeating week may only be moved forward from a chosen future date. The earlier version
/// keeps its own dates and merely stops where the new one starts, so nothing already planned changes retroactively.
/// </summary>
public sealed class SetRegularBaselineHandler(TimeProvider clock) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.SetRegularBaseline;

    public override Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
        ScheduleBaselineWrite.PlanLocksAsync(request, db, BaselineKind.Regular, ct);

    public override Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default) =>
        ScheduleBaselineWrite.ApplyAsync(this, request, receipt, db, BaselineKind.Regular, ct);
}

/// <summary>
/// Spec §5 and ADRs facility-0152/0220: a relief repeating week is Area/day-off/unset cells. A relief's first-break group is
/// inherited from the person they replace, so a pattern cell never stores one.
/// </summary>
public sealed class SetReliefBaselineHandler(TimeProvider clock) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.SetReliefBaseline;

    public override Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
        ScheduleBaselineWrite.PlanLocksAsync(request, db, BaselineKind.Relief, ct);

    public override Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default) =>
        ScheduleBaselineWrite.ApplyAsync(this, request, receipt, db, BaselineKind.Relief, ct);
}

/// <summary>Spec §5: one person's slot on one date gets an explicit Area; a no-op edit writes nothing.</summary>
public sealed class SetDatedAreaHandler(TimeProvider clock) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.SetDatedArea;

    public override Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
        ScheduleDatedWrite.PlanLocksAsync(request, db, ct);

    public override Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default) =>
        ScheduleDatedWrite.ApplyAsync(this, request, receipt, db,
            [ScheduleExceptionKind.Area], correction: false, ct);
}

/// <summary>Spec §5 and ADR facility-0189: the first-break group of one slot on one date, kept apart from the version.</summary>
public sealed class SetDatedBreakHandler(TimeProvider clock) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.SetDatedBreak;

    public override Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
        ScheduleDatedWrite.PlanLocksAsync(request, db, ct);

    public override Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default) =>
        ScheduleDatedWrite.ApplyAsync(this, request, receipt, db,
            [ScheduleExceptionKind.BreakGroup], correction: false, ct);
}

/// <summary>
/// Spec §7: an agreed leave interval stored as its own record. It never writes attendance, never closes an Area and never
/// cancels itself; a slot holds one live leave row so a later cancellation is unambiguous.
/// </summary>
public sealed class RecordLeaveHandler(TimeProvider clock) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.RecordLeave;

    public override Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
        ScheduleDatedWrite.PlanLocksAsync(request, db, ct);

    public override async Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadLeave(request.Payload, out var leave))
        {
            return Refused(new Dictionary<string, string>(), "payload", "ข้อมูลวันลาไม่ครบถ้วน");
        }

        var actor = await ReadActorAsync(request, db, ct);
        if (actor is null)
        {
            return Refused(new Dictionary<string, string>(), "actor", "บัญชีนี้ไม่มีสิทธิ์บันทึกการลา");
        }

        var key = ScheduleRevisionKeys.Leave(leave.UserId, leave.Date, leave.Shift);
        var (target, targetProblem) = await ReadTargetAsync(db, leave.UserId, leave.Date, ct);
        if (target is null)
        {
            return Refused(new Dictionary<string, string>(), key, targetProblem!);
        }

        if (target.Shift != leave.Shift)
        {
            return Refused(new Dictionary<string, string>(), key, "กะที่ส่งมาไม่ตรงกับกะของคนนี้ในวันนั้น");
        }

        if (target.BuildingId is not int buildingId || !actor.CanChange(buildingId, target.Shift, leave.Date))
        {
            return Refused(new Dictionary<string, string>(), key, $"ไม่มีสิทธิ์บันทึกการลาของคนนี้ในวันที่ {Iso(leave.Date)}");
        }

        var reason = ReadReason(leave.Reason, required: false, out var reasonProblem);
        if (reasonProblem is not null)
        {
            return Refused(new Dictionary<string, string>(), key, reasonProblem);
        }

        if (leave.EndsAt <= leave.StartsAt)
        {
            return Refused(new Dictionary<string, string>(), key, "เวลาสิ้นสุดการลาต้องอยู่หลังเวลาเริ่ม");
        }

        // facility-0183: a leave interval belongs to the shift date it starts in, so an interval naming another slot's date
        // is a wrong target rather than a partial leave of this one.
        if (ScheduleAuthority.BusinessDate(leave.StartsAt.UtcDateTime) != leave.Date)
        {
            return Refused(new Dictionary<string, string>(), key, "ช่วงเวลาลาไม่ได้เริ่มในกะของวันที่ส่งมา");
        }

        var slot = new ShiftSlot(leave.Date, leave.Shift);
        var shiftStart = new DateTimeOffset(ShiftCalendar.StartUtc(slot), TimeSpan.Zero);
        var shiftEnd = new DateTimeOffset(ShiftCalendar.EndUtc(slot), TimeSpan.Zero);
        if (leave.StartsAt >= shiftEnd || leave.EndsAt <= shiftStart)
        {
            return Refused(new Dictionary<string, string>(), key, "ช่วงเวลาลาอยู่นอกกะของวันนั้น");
        }

        var live = await LiveLeaveAsync(db, leave.UserId, leave.Date, leave.Shift, ct);
        var current = Revision(live?.Revision);
        var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal) { [key] = current };
        if (IsStale(request, key, current, out var expected))
        {
            return new ScheduleCommandApplication(revisions, [Stale(key, expected, current)]);
        }

        if (live is not null)
        {
            return Refused(revisions, key, "มีบันทึกการลาของกะนี้อยู่แล้ว ต้องยกเลิกก่อนจึงจะบันทึกใหม่ได้");
        }

        var created = new ScheduleException
        {
            UserId = leave.UserId,
            Kind = ScheduleExceptionKind.Leave,
            ShiftDate = leave.Date,
            Shift = leave.Shift,
            StartsAt = leave.StartsAt.UtcDateTime,
            EndsAt = leave.EndsAt.UtcDateTime,
            Reason = reason,
            CreatedById = request.ActorId,
            CreatedAt = request.Now.UtcDateTime,
            UpdatedAt = request.Now.UtcDateTime,
        };
        db.ScheduleExceptions.Add(created);
        db.ScheduleChanges.Add(Change(request, receipt, "ScheduleException", null, "create", null,
            Json(new
            {
                revision = Revision(created.Revision),
                kind = "leave",
                startsAt = created.StartsAt,
                endsAt = created.EndsAt,
            }), reason));
        revisions[key] = Revision(created.Revision);
        return new ScheduleCommandApplication(revisions, []);
    }

    internal static Task<ScheduleException?> LiveLeaveAsync(
        AppDbContext db, int userId, DateOnly date, Shift shift, CancellationToken ct) =>
        db.ScheduleExceptions.FirstOrDefaultAsync(row => row.UserId == userId
            && row.ShiftDate == date && row.Shift == shift
            && row.Kind == ScheduleExceptionKind.Leave && !row.IsCancelled, ct);
}

/// <summary>Spec §7 and ADR facility-0202: cancelling keeps the row, its interval and its history instead of deleting it.</summary>
public sealed class CancelLeaveHandler(TimeProvider clock) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.CancelLeave;

    public override Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
        ScheduleDatedWrite.PlanLocksAsync(request, db, ct);

    public override async Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default)
    {
        if (!SchedulePayload.TryReadLeaveSlot(request.Payload, out var slotPayload))
        {
            return Refused(new Dictionary<string, string>(), "payload", "ข้อมูลการยกเลิกวันลาไม่ครบถ้วน");
        }

        var actor = await ReadActorAsync(request, db, ct);
        if (actor is null)
        {
            return Refused(new Dictionary<string, string>(), "actor", "บัญชีนี้ไม่มีสิทธิ์ยกเลิกการลา");
        }

        var key = ScheduleRevisionKeys.Leave(slotPayload.UserId, slotPayload.Date, slotPayload.Shift);
        var (target, targetProblem) = await ReadTargetAsync(db, slotPayload.UserId, slotPayload.Date, ct);
        if (target is null)
        {
            return Refused(new Dictionary<string, string>(), key, targetProblem!);
        }

        if (target.Shift != slotPayload.Shift)
        {
            return Refused(new Dictionary<string, string>(), key, "กะที่ส่งมาไม่ตรงกับกะของคนนี้ในวันนั้น");
        }

        if (target.BuildingId is not int buildingId || !actor.CanChange(buildingId, target.Shift, slotPayload.Date))
        {
            return Refused(new Dictionary<string, string>(), key,
                $"ไม่มีสิทธิ์ยกเลิกการลาของคนนี้ในวันที่ {Iso(slotPayload.Date)}");
        }

        var reason = ReadReason(slotPayload.Reason, required: false, out var reasonProblem);
        if (reasonProblem is not null)
        {
            return Refused(new Dictionary<string, string>(), key, reasonProblem);
        }

        var live = await RecordLeaveHandler.LiveLeaveAsync(db, slotPayload.UserId, slotPayload.Date, slotPayload.Shift, ct);
        var current = Revision(live?.Revision);
        var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal) { [key] = current };
        if (IsStale(request, key, current, out var expected))
        {
            return new ScheduleCommandApplication(revisions, [Stale(key, expected, current)]);
        }

        if (live is null)
        {
            return Refused(revisions, key, "ไม่พบบันทึกการลาที่ยังไม่ถูกยกเลิกในกะนี้");
        }

        var before = Json(new { revision = Revision(live.Revision), cancelled = false });
        live.IsCancelled = true;
        live.CancelledAt = request.Now.UtcDateTime;
        live.CancelledById = request.ActorId;
        live.Revision += 1;
        live.UpdatedAt = request.Now.UtcDateTime;
        if (reason is not null)
        {
            live.Reason = reason;
        }

        db.ScheduleChanges.Add(Change(request, receipt, "ScheduleException", live.Id, "cancel", before,
            Json(new { revision = Revision(live.Revision), cancelled = true }), reason));
        revisions[key] = Revision(live.Revision);
        return new ScheduleCommandApplication(revisions, []);
    }
}

/// <summary>
/// ADR facility-0214 and A28: a deliberate correction of a plan date that has already been planned. It needs a reason, it
/// records the original revision beside the corrected one, it keeps the row's history, and it never touches attendance,
/// scans or inspections — the ordinary baseline editor (ADR facility-0187) is the path that only moves forward.
/// </summary>
public sealed class CorrectPlanHandler(TimeProvider clock) : ScheduleCommandHandler(clock)
{
    public override string Kind => ScheduleCommandKinds.CorrectPlan;

    public override Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
        ScheduleDatedWrite.PlanLocksAsync(request, db, ct);

    public override Task<ScheduleCommandApplication> ValidateAndApplyAsync(
        ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default) =>
        ScheduleDatedWrite.ApplyAsync(this, request, receipt, db,
            [ScheduleExceptionKind.Area, ScheduleExceptionKind.BreakGroup], correction: true, ct);
}

/// <summary>The shared body of the two repeating-week kinds, so the forward-only rule is written once.</summary>
internal static class ScheduleBaselineWrite
{
    internal static async Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, BaselineKind kind, CancellationToken ct)
    {
        if (!SchedulePayload.TryReadBaseline(request.Payload, out var payload))
        {
            return ScheduleCommandLockSet.None;
        }

        var scopes = new List<ScheduleScopeKey>();
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == payload.UserId, ct);
        Shift? shift = null;
        if (person is not null)
        {
            var profiles = await db.CleanerProfilePeriods.AsNoTracking()
                .Where(profile => profile.UserId == payload.UserId)
                .OrderBy(profile => profile.EffectiveFrom).ThenBy(profile => profile.Id)
                .ToListAsync(ct);
            var profile = profiles.LastOrDefault(entry =>
                ScheduleResolver.Applies(entry.EffectiveFrom, entry.EffectiveUntil, payload.EffectiveFrom));
            shift = profile?.Shift ?? person.Shift;
            if (profile is not null && shift is Shift profileShift)
            {
                var building = profile.HomeBuildingId ?? await BuildingOfPersonAreaAsync(db, person, ct);
                if (building is int home)
                {
                    scopes.Add(new ScheduleScopeKey(home, profileShift));
                }
            }
        }

        var areaIds = payload.Days.Where(day => day.AreaId is not null).Select(day => day.AreaId!.Value).Distinct().ToList();
        if (areaIds.Count > 0 && shift is Shift personShift)
        {
            var areas = await db.Areas.AsNoTracking()
                .Where(area => areaIds.Contains(area.Id))
                .Select(area => new { area.Id, area.BuildingId }).ToListAsync(ct);
            scopes.AddRange(areas.Select(area => new ScheduleScopeKey(area.BuildingId, personShift)));
        }

        return new ScheduleCommandLockSet([], [payload.UserId], scopes);
    }

    private static async Task<int?> BuildingOfPersonAreaAsync(AppDbContext db, User person, CancellationToken ct) =>
        person.AreaId is int areaId
            ? await db.Areas.AsNoTracking().Where(area => area.Id == areaId)
                .Select(area => (int?)area.BuildingId).FirstOrDefaultAsync(ct)
            : person.BuildingId;

    internal static async Task<ScheduleCommandApplication> ApplyAsync(
        ScheduleCommandHandler handler, ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db,
        BaselineKind kind, CancellationToken ct)
    {
        if (!SchedulePayload.TryReadBaseline(request.Payload, out var payload))
        {
            return ScheduleCommandHandler.Refused(new Dictionary<string, string>(), "payload",
                "ข้อมูลตารางประจำไม่ครบถ้วน");
        }

        var actor = await handler.ReadActorAsync(request, db, ct);
        if (actor is null)
        {
            return ScheduleCommandHandler.Refused(new Dictionary<string, string>(), "actor",
                "บัญชีนี้ไม่มีสิทธิ์แก้ตารางประจำ");
        }

        var key = ScheduleRevisionKeys.Baseline(payload.UserId, kind);
        var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal);

        // ADR facility-0187: the ordinary editor only moves the pattern forward, from a date after today.
        if (payload.EffectiveFrom <= actor.BusinessDate)
        {
            return ScheduleCommandHandler.Refused(revisions, key,
                $"วันเริ่มใช้ต้องเป็นวันถัดจากวันนี้ ({ScheduleCommandHandler.Iso(actor.BusinessDate)}) ขึ้นไป");
        }

        var (target, targetProblem) = await handler.ReadTargetAsync(db, payload.UserId, payload.EffectiveFrom, ct);
        if (target is null)
        {
            return ScheduleCommandHandler.Refused(revisions, key, targetProblem!);
        }

        if (payload.Days.Count == 0)
        {
            return ScheduleCommandHandler.Refused(revisions, key, "ต้องระบุอย่างน้อยหนึ่งวันของตารางประจำ");
        }

        var cells = new Dictionary<DayOfWeek, SchedulePayload.BaselineCell>();
        foreach (var cell in payload.Days)
        {
            if (!cells.TryAdd(cell.Weekday, cell))
            {
                return ScheduleCommandHandler.Refused(revisions, key, $"วัน {cell.Weekday} ซ้ำในคำสั่งเดียว");
            }

            if (cell.IsDayOff)
            {
                if (cell.AreaId is not null || cell.BreakGroup is not null)
                {
                    return ScheduleCommandHandler.Refused(revisions, key,
                        $"วันหยุด {cell.Weekday} ต้องไม่มี Area หรือกลุ่มพัก");
                }

                continue;
            }

            if (kind == BaselineKind.Relief)
            {
                // ADR facility-0152: a relief's group is the replaced person's, so the pattern never stores one.
                if (cell.BreakGroup is not null)
                {
                    return ScheduleCommandHandler.Refused(revisions, key,
                        $"ช่อง {cell.Weekday} ของสแปร์เก็บกลุ่มพักไม่ได้ เพราะใช้กลุ่มของคนที่ถูกแทน");
                }
            }
            else if (cell.BreakGroup is int group)
            {
                if (group is not (1 or 2))
                {
                    return ScheduleCommandHandler.Refused(revisions, key,
                        $"กลุ่มพักต้องเป็น 1 หรือ 2 (วันที่ {cell.Weekday})");
                }

                if (cell.AreaId is null)
                {
                    return ScheduleCommandHandler.Refused(revisions, key,
                        $"ช่อง {cell.Weekday} มีกลุ่มพักแต่ไม่มี Area");
                }
            }

            if (cell.AreaId is not int areaId)
            {
                continue;
            }

            var area = await db.Areas.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == areaId, ct);
            if (area is null || !area.IsActive)
            {
                return ScheduleCommandHandler.Refused(revisions, key, $"ไม่พบ Area {areaId} ที่ยังใช้งานอยู่");
            }

            if (kind == BaselineKind.Regular && target.BuildingId is int home && area.BuildingId != home)
            {
                return ScheduleCommandHandler.Refused(revisions, key,
                    $"Area {areaId} อยู่นอกตึกของคนนี้; การย้ายตึกต้องใช้คำสั่งย้าย");
            }

            if (!actor.CanManage(area.BuildingId, target.Shift, payload.EffectiveFrom))
            {
                return ScheduleCommandHandler.Refused(revisions, key,
                    $"ไม่มีสิทธิ์จัดตาราง Area {areaId} ในตึกนั้น");
            }
        }

        var versions = await db.ScheduleBaselines.Include(baseline => baseline.Days)
            .Where(baseline => baseline.UserId == payload.UserId && baseline.Kind == kind)
            .OrderBy(baseline => baseline.EffectiveFrom).ThenBy(baseline => baseline.Id)
            .ToListAsync(ct);
        var head = versions.LastOrDefault();
        var current = ScheduleCommandHandler.Revision(head?.Revision);
        revisions[key] = current;
        if (ScheduleCommandHandler.IsStale(request, key, current, out var expected))
        {
            return new ScheduleCommandApplication(revisions,
                [ScheduleCommandHandler.Stale(key, expected, current)]);
        }

        // A second submission of the same pattern is a no-op rather than a second version; a different pattern at a date
        // that already has a version is a correction (ADR facility-0214), not an ordinary baseline edit.
        if (versions.FirstOrDefault(version => version.EffectiveFrom == payload.EffectiveFrom) is { } existing)
        {
            var same = cells.All(cell =>
                existing.Days.FirstOrDefault(day => day.Weekday == cell.Key) is { } stored
                && stored.AreaId == cell.Value.AreaId && stored.BreakGroup == cell.Value.BreakGroup
                && stored.IsDayOff == cell.Value.IsDayOff);
            return same
                ? new ScheduleCommandApplication(revisions, [])
                : ScheduleCommandHandler.Refused(revisions, key,
                    "มีตารางประจำรุ่นที่เริ่มวันนี้อยู่แล้ว ถ้าต้องแก้ย้อนหลังให้ใช้คำสั่งแก้ไขแผนพร้อมเหตุผล");
        }

        var previous = versions.LastOrDefault(version => version.EffectiveFrom < payload.EffectiveFrom);
        var next = versions.FirstOrDefault(version => version.EffectiveFrom > payload.EffectiveFrom);
        var created = new ScheduleBaseline
        {
            UserId = payload.UserId,
            Kind = kind,
            EffectiveFrom = payload.EffectiveFrom,
            EffectiveUntil = next?.EffectiveFrom,
            CreatedById = request.ActorId,
            CreatedAt = request.Now.UtcDateTime,
            UpdatedAt = request.Now.UtcDateTime,
        };

        // The weekdays the client did not submit keep the cells they already had, so a one-day edit never clears the week.
        var inherited = previous?.Days.ToDictionary(day => day.Weekday) ?? [];
        foreach (var weekday in Enum.GetValues<DayOfWeek>())
        {
            if (cells.TryGetValue(weekday, out var cell))
            {
                created.Days.Add(new ScheduleBaselineDay
                {
                    Weekday = weekday, AreaId = cell.AreaId, BreakGroup = cell.BreakGroup, IsDayOff = cell.IsDayOff,
                });
            }
            else if (inherited.TryGetValue(weekday, out var stored))
            {
                created.Days.Add(new ScheduleBaselineDay
                {
                    Weekday = weekday, AreaId = stored.AreaId, BreakGroup = stored.BreakGroup, IsDayOff = stored.IsDayOff,
                });
            }
        }

        db.ScheduleBaselines.Add(created);
        db.ScheduleChanges.Add(ScheduleCommandHandler.Change(request, receipt, "ScheduleBaseline", null, "create",
            null, VersionJson(created)));

        if (previous is not null
            && (previous.EffectiveUntil is null || previous.EffectiveUntil.Value > payload.EffectiveFrom))
        {
            // ADR facility-0173: the earlier version keeps its own dates; only its end moves, and its revision moves with it.
            var before = VersionRangeJson(previous);
            previous.EffectiveUntil = payload.EffectiveFrom;
            previous.Revision += 1;
            previous.UpdatedAt = request.Now.UtcDateTime;
            db.ScheduleChanges.Add(ScheduleCommandHandler.Change(request, receipt, "ScheduleBaseline",
                previous.Id, "update", before, VersionRangeJson(previous)));
        }

        revisions[key] = ScheduleCommandHandler.Revision(next?.Revision ?? created.Revision);
        return new ScheduleCommandApplication(revisions, []);
    }

    private static string VersionJson(ScheduleBaseline version) => ScheduleCommandHandler.Json(new
    {
        revision = ScheduleCommandHandler.Revision(version.Revision),
        effectiveFrom = ScheduleCommandHandler.Iso(version.EffectiveFrom),
        effectiveUntil = version.EffectiveUntil is DateOnly until ? ScheduleCommandHandler.Iso(until) : null,
        days = version.Days.OrderBy(day => day.Weekday).Select(day => new
        {
            weekday = day.Weekday.ToString(),
            areaId = day.AreaId,
            breakGroup = day.BreakGroup,
            isDayOff = day.IsDayOff,
        }).ToList(),
    });

    private static string VersionRangeJson(ScheduleBaseline version) => ScheduleCommandHandler.Json(new
    {
        revision = ScheduleCommandHandler.Revision(version.Revision),
        effectiveFrom = ScheduleCommandHandler.Iso(version.EffectiveFrom),
        effectiveUntil = version.EffectiveUntil is DateOnly until ? ScheduleCommandHandler.Iso(until) : null,
    });
}

/// <summary>The shared body of the dated Area/break-group edits and the reasoned correction of a past one.</summary>
internal static class ScheduleDatedWrite
{
    internal static async Task<ScheduleCommandLockSet> PlanLocksAsync(
        ScheduleCommandRequest request, AppDbContext db, CancellationToken ct)
    {
        if (!SchedulePayload.TryReadDatedSlot(request.Payload, out var slot))
        {
            return ScheduleCommandLockSet.None;
        }

        var scopes = new List<ScheduleScopeKey>();
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == slot.UserId, ct);
        if (person?.BuildingId is int projected)
        {
            scopes.Add(new ScheduleScopeKey(projected, slot.Shift));
        }

        if (person?.AreaId is int areaId)
        {
            var building = await db.Areas.AsNoTracking().Where(area => area.Id == areaId)
                .Select(area => (int?)area.BuildingId).FirstOrDefaultAsync(ct);
            if (building is int fromArea)
            {
                scopes.Add(new ScheduleScopeKey(fromArea, slot.Shift));
            }
        }

        return new ScheduleCommandLockSet([new ScheduleSlotKey(slot.UserId, slot.Date, slot.Shift)], [], scopes);
    }

    internal static async Task<ScheduleCommandApplication> ApplyAsync(
        ScheduleCommandHandler handler, ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db,
        IReadOnlyList<ScheduleExceptionKind> kinds, bool correction, CancellationToken ct)
    {
        if (!SchedulePayload.TryReadDatedSlot(request.Payload, out var slot))
        {
            return ScheduleCommandHandler.Refused(new Dictionary<string, string>(), "payload",
                "ข้อมูลการแก้ตารางไม่ครบถ้วน");
        }

        var actor = await handler.ReadActorAsync(request, db, ct);
        if (actor is null)
        {
            return ScheduleCommandHandler.Refused(new Dictionary<string, string>(), "actor",
                "บัญชีนี้ไม่มีสิทธิ์แก้ตารางงาน");
        }

        var key = ScheduleRevisionKeys.DatedOverride(slot.UserId, slot.Date, slot.Shift, kinds[0]);
        var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal);

        var reason = ScheduleCommandHandler.ReadReason(slot.Reason, correction, out var reasonProblem);
        if (reasonProblem is not null)
        {
            return ScheduleCommandHandler.Refused(revisions, key, reasonProblem);
        }

        // A correction is a correction of something already planned, and a plan date that has passed is only corrected by
        // the reasoned command (ADR facility-0214); the two paths never overlap, so neither can bypass the other's rule.
        if (correction && slot.Date > actor.BusinessDate)
        {
            return ScheduleCommandHandler.Refused(revisions, key,
                "แก้ไขแผนใช้กับวันที่ผ่านมาแล้วเท่านั้น; วันข้างหน้าให้แก้ที่ตารางปกติ");
        }

        if (!correction && slot.Date < actor.BusinessDate)
        {
            return ScheduleCommandHandler.Refused(revisions, key,
                "วันที่ผ่านมาแล้วต้องแก้ด้วยคำสั่งแก้ไขแผนพร้อมเหตุผล");
        }

        if (correction && !slot.HasArea && !slot.HasBreakGroup)
        {
            return ScheduleCommandHandler.Refused(revisions, key, "ต้องระบุ Area หรือกลุ่มพักที่จะแก้ไข");
        }

        var (target, targetProblem) = await handler.ReadTargetAsync(db, slot.UserId, slot.Date, ct);
        if (target is null)
        {
            return ScheduleCommandHandler.Refused(revisions, key, targetProblem!);
        }

        if (target.Shift != slot.Shift)
        {
            return ScheduleCommandHandler.Refused(revisions, key, "กะที่ส่งมาไม่ตรงกับกะของคนนี้ในวันนั้น");
        }

        if (target.BuildingId is not int buildingId || !actor.CanChange(buildingId, target.Shift, slot.Date))
        {
            return ScheduleCommandHandler.Refused(revisions, key,
                $"ไม่มีสิทธิ์แก้ตารางของคนนี้ในวันที่ {ScheduleCommandHandler.Iso(slot.Date)}");
        }

        if (slot.HasArea && slot.AreaId is int areaId)
        {
            var area = await db.Areas.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == areaId, ct);
            if (area is null || !area.IsActive)
            {
                return ScheduleCommandHandler.Refused(revisions, key, $"ไม่พบ Area {areaId} ที่ยังใช้งานอยู่");
            }

            if (area.BuildingId != buildingId)
            {
                return ScheduleCommandHandler.Refused(revisions, key,
                    $"Area {areaId} อยู่นอกตึกของคนนี้; การย้ายตึกใช้คำสั่งย้ายพื้นที่");
            }
        }

        if (slot.HasBreakGroup && slot.BreakGroup is int group && group is not (1 or 2))
        {
            return ScheduleCommandHandler.Refused(revisions, key, "กลุ่มพักต้องเป็น 1 หรือ 2");
        }

        foreach (var kind in kinds)
        {
            var value = kind == ScheduleExceptionKind.Area ? slot.AreaId : slot.BreakGroup;
            if (kind == ScheduleExceptionKind.Area && !slot.HasArea)
            {
                continue;
            }

            if (kind == ScheduleExceptionKind.BreakGroup && !slot.HasBreakGroup)
            {
                continue;
            }

            var kindKey = ScheduleRevisionKeys.DatedOverride(slot.UserId, slot.Date, slot.Shift, kind);
            var live = await db.ScheduleExceptions.FirstOrDefaultAsync(row => row.UserId == slot.UserId
                && row.ShiftDate == slot.Date && row.Shift == slot.Shift
                && row.Kind == kind && !row.IsCancelled, ct);
            var current = ScheduleCommandHandler.Revision(live?.Revision);
            revisions[kindKey] = current;
            if (ScheduleCommandHandler.IsStale(request, kindKey, current, out var expected))
            {
                return new ScheduleCommandApplication(revisions,
                    [ScheduleCommandHandler.Stale(kindKey, expected, current)]);
            }

            var stored = kind == ScheduleExceptionKind.Area ? live?.AreaId : live?.BreakGroup;
            if (live is null && value is null)
            {
                continue; // nothing to remove and nothing to write
            }

            if (live is not null && stored == value)
            {
                // Ruling: a submitted row whose stored fact already equals the submitted value keeps the history it has.
                continue;
            }

            if (value is null)
            {
                var before = DatedJson(live!, current);
                live!.IsCancelled = true;
                live.CancelledAt = request.Now.UtcDateTime;
                live.CancelledById = request.ActorId;
                live.Revision += 1;
                live.UpdatedAt = request.Now.UtcDateTime;
                if (reason is not null)
                {
                    live.Reason = reason;
                }

                db.ScheduleChanges.Add(ScheduleCommandHandler.Change(request, receipt, "ScheduleException",
                    live.Id, "cancel", before, DatedJson(live, current), reason));
                revisions[kindKey] = ScheduleCommandHandler.Revision(live.Revision);
                continue;
            }

            if (live is null)
            {
                var created = new ScheduleException
                {
                    UserId = slot.UserId,
                    Kind = kind,
                    ShiftDate = slot.Date,
                    Shift = slot.Shift,
                    AreaId = kind == ScheduleExceptionKind.Area ? value : null,
                    BreakGroup = kind == ScheduleExceptionKind.BreakGroup ? value : null,
                    Reason = reason,
                    CreatedById = request.ActorId,
                    CreatedAt = request.Now.UtcDateTime,
                    UpdatedAt = request.Now.UtcDateTime,
                };
                db.ScheduleExceptions.Add(created);
                db.ScheduleChanges.Add(ScheduleCommandHandler.Change(request, receipt, "ScheduleException",
                    null, "create", null, DatedJson(created, current, correction), reason));
                revisions[kindKey] = ScheduleCommandHandler.Revision(created.Revision);
                continue;
            }

            var original = DatedJson(live, current);
            if (kind == ScheduleExceptionKind.Area)
            {
                live.AreaId = value;
            }
            else
            {
                live.BreakGroup = value;
            }

            live.Revision += 1;
            live.UpdatedAt = request.Now.UtcDateTime;
            if (reason is not null)
            {
                live.Reason = reason;
            }

            db.ScheduleChanges.Add(ScheduleCommandHandler.Change(request, receipt, "ScheduleException",
                live.Id, "update", original, DatedJson(live, current, correction), reason));
            revisions[kindKey] = ScheduleCommandHandler.Revision(live.Revision);
        }

        return new ScheduleCommandApplication(revisions, []);
    }

    /// <summary>
    /// The row as the audit records it. A correction also names the revision it started from and the one it produced, so the
    /// original plan and the corrected plan can be shown apart (ADR facility-0214, A28).
    /// </summary>
    private static string DatedJson(ScheduleException row, string originalRevision, bool correction = false) =>
        ScheduleCommandHandler.Json(new
        {
            revision = ScheduleCommandHandler.Revision(row.Revision),
            originalRevision = correction ? originalRevision : null,
            correctedRevision = correction ? ScheduleCommandHandler.Revision(row.Revision) : null,
            cancelled = row.IsCancelled,
            areaId = row.AreaId,
            breakGroup = row.BreakGroup,
        });
}

/// <summary>
/// Spec §11: the payload shapes the kinds above read. Every one of them is defensive — a body a client got wrong is a row
/// problem with a message, never a server error — and every batch is an array so one command can carry a partial roster.
/// </summary>
internal static class SchedulePayload
{
    internal sealed record WeeklyRow(int UserId, IReadOnlyList<DateOnly> OffDates);

    internal sealed record WeeklyBatch(DateOnly WeekStart, IReadOnlyList<WeeklyRow> Rows);

    internal sealed record BaselineCell(DayOfWeek Weekday, int? AreaId, int? BreakGroup, bool IsDayOff);

    internal sealed record BaselineRequest(int UserId, DateOnly EffectiveFrom, IReadOnlyList<BaselineCell> Days);

    internal sealed record DatedSlot(
        int UserId, DateOnly Date, Shift Shift, bool HasArea, int? AreaId, bool HasBreakGroup, int? BreakGroup,
        string? Reason);

    internal sealed record LeaveRequest(
        int UserId, DateOnly Date, Shift Shift, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason);

    internal sealed record LeaveSlot(int UserId, DateOnly Date, Shift Shift, string? Reason);

    internal static bool TryReadWeekly(JsonElement payload, out WeeklyBatch batch)
    {
        batch = null!;
        if (!TryDate(payload, "weekStart", out var weekStart))
        {
            return false;
        }

        if (!TryProperty(payload, "rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsed = new List<WeeklyRow>();
        foreach (var row in rows.EnumerateArray())
        {
            if (!TryInt(row, "userId", out var userId)
                || !TryProperty(row, "offDates", out var dates) || dates.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var offDates = new List<DateOnly>();
            foreach (var date in dates.EnumerateArray())
            {
                if (!TryDate(date, out var day))
                {
                    return false;
                }

                offDates.Add(day);
            }

            parsed.Add(new WeeklyRow(userId, offDates));
        }

        batch = new WeeklyBatch(weekStart, parsed);
        return true;
    }

    internal static bool TryReadBaseline(JsonElement payload, out BaselineRequest baseline)
    {
        baseline = null!;
        if (!TryInt(payload, "userId", out var userId)
            || !TryDate(payload, "effectiveFrom", out var effectiveFrom)
            || !TryProperty(payload, "days", out var days) || days.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var cells = new List<BaselineCell>();
        foreach (var day in days.EnumerateArray())
        {
            if (!TryProperty(day, "weekday", out var weekdayElement)
                || weekdayElement.ValueKind != JsonValueKind.String
                || !Enum.TryParse<DayOfWeek>(weekdayElement.GetString(), ignoreCase: true, out var weekday))
            {
                return false;
            }

            cells.Add(new BaselineCell(weekday, OptionalInt(day, "areaId"), OptionalInt(day, "breakGroup"),
                OptionalBool(day, "isDayOff") ?? false));
        }

        baseline = new BaselineRequest(userId, effectiveFrom, cells);
        return true;
    }

    internal static bool TryReadDatedSlot(JsonElement payload, out DatedSlot slot)
    {
        slot = null!;
        if (!TryInt(payload, "userId", out var userId)
            || !TryDate(payload, "date", out var date)
            || !TryShift(payload, out var shift))
        {
            return false;
        }

        slot = new DatedSlot(userId, date, shift, Has(payload, "areaId"), OptionalInt(payload, "areaId"),
            Has(payload, "breakGroup"), OptionalInt(payload, "breakGroup"), OptionalString(payload, "reason"));
        return true;
    }

    internal static bool TryReadLeave(JsonElement payload, out LeaveRequest leave)
    {
        leave = null!;
        if (!TryInt(payload, "userId", out var userId)
            || !TryDate(payload, "date", out var date)
            || !TryShift(payload, out var shift)
            || !TryInstant(payload, "startsAt", out var startsAt)
            || !TryInstant(payload, "endsAt", out var endsAt))
        {
            return false;
        }

        leave = new LeaveRequest(userId, date, shift, startsAt, endsAt, OptionalString(payload, "reason"));
        return true;
    }

    internal static bool TryReadLeaveSlot(JsonElement payload, out LeaveSlot slot)
    {
        slot = null!;
        if (!TryInt(payload, "userId", out var userId)
            || !TryDate(payload, "date", out var date)
            || !TryShift(payload, out var shift))
        {
            return false;
        }

        slot = new LeaveSlot(userId, date, shift, OptionalString(payload, "reason"));
        return true;
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value);
    }

    private static bool Has(JsonElement payload, string name) => TryProperty(payload, name, out _);

    private static bool TryInt(JsonElement payload, string name, out int value)
    {
        value = 0;
        return TryProperty(payload, name, out var element)
            && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static bool TryDate(JsonElement payload, string name, out DateOnly value)
    {
        value = default;
        return TryProperty(payload, name, out var element) && TryDate(element, out value);
    }

    private static bool TryDate(JsonElement element, out DateOnly value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.String
            && DateOnly.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static bool TryShift(JsonElement payload, out Shift shift)
    {
        shift = default;
        return TryProperty(payload, "shift", out var element)
            && element.ValueKind == JsonValueKind.String
            && Enum.TryParse(element.GetString(), ignoreCase: true, out shift)
            && Enum.IsDefined(shift);
    }

    private static bool TryInstant(JsonElement payload, string name, out DateTimeOffset value)
    {
        value = default;
        return TryProperty(payload, name, out var element)
            && element.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }

    private static int? OptionalInt(JsonElement payload, string name) =>
        TryProperty(payload, name, out var element) && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out var value)
            ? value
            : null;

    private static bool? OptionalBool(JsonElement payload, string name) =>
        TryProperty(payload, name, out var element)
            ? element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    private static string? OptionalString(JsonElement payload, string name) =>
        TryProperty(payload, name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
