using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JanitorialManagementDashboard.Application.Scheduling;
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
        var anchor = await db.ScheduleAnchors.FirstOrDefaultAsync(
            row => row.PersonId == personId && row.ShiftDate == shiftDate && row.Shift == shift, ct);
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
        int personId, DateTime nowUtc, CancellationToken ct = default)
    {
        var anchor = await db.ScheduleBaselineAnchors.FirstOrDefaultAsync(row => row.PersonId == personId, ct);
        if (anchor is null)
        {
            var created = new ScheduleBaselineAnchor { PersonId = personId, Revision = 1, UpdatedAt = nowUtc };
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

        anchor.Revision += 1;
        anchor.UpdatedAt = nowUtc;
        await db.SaveChangesAsync(ct);
        return anchor;
    }

    /// <summary>
    /// The slot anchor this transaction just failed to create, read with a locking read on MySQL so the winner's row is
    /// visible even though this transaction began before that commit. A plain read (SQLite) cannot answer from an older
    /// snapshot, so it stays plain.
    /// </summary>
    private async Task<ScheduleAnchor?> FindSlotAnchorUnderLockAsync(
        int personId, DateOnly shiftDate, Shift shift, CancellationToken ct)
    {
        if (IsMySql)
        {
            var rows = await db.ScheduleAnchors
                .FromSqlRaw("SELECT * FROM schedule_anchors WHERE person_id = {0} FOR UPDATE", personId)
                .ToListAsync(ct);
            return rows.FirstOrDefault(row => row.ShiftDate == shiftDate && row.Shift == shift);
        }

        var existing = await db.ScheduleAnchors.AsNoTracking().FirstOrDefaultAsync(
            row => row.PersonId == personId && row.ShiftDate == shiftDate && row.Shift == shift, ct);
        return existing is null ? null : await db.ScheduleAnchors.FirstAsync(row => row.Id == existing.Id, ct);
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
        if (actor.Role == UserRole.Supervisor)
        {
            authorised = (await SupervisorScopes.ReadAsync(db, actor, clock ?? TimeProvider.System, ct))
                .Where(period => buildingId is null || period.BuildingId == buildingId)
                .Where(period => shift is null || period.Shift == shift)
                .Where(period => dates.Exists(date => ScheduleAuthority.Covers(period, date)))
                .ToList();
            if (authorised.Count == 0)
            {
                return Empty(monday, readAt);
            }
        }

        var personIds = roster.Select(person => person.Id).ToHashSet();
        if (personIds.Count == 0)
        {
            return Empty(monday, readAt);
        }

        var profiles = await db.CleanerProfilePeriods.AsNoTracking()
            .Where(period => personIds.Contains(period.UserId)).ToListAsync(ct);
        var baselines = await db.ScheduleBaselines.AsNoTracking().Include(baseline => baseline.Days)
            .Where(baseline => personIds.Contains(baseline.UserId)).ToListAsync(ct);
        var areaBuildings = await db.Areas.AsNoTracking().Select(area => new { area.Id, area.BuildingId })
            .ToDictionaryAsync(area => area.Id, area => area.BuildingId, ct);

        // Membership only (spec §14): the compatibility projection may say which building a person belongs to, never what
        // their history was. Only the periods that overlap the read week are membership at all, so a person who left a
        // building stops counting as its member on the dates their period no longer covers.
        var memberships = roster.ToDictionary(person => person.Id, person => MembershipSlots(
            person,
            profiles.Where(profile => profile.UserId == person.Id
                && OverlapsWeek(profile.EffectiveFrom, profile.EffectiveUntil, monday, weekEnd)).ToList(),
            baselines.Where(baseline => baseline.UserId == person.Id).ToList(),
            areaBuildings,
            profiles.Exists(profile => profile.UserId == person.Id)));

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
        if (included.Count == 0)
        {
            return Empty(monday, readAt);
        }

        var includedIds = included.Select(person => person.Id).ToList();
        var exceptions = await db.ScheduleExceptions.AsNoTracking()
            .Where(row => includedIds.Contains(row.UserId) && row.ShiftDate >= monday && row.ShiftDate < weekEnd)
            .ToListAsync(ct);
        var savedWeeks = await db.WeeklyDayOffs.AsNoTracking().Include(week => week.Choices)
            .Where(week => includedIds.Contains(week.UserId) && week.WeekStart == monday)
            .ToListAsync(ct);
        var transfers = await db.AreaTransfers.AsNoTracking()
            .Where(row => includedIds.Contains(row.PersonId) && row.ShiftDate >= monday && row.ShiftDate < weekEnd)
            .ToListAsync(ct);
        var transferIds = transfers.Select(row => row.Id).ToList();
        var confirmations = transferIds.Count == 0
            ? []
            : await db.AreaTransferConfirmations.AsNoTracking()
                .Where(row => transferIds.Contains(row.TransferId)).ToListAsync(ct);

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

            rows.Add(new SchedulePersonWeek(person.Id, person.DisplayName,
                profile?.Kind.ToString() ?? UnknownCleanerKind,
                HomeBuilding(memberships[person.Id], profiles.Exists(entry => entry.UserId == person.Id), person),
                header is not null,
                readDates[person.Id].Select(date => ScheduleResolver.Resolve(input, date)).ToList()));

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

        var availability = rows.All(row =>
            row.Days.All(day => day.Availability == ScheduleResolver.AvailabilityUnavailable))
            ? ScheduleResolver.AvailabilityUnavailable
            : ScheduleResolver.AvailabilityAvailable;

        return new ScheduleProjection(monday, rows, WeekRevision(revisions), readAt, availability);
    }

    /// <summary>
    /// Which dated (building, shift) memberships the person's profile periods give them, used for scope and roster
    /// membership only. Each period that overlaps the read week is one dated membership of the same shape as a scope
    /// period, so <see cref="ScheduleAuthority.Covers"/> answers "is this person a member on this date" with the one
    /// day-level predicate the design has. A regular Cleaner's building comes from the Areas of their own baseline versions;
    /// when the person has no dated identity at all the compatibility columns are the only membership there is (spec §14),
    /// and they never become history.
    /// </summary>
    private static List<ScopePeriod> MembershipSlots(
        User person, IReadOnlyList<CleanerProfilePeriod> weekProfiles, IReadOnlyList<ScheduleBaseline> baselines,
        IReadOnlyDictionary<int, int> areaBuildings, bool hasDatedProfile)
    {
        var slots = new List<ScopePeriod>();
        foreach (var profile in weekProfiles)
        {
            if ((profile.HomeBuildingId ?? BaselineBuilding(baselines, profile, areaBuildings)) is int building)
            {
                slots.Add(new ScopePeriod(person.Id, building, profile.Shift,
                    profile.EffectiveFrom, profile.EffectiveUntil));
            }
        }

        if (slots.Count == 0 && !hasDatedProfile)
        {
            var building = person.AreaId is int area && areaBuildings.TryGetValue(area, out var fromArea)
                ? fromArea
                : person.BuildingId;
            if (building is int known && person.Shift is Shift shift)
            {
                slots.Add(new ScopePeriod(person.Id, known, shift, DateOnly.MinValue, null));
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

    private static ScheduleProjection Empty(DateOnly monday, DateTimeOffset readAt) =>
        new(monday, [], ScheduleResolver.NoRevision, readAt, AvailabilityEmpty);

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
