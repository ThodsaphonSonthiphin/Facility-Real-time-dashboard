using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;

/// <summary>
/// The scheduling reads and conflict checks that are stored data, not pure rules. Interval overlap cannot be expressed
/// as a portable unique index on MySQL and SQLite, so regular Supervisor ownership is checked here while the caller
/// holds the building/shift <see cref="Domain.Entities.Scheduling.ScheduleScopeAnchor"/>.
/// </summary>
public sealed class ScheduleStore(AppDbContext db, TimeProvider? clock = null)
{
    /// <summary>Nothing the actor may read in this week: no roster row at all, not an empty week of work.</summary>
    private const string AvailabilityEmpty = "empty";

    /// <summary>The person has no dated identity yet, so the roster shows them without claiming a Cleaner kind.</summary>
    private const string UnknownCleanerKind = "unknown";

    /// <summary>
    /// True when another regular Supervisor period for the building and shift overlaps the proposed end-exclusive range.
    /// A null <paramref name="effectiveUntil"/> is an open-ended claim. An inverted range throws instead of answering
    /// "free", because no stored row can ever overlap a range that describes no dates.
    /// </summary>
    public Task<bool> RegularScopeOverlapsAsync(
        int buildingId,
        Shift shift,
        DateOnly effectiveFrom,
        DateOnly? effectiveUntil,
        int? excludingPeriodId = null,
        CancellationToken ct = default)
    {
        EnsureRangeIsForward(effectiveFrom, effectiveUntil);
        var proposedEnd = effectiveUntil ?? DateOnly.MaxValue;
        return db.SupervisorScopePeriods.AnyAsync(
            period => period.BuildingId == buildingId
                && period.Shift == shift
                && (excludingPeriodId == null || period.Id != excludingPeriodId)
                && period.EffectiveFrom < proposedEnd
                && effectiveFrom < (period.EffectiveUntil ?? DateOnly.MaxValue),
            ct);
    }

    /// <summary>
    /// Rejects a regular Supervisor period that overlaps another one, or whose own range is inverted. Call inside the
    /// write transaction, after locking the building/shift anchor, so two people cannot both claim the same range.
    /// </summary>
    public async Task EnsureRegularScopeAvailableAsync(
        int buildingId,
        Shift shift,
        DateOnly effectiveFrom,
        DateOnly? effectiveUntil,
        int? excludingPeriodId = null,
        CancellationToken ct = default)
    {
        EnsureRangeIsForward(effectiveFrom, effectiveUntil);
        if (await RegularScopeOverlapsAsync(buildingId, shift, effectiveFrom, effectiveUntil, excludingPeriodId, ct))
        {
            throw new ScheduleConflictException("scope-overlap");
        }
    }

    /// <summary>
    /// Takes the building/shift <see cref="Domain.Entities.Scheduling.ScheduleScopeAnchor"/> for the rest of the caller's
    /// transaction: it is created when missing and its revision is bumped, which writes the row and so holds the lock a
    /// second writer of the same building/shift must wait for (spec §11: lock the stable anchor, not the target row that
    /// may not exist yet). Every regular membership check for that building/shift then runs while it is held.
    /// </summary>
    public async Task<ScheduleScopeAnchor> LockRegularScopeAsync(
        int buildingId, Shift shift, DateTime nowUtc, CancellationToken ct = default)
    {
        var anchor = await db.ScheduleScopeAnchors.FirstOrDefaultAsync(
            a => a.BuildingId == buildingId && a.Shift == shift, ct);
        if (anchor is null)
        {
            var created = new ScheduleScopeAnchor { BuildingId = buildingId, Shift = shift, Revision = 1, UpdatedAt = nowUtc };
            db.ScheduleScopeAnchors.Add(created);
            try
            {
                await db.SaveChangesAsync(ct);
                return created;
            }
            catch (DbUpdateException exception) when (IsDuplicateKeyViolation(exception))
            {
                // Another writer created the anchor first, which is the case this row exists for. Anything else (a deadlock
                // or a foreign key on the same save) rolls the transaction back and is not ours to explain: it leaves no
                // anchor row behind, so it is rethrown below instead of being read as "someone else won".
                db.Entry(created).State = EntityState.Detached;
                anchor = await FindAnchorUnderLockAsync(buildingId, shift, ct);
                if (anchor is null)
                {
                    throw;
                }
            }
        }

        anchor.Revision += 1;
        anchor.UpdatedAt = nowUtc;
        await db.SaveChangesAsync(ct);
        return anchor;
    }

    /// <summary>
    /// The building/shift anchor this transaction just failed to create, read with a locking read on MySQL: the winner
    /// may still be open, and its period must be visible to the overlap check that follows instead of being answered from
    /// this transaction's snapshot. A plain read (SQLite) cannot answer from an older snapshot, so it stays plain.
    /// </summary>
    private async Task<ScheduleScopeAnchor?> FindAnchorUnderLockAsync(int buildingId, Shift shift, CancellationToken ct)
    {
        if (IsMySql)
        {
            var rows = await db.ScheduleScopeAnchors
                .FromSqlRaw("SELECT * FROM schedule_scope_anchors WHERE building_id = {0} FOR UPDATE", buildingId)
                .ToListAsync(ct);
            return rows.FirstOrDefault(a => a.Shift == shift);
        }

        var existing = await db.ScheduleScopeAnchors.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BuildingId == buildingId && a.Shift == shift, ct);
        return existing is null ? null : await db.ScheduleScopeAnchors.FirstAsync(a => a.Id == existing.Id, ct);
    }

    /// <summary>
    /// <see cref="EnsureRegularScopeAvailableAsync"/> for a caller that already holds the building/shift anchor. On MySQL
    /// the rows are read with a locking read: under REPEATABLE READ a plain read would answer from the snapshot the
    /// transaction took before the other writer committed, so two people could both see a free slot.
    /// </summary>
    public async Task EnsureRegularScopeAvailableUnderLockAsync(
        int buildingId, Shift shift, DateOnly effectiveFrom, DateOnly? effectiveUntil, CancellationToken ct = default)
    {
        EnsureRangeIsForward(effectiveFrom, effectiveUntil);
        var proposedEnd = effectiveUntil ?? DateOnly.MaxValue;
        var rows = IsMySql
            ? await db.SupervisorScopePeriods
                .FromSqlRaw("SELECT * FROM supervisor_scope_periods WHERE building_id = {0} FOR UPDATE", buildingId)
                .AsNoTracking().ToListAsync(ct)
            : await db.SupervisorScopePeriods.AsNoTracking().Where(p => p.BuildingId == buildingId).ToListAsync(ct);
        if (rows.Any(period => period.Shift == shift
            && period.EffectiveFrom < proposedEnd
            && effectiveFrom < (period.EffectiveUntil ?? DateOnly.MaxValue)))
        {
            throw new ScheduleConflictException("scope-overlap");
        }
    }

    private bool IsMySql =>
        db.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>MySQL's ER_DUP_ENTRY: the save lost a UNIQUE index to a row another connection had already stored.</summary>
    private const int MySqlDuplicateEntry = 1062;

    /// <summary>SQLite's SQLITE_CONSTRAINT_UNIQUE extended code (a foreign key failure is 787, a plain constraint 19).</summary>
    private const int SqliteConstraintUnique = 2067;

    /// <summary>Microsoft.Data.Sqlite's exception type; the tests' provider, which the API itself does not reference.</summary>
    private const string SqliteExceptionTypeName = "Microsoft.Data.Sqlite.SqliteException";

    /// <summary>The extended code property of that type, read by name for the same reason.</summary>
    private const string SqliteExtendedCodeProperty = "SqliteExtendedErrorCode";

    /// <summary>
    /// True only when <paramref name="exception"/> is a real duplicate-key violation — MySQL 1062 or SQLite's
    /// SQLITE_CONSTRAINT_UNIQUE (2067) — because that is the only save failure another writer can cause by inserting the
    /// anchor first (spec §11). Everything else must reach the caller: a deadlock (MySQL 1213) or a lock wait timeout has
    /// already rolled the whole transaction back, so a command that read it as "another writer won" would carry on writing
    /// outside the transaction its receipt and audit rows commit in.
    /// <para>
    /// The SQLite branch reads the provider's extended code by name instead of by type because Microsoft.Data.Sqlite is the
    /// test provider and the API never references it. An unreadable code answers "not a duplicate", so the fallback always
    /// rethrows rather than swallowing an unknown failure.
    /// </para>
    /// </summary>
    public static bool IsDuplicateKeyViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException { Number: MySqlDuplicateEntry })
            {
                return true;
            }

            if (SqliteExtendedErrorCode(current) == SqliteConstraintUnique)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>SQLite's extended error code for a Microsoft.Data.Sqlite exception, or null for anything else.</summary>
    private static int? SqliteExtendedErrorCode(Exception exception)
    {
        var type = exception.GetType();
        if (type.FullName != SqliteExceptionTypeName)
        {
            return null;
        }

        return type.GetProperty(SqliteExtendedCodeProperty)?.GetValue(exception) is int code ? code : null;
    }

    /// <summary>
    /// The transaction one schedule command runs in (spec §11: the token, the business rows, the audit and the receipt
    /// commit together or not at all). MySQL takes ReadCommitted so every statement after a lock sees what the previous
    /// owner of that lock committed; REPEATABLE READ would keep answering from the snapshot taken before the lock was
    /// granted, which is exactly how two writers both "see" a free slot. SQLite has one writer at a time, so it keeps the
    /// strongest level it offers.
    /// </summary>
    public Task<IDbContextTransaction> BeginCommandTransactionAsync(CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(IsMySql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, ct);

    /// <summary>
    /// Takes the person/slot <see cref="Domain.Entities.Scheduling.ScheduleAnchor"/> for the rest of the caller's
    /// transaction, creating it when the slot has never been written and bumping its revision so the row is written and
    /// therefore locked (spec §11: two commands that both see an empty slot still contend on this row, so only one of them
    /// can commit the first create).
    /// </summary>
    public async Task<ScheduleAnchor> LockSlotAsync(
        int personId, DateOnly shiftDate, Shift shift, DateTime nowUtc, CancellationToken ct = default)
    {
        var anchor = await FindSlotAnchorUnderLockAsync(personId, shiftDate, shift, ct);
        if (anchor is null)
        {
            var created = new ScheduleAnchor
            {
                PersonId = personId, ShiftDate = shiftDate, Shift = shift, Revision = 1, UpdatedAt = nowUtc,
            };
            db.ScheduleAnchors.Add(created);
            try
            {
                await db.SaveChangesAsync(ct);
                return created;
            }
            catch (DbUpdateException exception) when (IsDuplicateKeyViolation(exception))
            {
                // Another command created this slot's anchor first, which is the case the row exists for. A deadlock, a lock
                // wait timeout or a foreign key rolls the command's transaction back and must reach the engine instead.
                db.Entry(created).State = EntityState.Detached;
                anchor = await FindSlotAnchorUnderLockAsync(personId, shiftDate, shift, ct);
                if (anchor is null)
                {
                    throw;
                }
            }
        }

        anchor.Revision += 1;
        anchor.UpdatedAt = nowUtc;
        await db.SaveChangesAsync(ct);
        return anchor;
    }

    /// <summary>
    /// Takes the per-person baseline head <see cref="Domain.Entities.Scheduling.ScheduleBaselineAnchor"/> for the rest of
    /// the caller's transaction, creating it when the person has no baseline yet: a version's first row and a range edit
    /// of an existing one both serialise here, so neither can be decided from a state the other is about to change.
    /// </summary>
    public async Task<ScheduleBaselineAnchor> LockBaselineHeadAsync(
        int personId, DateTime nowUtc, CancellationToken ct = default, bool advanceRevision = true)
    {
        var anchor = await FindBaselineAnchorUnderLockAsync(personId, ct);
        if (anchor is null)
        {
            var created = new ScheduleBaselineAnchor
            {
                PersonId = personId,
                Revision = advanceRevision ? 1 : 0,
                UpdatedAt = nowUtc,
            };
            db.ScheduleBaselineAnchors.Add(created);
            try
            {
                await db.SaveChangesAsync(ct);
                return created;
            }
            catch (DbUpdateException exception) when (IsDuplicateKeyViolation(exception))
            {
                // The same create/retry as the slot anchor: only a lost UNIQUE race is recoverable, never a rolled-back
                // transaction the command would otherwise keep writing outside of.
                db.Entry(created).State = EntityState.Detached;
                anchor = await FindBaselineAnchorUnderLockAsync(personId, ct);
                if (anchor is null)
                {
                    throw;
                }
            }
        }

        if (advanceRevision)
        {
            anchor.Revision += 1;
            anchor.UpdatedAt = nowUtc;
            await db.SaveChangesAsync(ct);
        }
        return anchor;
    }

    /// <summary>
    /// Lock this exact slot before modifying its anchor so a concurrent retention delete or another slot writer
    /// serialises with this transaction. SQLite serialises writers at the transaction level.
    /// </summary>
    private async Task<ScheduleAnchor?> FindSlotAnchorUnderLockAsync(
        int personId, DateOnly shiftDate, Shift shift, CancellationToken ct)
    {
        if (IsMySql)
        {
            var rows = await db.ScheduleAnchors
                .FromSqlRaw("SELECT * FROM schedule_anchors WHERE person_id = {0} AND shift_date = {1} AND shift = {2} FOR UPDATE",
                    personId, shiftDate.ToDateTime(TimeOnly.MinValue), shift.ToString().ToUpperInvariant())
                .ToListAsync(ct);
            return rows.SingleOrDefault();
        }

        return await db.ScheduleAnchors.FirstOrDefaultAsync(
            row => row.PersonId == personId && row.ShiftDate == shiftDate && row.Shift == shift, ct);
    }

    /// <summary>The per-person baseline head, read with the same locking read for the same reason.</summary>
    private async Task<ScheduleBaselineAnchor?> FindBaselineAnchorUnderLockAsync(int personId, CancellationToken ct)
    {
        if (IsMySql)
        {
            var rows = await db.ScheduleBaselineAnchors
                .FromSqlRaw("SELECT * FROM schedule_baseline_anchors WHERE person_id = {0} FOR UPDATE", personId)
                .ToListAsync(ct);
            return rows.FirstOrDefault(row => row.PersonId == personId);
        }

        var existing = await db.ScheduleBaselineAnchors.AsNoTracking()
            .FirstOrDefaultAsync(row => row.PersonId == personId, ct);
        return existing is null ? null : await db.ScheduleBaselineAnchors.FirstAsync(row => row.Id == existing.Id, ct);
    }

    /// <summary>
    /// Spec §3, §10 and §11 (acceptance A01, A04, A20 and A27): the authorised projection of one ISO week. The roster is
    /// the active Cleaner membership itself, so a person with no scheduling row still appears — with unavailable days,
    /// never with a guessed Area. Scope is decided per date by <see cref="ScheduleAuthority.Covers"/> over the periods the
    /// one shared <see cref="SupervisorScopes"/> reader loads: Admin reads every building, a Supervisor only the dates and
    /// buildings its dated periods hold, a Cleaner only their own row; a supplied building or shift narrows and never
    /// widens.
    /// <para>
    /// Every input of the week is read inside one transaction, so a multi-row write that commits meanwhile yields the
    /// whole before or the whole after projection, never half of each.
    /// </para>
    /// </summary>
    public async Task<ScheduleProjection> ReadWeekAsync(
        int actorId, DateOnly weekStart, int? buildingId, Shift? shift, CancellationToken ct = default)
    {
        var monday = ScheduleRules.WeekStart(weekStart);
        var readAt = (clock ?? TimeProvider.System).GetUtcNow();

        // MySQL answers from the transaction's REPEATABLE READ snapshot, and SERIALIZABLE there would take shared locks on
        // every read of a projection. SQLite's Serializable transaction is the closest equivalent it offers, so the level
        // is chosen per provider instead of being inferred from whichever one a test happens to run on.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(
                IsMySql ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable, ct)
            : null;

        return await ReadSnapshotAsync(actorId, monday, buildingId, shift, readAt, ct);
    }

    /// <summary>The machine codes a relief lookup answers with, one per stored fact that blocks the assignment.</summary>

    /// <summary>Another live relief assignment already holds the person's slot (spec §6: a second row is a conflict).</summary>
    public const string ReliefExistingAssignment = "existing-assignment";

    /// <summary>The person's own applicable plan already works an Area in that slot; nothing may be moved or cancelled.</summary>
    public const string ReliefExistingDuty = "existing-duty";

    /// <summary>An agreed leave interval covers the slot, so the person is not available for a loan.</summary>
    public const string ReliefOnLeave = "on-leave";

    /// <summary>The candidate may be planned into that shift: no conflict at all.</summary>
    public const string ReliefEligible = "eligible";

    /// <summary>At least one stored fact blocks it; <see cref="ReliefCandidateFacts.Conflicts"/> names which.</summary>
    public const string ReliefConflict = "conflict";

    /// <summary>
    /// Spec §3, §6 and §14: the dated profile period in force on <paramref name="date"/>, or null when no period covers it.
    /// The half-open boundary is <see cref="ScheduleResolver.Applies"/>, so a period that ends on that date does not answer
    /// for it, and the latest start wins exactly as the week read resolves a person's identity.
    /// </summary>
    public async Task<CleanerProfilePeriod?> ReadProfileAsync(int userId, DateOnly date, CancellationToken ct = default)
    {
        var profiles = await db.CleanerProfilePeriods.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .OrderBy(profile => profile.EffectiveFrom).ThenBy(profile => profile.Id)
            .ToListAsync(ct);
        return profiles.LastOrDefault(profile =>
            ScheduleResolver.Applies(profile.EffectiveFrom, profile.EffectiveUntil, date));
    }

    /// <summary>
    /// The live relief assignment of one person's slot, or null. A cancelled row keeps its history but does not hold the
    /// slot, so it is never what a new assignment (or the revision key that names the slot) contends with.
    /// </summary>
    public Task<ReliefAssignment?> LiveReliefAsync(
        int userId, DateOnly date, Shift shift, CancellationToken ct = default) =>
        db.ReliefAssignments.AsNoTracking().FirstOrDefaultAsync(row => row.UserId == userId
            && row.ShiftDate == date && row.Shift == shift && !row.IsCancelled, ct);

    /// <summary>
    /// Spec §9 and ADRs facility-0203/0204/0213: the one small read of a slot's midshift transfer — the latest instruction
    /// (cancelled or not, because a cancellation is history the screen still shows), its confirmation when somebody arrived
    /// under it, and the destination the last confirmed instruction actually moved responsibility to, or null when the slot
    /// has no instruction at all.
    /// <para>
    /// The chain matters after a confirmed arrival: a later forward instruction is a new row (ADR facility-0213), so the
    /// destination of the last confirmed row is where the person still is until the forward instruction takes effect or is
    /// cancelled. Reading only the latest row would answer the plan's original Area in between, which is never where that
    /// person is. It reads one slot rather than the week: the week projection already assembles
    /// <see cref="Application.Scheduling.TransferFact"/> rows for the roster.
    /// </para>
    /// </summary>
    public async Task<SlotTransfer?> ReadSlotTransferAsync(
        int personId, DateOnly date, Shift shift, CancellationToken ct = default)
    {
        var instructions = await db.AreaTransfers.AsNoTracking()
            .Where(row => row.PersonId == personId && row.ShiftDate == date && row.Shift == shift)
            .OrderBy(row => row.Id)
            .ToListAsync(ct);
        if (instructions.Count == 0)
        {
            return null;
        }

        var ids = instructions.Select(row => row.Id).ToList();
        var confirmations = await db.AreaTransferConfirmations.AsNoTracking()
            .Where(row => ids.Contains(row.TransferId))
            .ToListAsync(ct);
        return BuildSlotTransfer(instructions, confirmations);
    }

    /// <summary>
    /// Spec §13 (Task 11): every person's stored transfer chain in one slot, read in two queries. The attendance board needs
    /// the current responsibility per person, and asking <see cref="ReadSlotTransferAsync"/> once per person would multiply
    /// the same two reads across the roster.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, SlotTransfer>> ReadSlotTransfersAsync(
        ShiftSlot slot, CancellationToken ct = default)
    {
        var instructions = await db.AreaTransfers.AsNoTracking()
            .Where(row => row.ShiftDate == slot.ShiftDate && row.Shift == slot.Shift)
            .OrderBy(row => row.Id)
            .ToListAsync(ct);
        if (instructions.Count == 0)
        {
            return new Dictionary<int, SlotTransfer>();
        }

        var ids = instructions.Select(row => row.Id).ToList();
        var confirmations = await db.AreaTransferConfirmations.AsNoTracking()
            .Where(row => ids.Contains(row.TransferId))
            .ToListAsync(ct);
        return instructions
            .GroupBy(row => row.PersonId)
            .ToDictionary(group => group.Key, group => BuildSlotTransfer(group.ToList(), confirmations));
    }

    /// <summary>
    /// Spec §13 and ADR facility-0216 (Task 11): the legitimate actual entry facts of one slot, for the shared entry
    /// projection. It emits exactly two kinds — the initial Shift-In recorded at the Area (filed under the handover the
    /// Admin sees, admin and supervisor corrections included) and a confirmed transfer arrival at its destination. An
    /// instruction, a loan, a skip marker or a pending replacement is never evidence and is not read here.
    /// </summary>
    public async Task<IReadOnlyList<AreaEntryEvidence>> ReadEntryEvidenceAsync(
        ShiftSlot slot, CancellationToken ct = default)
    {
        var shiftIns = await db.ShiftAttendances.AsNoTracking()
            .Where(row => row.ShiftDate == slot.ShiftDate && row.Shift == slot.Shift
                && row.EventType == AttendanceEvent.ShiftIn)
            .Select(row => new { row.AreaId, row.OccurredAt })
            .ToListAsync(ct);

        // The confirmation carries the Area and time; the instruction only supplies the slot, because a confirmation has no
        // date/shift of its own. A cancelled instruction never has a confirmation (Task 10), and the guard states that.
        var arrivals = await db.AreaTransferConfirmations.AsNoTracking()
            .Join(db.AreaTransfers.AsNoTracking(),
                confirmation => confirmation.TransferId,
                instruction => instruction.Id,
                (confirmation, instruction) => new { confirmation, instruction })
            .Where(row => row.instruction.ShiftDate == slot.ShiftDate
                && row.instruction.Shift == slot.Shift
                && !row.instruction.IsCancelled)
            .Select(row => new { row.confirmation.AreaId, row.confirmation.OccurredAt, row.instruction.Id })
            .ToListAsync(ct);

        return shiftIns
            .Select(row => new AreaEntryEvidence(row.AreaId, slot.ShiftDate, slot.Shift, Utc(row.OccurredAt),
                EntryEvidenceKinds.ShiftIn, null))
            .Concat(arrivals.Select(row => new AreaEntryEvidence(row.AreaId, slot.ShiftDate, slot.Shift,
                Utc(row.OccurredAt), EntryEvidenceKinds.TransferArrival, row.Id)))
            .ToList();
    }

    /// <summary>The one chain reading both callers share: latest instruction, its confirmation and the last confirmed Area.</summary>
    private static SlotTransfer BuildSlotTransfer(
        IReadOnlyList<AreaTransfer> instructions, IReadOnlyList<AreaTransferConfirmation> confirmations)
    {
        var latest = instructions[^1];
        // A confirmed arrival is evidence that stays: the latest confirmed row is where responsibility has actually moved to,
        // and a forward instruction or a cancelled one only builds on it.
        var lastConfirmedAreaId = instructions
            .Where(row => confirmations.Any(confirmation => confirmation.TransferId == row.Id))
            .Select(row => (int?)row.ToAreaId)
            .LastOrDefault();
        return new SlotTransfer(
            latest,
            confirmations.FirstOrDefault(confirmation => confirmation.TransferId == latest.Id),
            lastConfirmedAreaId);
    }

    /// <summary>The live leave row of one person's slot: an agreed absence that no plan may quietly work through.</summary>
    public Task<bool> HasLiveLeaveAsync(
        int userId, DateOnly date, Shift shift, CancellationToken ct = default) =>
        db.ScheduleExceptions.AsNoTracking().AnyAsync(row => row.UserId == userId
            && row.ShiftDate == date && row.Shift == shift
            && row.Kind == ScheduleExceptionKind.Leave && !row.IsCancelled, ct);

    /// <summary>
    /// The one person's resolved day, through the very resolver the week read uses, or null when the person does not exist.
    /// It exists so a command can ask "what do the stored records say this person works that day" without re-implementing
    /// precedence: a dated Area, else the versioned baseline, and the day-off/leave states kept apart from both.
    /// </summary>
    public async Task<ScheduleDay?> ReadPersonDayAsync(
        int userId, DateOnly date, Shift shift, CancellationToken ct = default)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId, ct))
        {
            return null;
        }

        var profiles = await db.CleanerProfilePeriods.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .ToListAsync(ct);
        var baselines = await db.ScheduleBaselines.AsNoTracking().Include(baseline => baseline.Days)
            .Where(baseline => baseline.UserId == userId
                && baseline.EffectiveFrom <= date
                && (baseline.EffectiveUntil == null || date < baseline.EffectiveUntil.Value))
            .OrderBy(baseline => baseline.EffectiveFrom).ThenBy(baseline => baseline.Id)
            .ToListAsync(ct);
        var header = await db.WeeklyDayOffs.AsNoTracking().Include(week => week.Choices)
            .FirstOrDefaultAsync(week => week.UserId == userId && week.WeekStart == ScheduleRules.WeekStart(date), ct);
        var exceptions = await db.ScheduleExceptions.AsNoTracking()
            .Where(row => row.UserId == userId && row.ShiftDate == date && row.Shift == shift)
            .OrderBy(row => row.Id)
            .ToListAsync(ct);

        // History coverage is the person's own earliest dated identity or baseline version, exactly as the week read decides
        // it: before that the resolver answers "unavailable" rather than inventing a plan.
        var historyStartsAt = profiles.Select(profile => (DateOnly?)profile.EffectiveFrom)
            .Concat(baselines.Select(baseline => (DateOnly?)baseline.EffectiveFrom))
            .Min() ?? DateOnly.MaxValue;

        var input = new ScheduleInput(
            userId,
            historyStartsAt,
            baselines.SelectMany(baseline => baseline.Days.Select(day => new BaselineFact(day.Id,
                baseline.EffectiveFrom, baseline.EffectiveUntil, day.Weekday, day.AreaId, day.BreakGroup,
                Revision(baseline.Revision), day.IsDayOff))).ToList(),
            header?.Choices.Select(choice => choice.OffDate).ToHashSet(),
            exceptions.Where(row => !row.IsCancelled && row.Kind != ScheduleExceptionKind.Leave)
                .Select(row => new DatedFact(row.Id, row.ShiftDate, row.Shift, row.AreaId, row.BreakGroup,
                    Revision(row.Revision)))
                .ToList(),
            exceptions.Where(row => row.Kind == ScheduleExceptionKind.Leave)
                .Select(row => new LeaveFact(row.Id, row.ShiftDate, row.Shift, Utc(row.StartsAt!.Value),
                    Utc(row.EndsAt!.Value), row.IsCancelled, Revision(row.Revision)))
                .ToList(),
            []);
        return ScheduleResolver.Resolve(input, date);
    }

    /// <summary>
    /// Spec §3 and §6: the active Supervisor responsible for a building and shift on a date — the person whose agreement a
    /// cross-building loan has to be reported from. The dated rows answer first; the account projection is read only while no
    /// dated row exists at all (spec §14), which is the same rule the account forms hold a building slot to.
    /// </summary>
    public async Task<int?> ResponsibleSupervisorAsync(
        int buildingId, Shift shift, DateOnly date, CancellationToken ct = default)
    {
        var dated = await (
                from period in db.SupervisorScopePeriods.AsNoTracking()
                join holder in db.Users.AsNoTracking() on period.SupervisorId equals holder.Id
                where period.BuildingId == buildingId && period.Shift == shift
                    && period.EffectiveFrom <= date
                    && (period.EffectiveUntil == null || date < period.EffectiveUntil.Value)
                    && holder.Role == UserRole.Supervisor && holder.IsActive
                orderby period.EffectiveFrom
                select (int?)period.SupervisorId)
            .FirstOrDefaultAsync(ct);
        if (dated is not null)
        {
            return dated;
        }

        if (await db.SupervisorScopePeriods.AsNoTracking().AnyAsync(ct))
        {
            return null;
        }

        return await db.Users.AsNoTracking()
            .Where(user => user.Role == UserRole.Supervisor && user.IsActive
                && user.BuildingId == buildingId && user.Shift == shift)
            .Select(user => (int?)user.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Spec §6: every stored fact that blocks planning this person into a slot, as machine codes. It is one rule for the
    /// command and the lookup, because a screen that offered a person the command then refuses is worse than no list: a
    /// second live assignment, an agreed leave interval, or a regular duty the person already holds. Nothing here cancels or
    /// moves the blocking row.
    /// </summary>
    public async Task<IReadOnlyList<string>> ReliefConflictsAsync(
        int userId, DateOnly date, Shift shift, CancellationToken ct = default)
    {
        var conflicts = new List<string>();
        if (await LiveReliefAsync(userId, date, shift, ct) is not null)
        {
            conflicts.Add(ReliefExistingAssignment);
        }

        if (await HasLiveLeaveAsync(userId, date, shift, ct))
        {
            conflicts.Add(ReliefOnLeave);
        }

        var day = await ReadPersonDayAsync(userId, date, shift, ct);
        if (day is { IsDayOff: false, IsLeave: false } && day.Area.Value is not null)
        {
            conflicts.Add(ReliefExistingDuty);
        }

        return conflicts;
    }

    /// <summary>
    /// Spec §6 and A09: the lookup a destination recorder makes by employee id and source building. It answers only for an
    /// active Relief Cleaner whose dated profile on that date puts them in that very source building — anyone else is not
    /// returned at all — and it describes eligibility, never the person's schedule or another team's rows.
    /// </summary>
    public async Task<ReliefCandidateFacts?> ReadReliefCandidateAsync(
        string? employeeId, int sourceBuildingId, DateOnly date, Shift shift, CancellationToken ct = default,
        int? ownUserId = null)
    {
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(user =>
            (ownUserId != null ? user.Id == ownUserId : user.EmployeeId == employeeId) && user.Role == UserRole.Cleaner && user.IsActive, ct);
        if (person is null)
        {
            return null;
        }

        var profile = await ReadProfileAsync(person.Id, date, ct);
        if (profile is not { Kind: CleanerKind.Relief } || profile.HomeBuildingId != sourceBuildingId
            || profile.Shift != shift)
        {
            return null;
        }

        var code = await db.Buildings.AsNoTracking()
            .Where(building => building.Id == sourceBuildingId)
            .Select(building => building.Code)
            .FirstOrDefaultAsync(ct);
        if (code is null)
        {
            return null;
        }

        var conflicts = await ReliefConflictsAsync(person.Id, date, shift, ct);
        return new ReliefCandidateFacts(person.Id, person.EmployeeId!, person.DisplayName, sourceBuildingId, code,
            shift, conflicts.Count == 0, conflicts.Count == 0 ? ReliefEligible : ReliefConflict, conflicts);
    }

    private async Task<ScheduleProjection> ReadSnapshotAsync(
        int actorId, DateOnly monday, int? buildingId, Shift? shift, DateTimeOffset readAt, CancellationToken ct)
    {
        var weekEnd = monday.AddDays(7);
        var dates = Enumerable.Range(0, 7).Select(offset => monday.AddDays(offset)).ToList();

        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId, ct);
        if (actor is not { IsActive: true })
        {
            return Empty(monday, readAt);
        }

        var cleaners = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive)
            .OrderBy(u => u.DisplayName).ThenBy(u => u.Id)
            .ToListAsync(ct);

        // A Cleaner reads their own row only; a management read starts from the whole roster and is narrowed below.
        var roster = actor.Role == UserRole.Cleaner
            ? cleaners.Where(person => person.Id == actor.Id).ToList()
            : cleaners;

        // The actor's dated authority, loaded by the one rule the Api's ScheduleScopeReader reads through too, so the two
        // cannot drift (spec §14). A supplied building or shift narrows the periods and can never widen them. Which dates a
        // period may be read on is decided below, per date, by ScheduleAuthority.Covers — not by a week-overlap test, which
        // would hand a Supervisor the dates before their period starts and the dates after it ends.
        List<ScopePeriod>? authorised = null;
        IReadOnlyCollection<ScopePeriod> managementScope = [];
        if (actor.Role == UserRole.Supervisor)
        {
            managementScope = await SupervisorScopes.ReadAsync(db, actor, clock ?? TimeProvider.System, ct);
            authorised = managementScope.Where(period => buildingId is null || period.BuildingId == buildingId)
                .Where(period => shift is null || period.Shift == shift)
                .Where(period => dates.Exists(date => ScheduleAuthority.Covers(period, date)))
                .ToList();
            if (authorised.Count == 0)
            {
                return Empty(monday, readAt);
            }
        }

        var personIds = roster.Select(person => person.Id).ToHashSet();

        // Spec §6 and controller ruling (e): the relief rows are a top-level list of their own, scoped to what this reader may
        // see — a Cleaner only their own, a Supervisor only a destination building/shift/date of theirs, an Admin all — so a
        // week can carry relief work even when no roster row of the week survived the scope.
        var relief = await ReadReliefAsync(actor, monday, weekEnd, buildingId, shift, authorised, ct);

        var areas = await db.Areas.AsNoTracking().OrderBy(area => area.Code)
            .Select(area => new { area.Id, area.BuildingId, area.Code, area.Name, area.IsActive, area.ShiftPattern }).ToListAsync(ct);
        var areaBuildings = areas.ToDictionary(area => area.Id, area => area.BuildingId);
        var optionsByBuilding = areas.Where(area => area.IsActive).GroupBy(area => area.BuildingId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ScheduleAreaOption>)group
                .Select(area => new ScheduleAreaOption(area.Id, $"{area.Code} · {area.Name}")).ToList());

        var businessDate = ScheduleAuthority.BusinessDate(readAt.UtcDateTime);
        var buildingCodes = await db.Buildings.AsNoTracking().ToDictionaryAsync(row => row.Id, row => row.Code, ct);
        var destinations = new List<ScheduleDestination>();
        if (actor.Role is UserRole.Admin or UserRole.Supervisor)
        {
            foreach (var date in dates)
            foreach (var slotShift in Enum.GetValues<Shift>().Where(value => shift is null || value == shift))
            foreach (var building in buildingCodes.Where(row => buildingId is null || row.Key == buildingId))
            {
                var canChange = date < businessDate
                    ? ScheduleAuthority.CanCorrectHistory(actor.Role, actor.Id, building.Key, slotShift, date, businessDate,
                        managementScope)
                    : ScheduleAuthority.CanManage(actor.Role, actor.Id, building.Key, slotShift, date, managementScope);
                if (!canChange) continue;
                var options = areas.Where(area => area.IsActive && area.BuildingId == building.Key
                    && (slotShift == Shift.Day || area.ShiftPattern != ShiftPattern.DayOnly))
                    .Select(area => new ScheduleAreaOption(area.Id, $"{area.Code} · {area.Name}")).ToList();
                if (options.Count > 0) destinations.Add(new ScheduleDestination(date, slotShift, building.Key, building.Value, options));
            }
        }

        var sourceBuildings = destinations.Count == 0 ? null : buildingCodes.OrderBy(row => row.Value)
            .Select(row => new ScheduleSourceBuilding(row.Key, row.Value)).ToList();

        if (personIds.Count == 0)
        {
            return Empty(monday, readAt, relief) with { Destinations = destinations, SourceBuildings = sourceBuildings };
        }

        var profiles = await db.CleanerProfilePeriods.AsNoTracking()
            .Where(period => personIds.Contains(period.UserId)).ToListAsync(ct);
        var baselines = await db.ScheduleBaselines.AsNoTracking().Include(baseline => baseline.Days)
            .Where(baseline => personIds.Contains(baseline.UserId)).ToListAsync(ct);
        // Membership only (spec §14): the compatibility projection may say which building a person belongs to, never what
        // their history was. Only the periods that overlap the read week are membership at all, so a person who left a
        // building stops counting as its member on the dates their period no longer covers; and the projection is the
        // membership of the dates before the person's first dated identity, so a profile written today does not remove the
        // dates before today from the read.
        var memberships = roster.ToDictionary(person => person.Id, person => MembershipSlots(
            person,
            profiles.Where(profile => profile.UserId == person.Id).ToList(),
            baselines.Where(baseline => baseline.UserId == person.Id).ToList(),
            areaBuildings,
            monday,
            weekEnd));

        // Per-date scope (spec §3): a Supervisor's row holds only the dates its dated period covers, so a period that starts
        // or ends mid-week never reads the rest of the week, and a person with no covered date of the week is not in the
        // roster at all. Every other actor reads the seven days of the week it is entitled to.
        IReadOnlyList<DateOnly> DatesThisReadCovers(User person)
        {
            var slots = memberships[person.Id];
            if (authorised is not null)
            {
                return dates.Where(date => slots.Any(slot =>
                        ScheduleAuthority.Covers(slot, date)
                        && authorised.Any(period => period.BuildingId == slot.BuildingId && period.Shift == slot.Shift
                            && ScheduleAuthority.Covers(period, date))))
                    .ToList();
            }

            var matchesFilters = slots.Count == 0
                ? buildingId is null && shift is null
                : slots.Any(slot => (buildingId is null || slot.BuildingId == buildingId)
                    && (shift is null || slot.Shift == shift));
            return matchesFilters ? dates : [];
        }

        var readDates = roster.ToDictionary(person => person.Id, DatesThisReadCovers);
        var included = roster.Where(person => readDates[person.Id].Count > 0).ToList();
        var includedIds = included.Select(person => person.Id).ToList();
        // A transfer makes its slot visible to the dated teams on either side without adding the cleaner to their roster.
        // Read those standalone facts in the same snapshot as the roster and scope.
        var scopedBuildings = authorised?.Select(period => period.BuildingId).ToHashSet() ?? [];
        var candidateAreaIds = areas.Where(area => scopedBuildings.Contains(area.BuildingId)).Select(area => area.Id).ToList();
        var transfers = await db.AreaTransfers.AsNoTracking()
            .Where(row => row.ShiftDate >= monday && row.ShiftDate < weekEnd
                && (includedIds.Contains(row.PersonId) || (personIds.Contains(row.PersonId)
                    && (candidateAreaIds.Contains(row.FromAreaId) || candidateAreaIds.Contains(row.ToAreaId)))))
            .ToListAsync(ct);
        var transferIds = transfers.Select(row => row.Id).ToList();
        var confirmations = transferIds.Count == 0
            ? []
            : await db.AreaTransferConfirmations.AsNoTracking()
                .Where(row => transferIds.Contains(row.TransferId)).ToListAsync(ct);
        var transferSlots = new List<ScheduleTransferSlot>();
        if (authorised is not null)
        {
            var areaCodes = areas.ToDictionary(area => area.Id, area => area.Code);
            var people = roster.ToDictionary(person => person.Id, person => person.DisplayName);
            var arrivals = confirmations.ToDictionary(row => row.TransferId);
            bool CanRead(int areaId, Shift shiftForDate, DateOnly date)
            {
                if (!areaBuildings.TryGetValue(areaId, out var building)
                    || (buildingId is not null && building != buildingId)
                    || (shift is not null && shiftForDate != shift))
                {
                    return false;
                }

                return date < businessDate
                    ? ScheduleAuthority.CanCorrectHistory(actor.Role, actor.Id, building, shiftForDate, date, businessDate,
                        managementScope)
                    : ScheduleAuthority.CanManage(actor.Role, actor.Id, building, shiftForDate, date, managementScope);
            }

            transferSlots = transfers
                .Where(row => !row.IsCancelled && (CanRead(row.FromAreaId, row.Shift, row.ShiftDate)
                    || CanRead(row.ToAreaId, row.Shift, row.ShiftDate)))
                .GroupBy(row => (row.PersonId, row.ShiftDate, row.Shift))
                .Select(group => group.MaxBy(row => row.Id)!)
                .OrderBy(row => row.ShiftDate).ThenBy(row => row.Shift).ThenBy(row => people[row.PersonId]).ThenBy(row => row.Id)
                .Select(row => new ScheduleTransferSlot(row.Id, row.PersonId, people[row.PersonId], row.ShiftDate, row.Shift,
                    areaCodes[row.FromAreaId], areaCodes[row.ToAreaId], Revision(row.Revision),
                    arrivals.TryGetValue(row.Id, out var arrival) ? Utc(arrival.OccurredAt) : null))
                .ToList();
        }

        if (included.Count == 0)
        {
            return Empty(monday, readAt, relief, transferSlots) with { Destinations = destinations, SourceBuildings = sourceBuildings };
        }

        var baselineAnchors = await db.ScheduleBaselineAnchors.AsNoTracking()
            .Where(anchor => includedIds.Contains(anchor.PersonId))
            .ToDictionaryAsync(anchor => anchor.PersonId, anchor => anchor.Revision, ct);
        var exceptions = await db.ScheduleExceptions.AsNoTracking()
            .Where(row => includedIds.Contains(row.UserId) && row.ShiftDate >= monday && row.ShiftDate < weekEnd)
            .ToListAsync(ct);
        var savedWeeks = await db.WeeklyDayOffs.AsNoTracking().Include(week => week.Choices)
            .Where(week => includedIds.Contains(week.UserId) && week.WeekStart == monday)
            .ToListAsync(ct);
        var correctionAudits = actor.Role == UserRole.Cleaner
            ? null
            : await ReadCorrectionAuditsAsync(monday, weekEnd, ct);

        var rows = new List<SchedulePersonWeek>(included.Count);
        var revisions = new List<string>();
        foreach (var person in included)
        {
            var personProfiles = profiles.Where(profile => profile.UserId == person.Id)
                .OrderBy(profile => profile.EffectiveFrom).ThenBy(profile => profile.Id).ToList();
            var personBaselines = baselines.Where(baseline => baseline.UserId == person.Id)
                .OrderBy(baseline => baseline.EffectiveFrom).ThenBy(baseline => baseline.Id).ToList();

            // History coverage is the person's own earliest dated identity or baseline version; no global start exists yet.
            var historyStartsAt = personProfiles.Select(profile => (DateOnly?)profile.EffectiveFrom)
                .Concat(personBaselines.Select(baseline => (DateOnly?)baseline.EffectiveFrom))
                .Min() ?? DateOnly.MaxValue;

            // The shift each date of the week belongs to decides which dated rows are that day's slot, so a night shift
            // keeps the date it started on (ADR facility-0183, A20) instead of borrowing the other shift's rows.
            var shiftOn = dates.ToDictionary(date => date,
                date => personProfiles
                    .LastOrDefault(profile => ScheduleResolver.Applies(profile.EffectiveFrom, profile.EffectiveUntil, date))
                    ?.Shift);
            bool IsTheDaysSlot(DateOnly date, Shift slotShift) =>
                shiftOn[date] is not Shift dayShift || dayShift == slotShift;

            // The shift a write must name for a date, resolved exactly as the command handlers resolve it: the dated period
            // in force that day, else — only when the person has no dated row at all — the compatibility projection (spec
            // §14). It is per date because a person's shift can change inside one week, and it is exposed so a client can
            // identify the row it read instead of guessing (spec §11).
            var writtenShiftOn = dates.ToDictionary(date => date,
                date => shiftOn[date] ?? (personProfiles.Count == 0 ? person.Shift : null));

            // The row's own identity is the profile that applies to the read week: the one in force on Monday, else the last
            // one overlapping the week. A period that ended before the week therefore lends the row neither a building nor a
            // kind, instead of the row quietly showing a current-looking identity (spec §14: compatibility is not history).
            var profile = personProfiles
                    .LastOrDefault(candidate => ScheduleResolver.Applies(candidate.EffectiveFrom, candidate.EffectiveUntil, monday))
                ?? personProfiles.LastOrDefault(candidate =>
                    OverlapsWeek(candidate.EffectiveFrom, candidate.EffectiveUntil, monday, weekEnd));
            var header = savedWeeks.FirstOrDefault(week => week.UserId == person.Id);

            var input = new ScheduleInput(
                person.Id,
                historyStartsAt,
                personBaselines
                    .Where(baseline => baseline.EffectiveFrom < weekEnd
                        && (baseline.EffectiveUntil is null || baseline.EffectiveUntil.Value > monday))
                    .SelectMany(baseline => baseline.Days.Select(day => new BaselineFact(day.Id,
                        baseline.EffectiveFrom, baseline.EffectiveUntil, day.Weekday, day.AreaId, day.BreakGroup,
                        Revision(baseline.Revision), day.IsDayOff)))
                    .ToList(),
                header?.Choices.Select(choice => choice.OffDate).ToHashSet(),
                exceptions
                    .Where(row => row.UserId == person.Id && !row.IsCancelled
                        && row.Kind != ScheduleExceptionKind.Leave && IsTheDaysSlot(row.ShiftDate, row.Shift))
                    .Select(row => new DatedFact(row.Id, row.ShiftDate, row.Shift, row.AreaId, row.BreakGroup,
                        Revision(row.Revision)))
                    .ToList(),
                exceptions
                    .Where(row => row.UserId == person.Id && row.Kind == ScheduleExceptionKind.Leave
                        && IsTheDaysSlot(row.ShiftDate, row.Shift))
                    .Select(row => new LeaveFact(row.Id, row.ShiftDate, row.Shift,
                        Utc(row.StartsAt!.Value), Utc(row.EndsAt!.Value), row.IsCancelled, Revision(row.Revision)))
                    .ToList(),
                transfers
                    .Where(row => row.PersonId == person.Id && IsTheDaysSlot(row.ShiftDate, row.Shift))
                    .Select(row => new TransferFact(row.Id, row.ShiftDate, row.Shift, row.FromAreaId, row.ToAreaId,
                        Utc(row.EffectiveAt),
                        confirmations.Where(confirmation => confirmation.TransferId == row.Id)
                            .Select(confirmation => (DateTimeOffset?)Utc(confirmation.OccurredAt)).FirstOrDefault(),
                        row.IsCancelled, Revision(row.Revision)))
                    .ToList());

            ScheduleDay ReadDay(DateOnly date)
            {
                var day = ScheduleResolver.Resolve(input, date) with { Shift = writtenShiftOn[date] };
                if (actor.Role == UserRole.Cleaner || day.Shift is not Shift slotShift
                    || date < historyStartsAt)
                {
                    return day;
                }

                var activeProfile = personProfiles.LastOrDefault(entry =>
                    ScheduleResolver.Applies(entry.EffectiveFrom, entry.EffectiveUntil, date));
                var applicable = personBaselines.Where(entry =>
                    ScheduleResolver.Applies(entry.EffectiveFrom, entry.EffectiveUntil, date)).ToList();
                var building = activeProfile?.HomeBuildingId;
                if (activeProfile is not null && building is null)
                {
                    var targetArea = applicable.SelectMany(entry => entry.Days).OrderBy(cell => cell.Weekday)
                        .FirstOrDefault(cell => cell.AreaId is not null)?.AreaId;
                    building = targetArea is int id ? areaBuildings.GetValueOrDefault(id) : null;
                }
                else if (activeProfile is null && personProfiles.Count == 0)
                {
                    building = person.AreaId is int id ? areaBuildings.GetValueOrDefault(id) : person.BuildingId;
                }

                var canChange = building is int targetBuilding && (date < businessDate
                    ? ScheduleAuthority.CanCorrectHistory(actor.Role, actor.Id, targetBuilding, slotShift, date, businessDate,
                        managementScope)
                    : ScheduleAuthority.CanManage(actor.Role, actor.Id, targetBuilding, slotShift, date, managementScope));
                var baseline = applicable.LastOrDefault(entry => activeProfile?.Kind switch
                {
                    CleanerKind.Regular => entry.Kind == BaselineKind.Regular,
                    CleanerKind.Relief => entry.Kind == BaselineKind.Relief,
                    _ => true,
                })?.Days.FirstOrDefault(cell => cell.Weekday == date.DayOfWeek);
                var live = exceptions.Where(row => row.UserId == person.Id && row.Shift == slotShift
                    && row.ShiftDate == date && !row.IsCancelled).ToList();
                var leave = live.Find(row => row.Kind == ScheduleExceptionKind.Leave);
                var prefix = FormattableString.Invariant($"exception:{person.Id}:{date:yyyy-MM-dd}:{slotShift}:");
                var corrections = correctionAudits?[prefix + "area"]
                    .Concat(correctionAudits[prefix + "breakgroup"]).OrderBy(change => change.At).ToList() ?? [];
                return day with
                {
                    Edit = new ScheduleEditFacts(canChange, header?.Choices.Any(choice => choice.OffDate == date) == true,
                        baseline?.AreaId, baseline?.BreakGroup, baseline?.IsDayOff == true,
                        Revision(live.Find(row => row.Kind == ScheduleExceptionKind.Area)?.Revision ?? 0),
                        Revision(live.Find(row => row.Kind == ScheduleExceptionKind.BreakGroup)?.Revision ?? 0),
                        leave is null ? null : new ScheduleLeaveEdit(Revision(leave.Revision),
                            Utc(leave.StartsAt!.Value), Utc(leave.EndsAt!.Value)),
                        canChange && building is int knownBuilding ? optionsByBuilding.GetValueOrDefault(knownBuilding) ?? [] : [],
                        corrections),
                };
            }

            var personDays = readDates[person.Id].Select(ReadDay).ToList();
            bool? weeklyReplaceAllowed = actor.Role == UserRole.Cleaner ? null
                : header?.Choices.All(choice => personDays.Any(day =>
                    day.Date == choice.OffDate && day.Edit?.CanChange == true)) ?? true;
            var timelineRevision = Revision(baselineAnchors.GetValueOrDefault(person.Id));
            var homeBuilding = HomeBuilding(memberships[person.Id], profiles.Exists(entry => entry.UserId == person.Id), person);
            rows.Add(new SchedulePersonWeek(person.Id, person.DisplayName,
                profile?.Kind.ToString() ?? UnknownCleanerKind,
                homeBuilding,
                header is not null,
                personDays,
                // Regular and relief share the per-person timeline anchor used by writes. A missing anchor starts at 0.
                Revision(header?.Revision ?? 0),
                timelineRevision,
                timelineRevision,
                weeklyReplaceAllowed,
                homeBuilding is int homeId ? buildingCodes.GetValueOrDefault(homeId) : null));

            revisions.Add($"baseline-anchor#{person.Id}@{timelineRevision}");
            revisions.AddRange(personProfiles.Select(entry => $"profile#{entry.Id}@{Revision(entry.Revision)}"));
            revisions.AddRange(personBaselines.Select(entry => $"baseline#{entry.Id}@{Revision(entry.Revision)}"));
            if (header is not null)
            {
                revisions.Add($"week#{header.Id}@{Revision(header.Revision)}");
            }

            revisions.AddRange(exceptions.Where(row => row.UserId == person.Id)
                .Select(row => $"exception#{row.Id}@{Revision(row.Revision)}"));
            revisions.AddRange(transfers.Where(row => row.PersonId == person.Id)
                .Select(row => $"transfer#{row.Id}@{Revision(row.Revision)}"));
        }

        var availability = transferSlots.Count == 0 && rows.All(row =>
            row.Days.All(day => day.Availability == ScheduleResolver.AvailabilityUnavailable))
            ? ScheduleResolver.AvailabilityUnavailable
            : ScheduleResolver.AvailabilityAvailable;

        // A relief row is part of what the week was built from, so its revision moves the week digest too.
        revisions.AddRange(relief.Select(row => $"relief#{row.Id}@{row.Revision}"));
        revisions.AddRange(transferSlots.Select(TransferSlotRevision));
        return new ScheduleProjection(monday, rows, WeekRevision(revisions), readAt, availability, relief, destinations,
            sourceBuildings, transferSlots);
    }

    /// <summary>
    /// Spec §3 and §6 (controller ruling e): the relief assignments of the read week, scoped to the reader. A Cleaner sees
    /// only their own; a Supervisor only rows whose destination building, shift and date their dated periods hold; an Admin
    /// everything. A supplied building or shift narrows and never widens, exactly as it does for the roster. Cancelled rows
    /// stay in the list with their state, because history that vanished would make "it was recorded, then cancelled"
    /// unreadable (spec §10), and each row carries the names and codes the screen shows — never another team's schedule.
    /// </summary>
    private async Task<IReadOnlyList<ReliefAssignmentFact>> ReadReliefAsync(
        User actor, DateOnly monday, DateOnly weekEnd, int? buildingId, Shift? shift,
        IReadOnlyList<ScopePeriod>? authorised, CancellationToken ct)
    {
        var stored = await db.ReliefAssignments.AsNoTracking()
            .Where(row => row.ShiftDate >= monday && row.ShiftDate < weekEnd)
            .OrderBy(row => row.ShiftDate).ThenBy(row => row.Shift).ThenBy(row => row.Id)
            .ToListAsync(ct);
        if (stored.Count == 0)
        {
            return [];
        }

        var areaIds = stored.Select(row => row.AreaId).Distinct().ToList();
        var areas = await db.Areas.AsNoTracking()
            .Where(area => areaIds.Contains(area.Id))
            .Select(area => new { area.Id, area.Code, area.BuildingId })
            .ToDictionaryAsync(area => area.Id, area => area, ct);
        var buildings = await db.Buildings.AsNoTracking()
            .Select(building => new { building.Id, building.Code })
            .ToDictionaryAsync(building => building.Id, building => building.Code, ct);
        var personIds = stored.Select(row => row.UserId)
            .Concat(stored.Where(row => row.ReplacedUserId != null).Select(row => row.ReplacedUserId!.Value))
            .Concat(stored.Where(row => row.SourceSupervisorId != null).Select(row => row.SourceSupervisorId!.Value))
            .Distinct()
            .ToList();
        var names = await db.Users.AsNoTracking()
            .Where(person => personIds.Contains(person.Id))
            .Select(person => new { person.Id, person.DisplayName })
            .ToDictionaryAsync(person => person.Id, person => person.DisplayName, ct);

        var visible = new List<ReliefAssignmentFact>();
        foreach (var row in stored)
        {
            if (!areas.TryGetValue(row.AreaId, out var area))
            {
                continue; // a row whose destination the read cannot name is not describable, so it is not shown
            }

            if (buildingId is int onlyBuilding && area.BuildingId != onlyBuilding)
            {
                continue;
            }

            if (shift is Shift onlyShift && row.Shift != onlyShift)
            {
                continue;
            }

            if (actor.Role == UserRole.Cleaner)
            {
                if (row.UserId != actor.Id)
                {
                    continue;
                }
            }
            else if (authorised is not null && !authorised.Any(period =>
                period.BuildingId == area.BuildingId && period.Shift == row.Shift
                && ScheduleAuthority.Covers(period, row.ShiftDate)))
            {
                continue;
            }

            visible.Add(new ReliefAssignmentFact(
                row.Id,
                row.UserId,
                names.GetValueOrDefault(row.UserId, string.Empty),
                row.ShiftDate,
                row.Shift,
                row.AreaId,
                area.Code,
                area.BuildingId,
                buildings.GetValueOrDefault(area.BuildingId, string.Empty),
                row.BreakGroup,
                row.ReplacedUserId,
                row.ReplacedUserId is int replaced ? names.GetValueOrDefault(replaced) : null,
                row.SourceBuildingId,
                buildings.GetValueOrDefault(row.SourceBuildingId, string.Empty),
                row.SourceSupervisorId,
                row.SourceSupervisorId is int source ? names.GetValueOrDefault(source) : null,
                row.AgreementReported,
                row.IsCancelled,
                Revision(row.Revision)));
        }

        return visible;
    }

    private sealed record CorrectionAudit(
        string Result, string Actor, DateTime At, string? Before, string After, string Reason);

    private async Task<ILookup<string, ScheduleCorrection>> ReadCorrectionAuditsAsync(
        DateOnly monday, DateOnly weekEnd, CancellationToken ct)
    {
        // The retained receipt identifies the slot even for creates, whose audit has no database entity id yet.
        // Narrow the read by month in SQL, then match only exact keys of the caller's covered dates.
        var firstMonth = monday.ToString("yyyy-MM-", CultureInfo.InvariantCulture);
        var lastMonth = weekEnd.AddDays(-1).ToString("yyyy-MM-", CultureInfo.InvariantCulture);
        var audits = await (from change in db.ScheduleChanges.AsNoTracking()
                            join receipt in db.ScheduleCommandReceipts on change.ReceiptId equals receipt.Id
                            join author in db.Users on receipt.ActorId equals author.Id
                            where receipt.Kind == "correct-plan" && change.EntityType == "ScheduleException"
                                && change.AfterJson != null && change.Reason != null
                                && (receipt.ResultJson.Contains(firstMonth) || receipt.ResultJson.Contains(lastMonth))
                            orderby change.Id
                            select new CorrectionAudit(receipt.ResultJson, author.DisplayName, change.CreatedAt,
                                change.BeforeJson, change.AfterJson!, change.Reason!)).ToListAsync(ct);
        return audits.SelectMany(ProjectCorrection).ToLookup(entry => entry.Key, entry => entry.Correction);
    }

    private static IEnumerable<(string Key, ScheduleCorrection Correction)> ProjectCorrection(CorrectionAudit audit)
    {
        using var result = JsonDocument.Parse(audit.Result);
        if (!result.RootElement.TryGetProperty("Revisions", out var revisions))
        {
            yield break;
        }

        using var after = JsonDocument.Parse(audit.After);
        using var before = audit.Before is null ? null : JsonDocument.Parse(audit.Before);
        var isArea = AuditNumber(after, "areaId") is not null || AuditNumber(before, "areaId") is not null;
        var field = isArea ? "areaId" : "breakGroup";
        var suffix = isArea ? ":area" : ":breakgroup";
        foreach (var revision in revisions.EnumerateObject())
        {
            if (revision.Name.EndsWith(suffix, StringComparison.Ordinal))
            {
                yield return (revision.Name, new ScheduleCorrection(field, Utc(audit.At), audit.Actor, audit.Reason,
                    AuditNumber(before, field),
                    after.RootElement.GetProperty("cancelled").GetBoolean() ? null : AuditNumber(after, field)));
            }
        }
    }

    private static int? AuditNumber(JsonDocument? audit, string field) =>
        audit is not null && audit.RootElement.TryGetProperty(field, out var value)
            && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

    /// <summary>
    /// Which dated (building, shift) memberships the person's profile periods give them, used for scope and roster
    /// membership only. Each period that overlaps the read week is one dated membership of the same shape as a scope
    /// period, so <see cref="ScheduleAuthority.Covers"/> answers "is this person a member on this date" with the one
    /// day-level predicate the design has. A regular Cleaner's building comes from the Areas of their own baseline versions;
    /// when the person has no dated identity at all the compatibility columns are the only membership there is (spec §14),
    /// and they never become history.
    /// <para>
    /// Controller ruling for Task 7: the compatibility projection is the membership of the dates a dated profile does not
    /// cover — the dates of this read that lie before the person's first dated identity — not merely of the "no dated row
    /// at all" state. Writing a profile that starts today therefore does not delete the earlier dates from the read, and
    /// it still adds no projection membership to a date a profile has taken over.
    /// </para>
    /// </summary>
    private static List<ScopePeriod> MembershipSlots(
        User person, IReadOnlyList<CleanerProfilePeriod> personProfiles, IReadOnlyList<ScheduleBaseline> baselines,
        IReadOnlyDictionary<int, int> areaBuildings, DateOnly monday, DateOnly weekEnd)
    {
        var slots = new List<ScopePeriod>();
        foreach (var profile in personProfiles
                     .Where(profile => OverlapsWeek(profile.EffectiveFrom, profile.EffectiveUntil, monday, weekEnd))
                     .OrderBy(profile => profile.EffectiveFrom).ThenBy(profile => profile.Id))
        {
            if ((profile.HomeBuildingId ?? BaselineBuilding(baselines, profile, areaBuildings)) is int building
                && profile.Shift is Shift profileShift)
            {
                slots.Add(new ScopePeriod(person.Id, building, profileShift,
                    profile.EffectiveFrom, profile.EffectiveUntil));
            }
        }

        // The read's dates before that first dated identity — the whole week for a pre-backfill account, and the days
        // before today for somebody whose first profile was written today. The range is the read week's own, so the
        // projection never claims a date this read does not cover.
        var firstDated = personProfiles.Count == 0
            ? (DateOnly?)null
            : personProfiles.Min(profile => profile.EffectiveFrom);
        if ((firstDated ?? weekEnd) > monday)
        {
            var building = person.AreaId is int area && areaBuildings.TryGetValue(area, out var fromArea)
                ? fromArea
                : person.BuildingId;
            if (building is int known && person.Shift is Shift shift)
            {
                slots.Add(new ScopePeriod(person.Id, known, shift, monday,
                    firstDated is { } start && start < weekEnd ? start : weekEnd));
            }
        }

        return slots;
    }

    /// <summary>
    /// The building of the first Area named by a regular baseline version whose own range overlaps the profile's range, so a
    /// version that never applied while the profile did cannot decide which building the person belongs to.
    /// </summary>
    private static int? BaselineBuilding(
        IReadOnlyList<ScheduleBaseline> baselines, CleanerProfilePeriod profile,
        IReadOnlyDictionary<int, int> areaBuildings) =>
        baselines
            .Where(baseline => baseline.Kind == BaselineKind.Regular
                && baseline.EffectiveFrom < (profile.EffectiveUntil ?? DateOnly.MaxValue)
                && profile.EffectiveFrom < (baseline.EffectiveUntil ?? DateOnly.MaxValue))
            .OrderBy(baseline => baseline.EffectiveFrom).ThenBy(baseline => baseline.Id)
            .SelectMany(baseline => baseline.Days)
            .Select(day => day.AreaId is int areaId && areaBuildings.TryGetValue(areaId, out var building)
                ? building
                : (int?)null)
            .FirstOrDefault(building => building is not null);

    /// <summary>
    /// The building the row shows. A dated identity that does not overlap the read week is not a membership of it, so such a
    /// person shows no home building rather than the compatibility projection's current value; a person with no dated
    /// identity at all keeps that projection as its only membership.
    /// </summary>
    private static int? HomeBuilding(IReadOnlyList<ScopePeriod> slots, bool hasDatedProfile, User person) =>
        slots.Count > 0 ? slots[0].BuildingId : hasDatedProfile ? null : person.BuildingId;

    /// <summary>True when a half-open effective range has a date in the read week — the same boundary as Applies.</summary>
    private static bool OverlapsWeek(DateOnly from, DateOnly? until, DateOnly monday, DateOnly weekEnd) =>
        from < weekEnd && (until is null || until.Value > monday);

    /// <summary>An opaque token over every row the week was built from, so a client can tell a changed week from a read.</summary>
    private static string WeekRevision(List<string> revisions)
    {
        revisions.Sort(StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";", revisions))))
            .ToLowerInvariant();
    }

    /// <summary>A stored revision as the decimal string the contract serialises it as.</summary>
    private static string Revision(long revision) => revision.ToString(CultureInfo.InvariantCulture);

    /// <summary>A stored DATETIME is UTC; the offset is attached explicitly instead of from the machine's zone.</summary>
    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>
    /// A week the actor has no roster row for. It is not "an empty week of work": nothing is readable at all, so the
    /// availability word says so unless the reader can see standalone relief or transfer slots.
    /// </summary>
    private static ScheduleProjection Empty(
        DateOnly monday, DateTimeOffset readAt, IReadOnlyList<ReliefAssignmentFact>? relief = null,
        IReadOnlyList<ScheduleTransferSlot>? transferSlots = null)
    {
        IReadOnlyList<ReliefAssignmentFact> rows = relief ?? [];
        var revisions = rows.Select(row => $"relief#{row.Id}@{row.Revision}")
            .Concat((transferSlots ?? []).Select(TransferSlotRevision)).ToList();
        return new ScheduleProjection(
            monday, [], revisions.Count == 0 ? ScheduleResolver.NoRevision : WeekRevision(revisions), readAt,
            revisions.Count == 0 ? AvailabilityEmpty : ScheduleResolver.AvailabilityAvailable, rows,
            TransferSlots: transferSlots);
    }

    private static string TransferSlotRevision(ScheduleTransferSlot slot) =>
        $"transfer-slot#{slot.Id}@{slot.Revision}:{slot.ConfirmedAt?.UtcTicks}";

    /// <summary>The same rule the ck_*_effective_range CHECK constraints apply, so a caller hears about it first.</summary>
    private static void EnsureRangeIsForward(DateOnly effectiveFrom, DateOnly? effectiveUntil)
    {
        if (effectiveUntil is { } until && until <= effectiveFrom)
        {
            throw new ScheduleConflictException("invalid-range");
        }
    }
}

/// <summary>A scheduling operation refused because it would overlap another person's effective responsibility.</summary>
public sealed class ScheduleConflictException(string code)
    : Exception($"The schedule change conflicts with existing responsibility ({code}).")
{
    public string Code { get; } = code;
}

/// <summary>
/// Spec §9: one slot's midshift transfer as it is stored — the latest instruction, the single confirmation recorded under
/// that instruction if any, and the destination of the most recent confirmed instruction. Nothing here decides what the
/// trio means: a cancelled instruction holds no responsibility, an unconfirmed one in force leaves no usable Area, a
/// confirmed one makes its destination the duty, and a forward instruction builds on <see cref="LastConfirmedAreaId"/>
/// until it takes effect.
/// </summary>
public sealed record SlotTransfer(
    AreaTransfer Instruction,
    AreaTransferConfirmation? Confirmation,
    int? LastConfirmedAreaId = null);
