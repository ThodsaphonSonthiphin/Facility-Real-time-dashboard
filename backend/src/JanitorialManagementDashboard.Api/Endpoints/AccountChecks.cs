using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>Where an account works (Area or building + shift), and the checks that keep login names and slots unique.</summary>
internal static class AccountChecks
{
    public const string EmployeeIdTaken = "รหัสพนักงานนี้มีแล้ว";
    public const string UsernameTaken = "ชื่อผู้ใช้นี้มีแล้ว";

    /// <summary>
    /// The place a form asks for, checked against the role. Area and Building are tracked entities or null.
    /// <see cref="HomeBuildingId"/> is the dated identity a Cleaner's profile will state: a Relief Cleaner's own home
    /// building, or the building a regular Cleaner's Area belongs to (facility-0137/0143/0194).
    /// </summary>
    public sealed record AccountPlace(Area? Area, Building? Building, Shift? Shift, int? HomeBuildingId = null);

    /// <summary>The regular/relief identity of a Cleaner as the stored records state it on a date.</summary>
    public sealed record CleanerIdentity(CleanerKind Kind, int? HomeBuildingId, Shift? Shift);

    /// <summary>What a save claims: its login name and its slot. A new account has UserId 0.</summary>
    public sealed record AccountClaim(int UserId, UserRole Role, string? Username, string? EmployeeId, int? AreaId, int? BuildingId, Shift? Shift);

    /// <summary>A deactivated account holds no slot (users.cleaner_slot and supervisor_slot are NULL for it).</summary>
    public static AccountClaim ClaimOf(User user) => new(
        user.Id,
        user.Role,
        user.Username,
        user.EmployeeId,
        user.IsActive ? user.AreaId : null,
        user.IsActive ? user.BuildingId : null,
        user.Shift);

    /// <summary>
    /// Cleaner: shift required, Area optional, no building. Supervisor: building and shift, no Area. Admin: none of them.
    /// <paramref name="userId"/> is the account being edited (0 for a new one), so it never collides with itself.
    /// <paramref name="businessDate"/> is the clock's Thai date, the date the dated Supervisor membership is read for.
    /// <paramref name="checkSlot"/> is false for a deactivated account, which holds no slot; activation checks it later.
    /// <paramref name="currentAreaId"/> is the Cleaner's Area now: keeping it is allowed even after the Area was deactivated
    /// (Area deactivation does not unassign Cleaners), but moving to an inactive Area is not.
    /// <para>
    /// Controller ruling (a) for Task 7: an absent <paramref name="cleanerKind"/> keeps the behaviour every existing caller
    /// has (a regular Cleaner, whose Area may be empty), an explicit <see cref="CleanerKind.Regular"/> needs an Area because
    /// that is where its home building comes from, and a <see cref="CleanerKind.Relief"/> has a home building and no Area —
    /// a relief's home building is not a Supervisor slot and never occupies <c>users.supervisor_slot</c>. A
    /// <paramref name="cleanerKind"/> the enum does not define is refused up front, so no stored kind can be a number no
    /// reader renders (the wire accepts integer enum values).
    /// </para>
    /// </summary>
    public static async Task<(AccountPlace? Place, IResult? Error)> ReadPlaceAsync(
        AppDbContext db, UserRole role, int userId, int? areaId, int? buildingId, Shift? shift, DateOnly businessDate,
        bool checkSlot = true, int? currentAreaId = null, CleanerKind? cleanerKind = null, int? homeBuildingId = null)
    {
        if (shift is { } given && !Enum.IsDefined(given))
        {
            return (null, Bad("เลือกกะเช้าหรือกะดึก"));
        }

        // The wire accepts any number for an enum (JsonStringEnumConverter allows integer values), so a value the enum does
        // not define must be refused here rather than compared against Regular/Relief, fall through as "neither" and reach
        // cleaner_profile_periods.kind as a number no reader can render.
        if (cleanerKind is { } givenKind && !Enum.IsDefined(givenKind))
        {
            return (null, Bad("ประเภทแม่บ้านต้องเป็นประจำหรือสแปร์เท่านั้น"));
        }

        switch (role)
        {
            case UserRole.Admin:
                return areaId is null && buildingId is null && shift is null
                    ? (new AccountPlace(null, null, null), null)
                    : (null, Bad("บัญชีแอดมินไม่มีพื้นที่ ตึก หรือกะ"));

            case UserRole.Cleaner:
            {
                if (buildingId is not null)
                {
                    return (null, Bad("แม่บ้านประจำพื้นที่ ไม่ได้ประจำตึก"));
                }

                if (cleanerKind == CleanerKind.Relief)
                {
                    if (shift is not { } cleanerShift)
                    {
                        return (null, Bad("ต้องเลือกกะของแม่บ้าน"));
                    }

                    if (areaId is not null)
                    {
                        return (null, Bad("สแปร์ไม่มีพื้นที่ประจำ เลือกได้เฉพาะตึกต้นทางกับกะ"));
                    }

                    if (homeBuildingId is null)
                    {
                        return (null, Bad("ต้องเลือกตึกต้นทางของสแปร์"));
                    }

                    var home = await db.Buildings.FirstOrDefaultAsync(b => b.Id == homeBuildingId && b.IsActive);
                    return home is null
                        ? (null, Bad("ต้องเลือกตึกต้นทางที่ใช้งานอยู่"))
                        // A relief's home building lives in the dated profile, not in the Supervisor building column.
                        : (new AccountPlace(null, null, cleanerShift, home.Id), null);
                }

                if (areaId is int chosenAreaId)
                {
                    if (shift is not { } cleanerShift)
                    {
                        return (null, Bad("ต้องเลือกกะของแม่บ้าน"));
                    }

                    var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == chosenAreaId && (a.IsActive || a.Id == currentAreaId));
                    if (area is null)
                    {
                        return (null, Bad("ต้องเลือกพื้นที่ที่ใช้งานอยู่"));
                    }

                    // Not HasShift: it is false for an inactive Area, and a kept inactive Area must still pass when the shift fits
                    if (cleanerShift == Shift.Night && area.ShiftPattern != ShiftPattern.DayAndNight)
                    {
                        return (null, Bad($"{area.Code} ทำเฉพาะกะเช้า เลือกกะดึกไม่ได้"));
                    }

                    if (homeBuildingId is int chosen && chosen != area.BuildingId)
                    {
                        return (null, Bad("ตึกของแม่บ้านประจำมาจากพื้นที่ เลือกเองไม่ได้"));
                    }

                    return checkSlot && await HolderAsync(db, role, area.Id, null, cleanerShift, userId, businessDate) is { } holder
                        ? (null, Conflict(SlotTaken(holder)))
                        : (new AccountPlace(area, null, cleanerShift, area.BuildingId), null);
                }

                if (cleanerKind == CleanerKind.Regular && homeBuildingId is null)
                {
                    return (null, Bad("แม่บ้านประจำต้องเลือกตึกประจำหรือพื้นที่ประจำ"));
                }

                if (homeBuildingId is int chosenBuildingId)
                {
                    var home = await db.Buildings.FirstOrDefaultAsync(b => b.Id == chosenBuildingId && b.IsActive);
                    return home is null
                        ? (null, Bad("ต้องเลือกตึกประจำที่ใช้งานอยู่"))
                        : (new AccountPlace(null, null, shift, home.Id), null);
                }

                if (shift is null)
                {
                    return (null, Bad("ต้องเลือกกะของแม่บ้าน"));
                }

                return (new AccountPlace(null, null, shift), null);
            }

            default:
            {
                if (areaId is not null)
                {
                    return (null, Bad("หัวหน้าประจำตึก ไม่ได้ประจำพื้นที่"));
                }

                if (buildingId is null || shift is not { } supervisorShift)
                {
                    return (null, Bad("ต้องเลือกตึกและกะของหัวหน้า"));
                }

                var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId && b.IsActive);
                if (building is null)
                {
                    return (null, Bad("ต้องเลือกตึกที่ใช้งานอยู่"));
                }

                return checkSlot && await HolderAsync(db, role, null, building.Id, supervisorShift, userId, businessDate) is { } holder
                    ? (null, Conflict(SlotTaken(holder)))
                    : (new AccountPlace(null, building, supervisorShift), null);
            }
        }
    }

    /// <summary>
    /// The identity a Cleaner's stored records state on <paramref name="businessDate"/>: their dated profile when one covers
    /// the date, else the account row's own projection — the same compatibility rule the week read applies (spec §14), so the
    /// form can tell "nothing changed" from "this edit starts a new identity today" without inventing a past.
    /// </summary>
    public static async Task<CleanerIdentity> CurrentCleanerIdentityAsync(
        AppDbContext db, User user, DateOnly businessDate, CancellationToken ct = default)
    {
        var period = await CurrentCleanerProfileAsync(db, user.Id, businessDate, ct);
        if (period is not null)
        {
            return new CleanerIdentity(period.Kind, period.HomeBuildingId, period.Shift);
        }

        // A pre-backfill Cleaner has no dated row at all; their Area is the only statement of which building they belong to.
        var home = user.AreaId is int areaId
            ? await db.Areas.AsNoTracking().Where(area => area.Id == areaId)
                .Select(area => (int?)area.BuildingId).FirstOrDefaultAsync(ct)
            : null;
        return new CleanerIdentity(CleanerKind.Regular, home, user.Shift);
    }

    /// <summary>The dated profile period in force on that date, or null when the person has none covering it.</summary>
    public static async Task<CleanerProfilePeriod?> CurrentCleanerProfileAsync(
        AppDbContext db, int userId, DateOnly businessDate, CancellationToken ct = default)
    {
        var periods = await db.CleanerProfilePeriods.AsNoTracking()
            .Where(period => period.UserId == userId)
            .OrderBy(period => period.EffectiveFrom).ThenBy(period => period.Id)
            .ToListAsync(ct);
        return periods.LastOrDefault(period =>
            ScheduleResolver.Applies(period.EffectiveFrom, period.EffectiveUntil, businessDate));
    }

    /// <summary>
    /// The other active account in that Area-shift (Cleaner) or building-shift (Supervisor) slot on
    /// <paramref name="businessDate"/>, with its Area or Building loaded so the message can name it.
    /// </summary>
    public static async Task<User?> HolderAsync(
        AppDbContext db, UserRole role, int? areaId, int? buildingId, Shift? shift, int exceptUserId, DateOnly businessDate) =>
        role switch
        {
            UserRole.Cleaner when areaId is not null && shift is not null => await CleanerHolderAsync(
                db, areaId.Value, shift.Value, exceptUserId, businessDate),
            UserRole.Supervisor when buildingId is not null && shift is not null => await SupervisorHolderAsync(
                db, buildingId.Value, shift.Value, exceptUserId, businessDate),
            _ => null,
        };

    private static async Task<User?> CleanerHolderAsync(
        AppDbContext db, int areaId, Shift shift, int exceptUserId, DateOnly businessDate)
    {
        var conflict = await RegularBaselineOverlap.FindSlotConflictAsync(db, exceptUserId, areaId, shift, businessDate);
        if (conflict is null)
        {
            return null;
        }

        var holder = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == conflict.HolderUserId);
        if (holder is null)
        {
            return null;
        }

        holder.Area = await db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == conflict.AreaId);
        holder.Shift = conflict.Shift;
        return holder;
    }

    private static Task<User?> ProjectionCleanerAsync(AppDbContext db, int areaId, Shift shift, int exceptUserId) =>
        db.Users.AsNoTracking().Include(u => u.Area)
            .FirstOrDefaultAsync(u => u.Id != exceptUserId && u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == areaId && u.Shift == shift);

    /// <summary>
    /// Spec §3: the active Supervisor whose dated period covers <paramref name="businessDate"/> for that building and
    /// shift. The dated rows are the authority, so a building and shift the dated rows have freed is free even when the
    /// account projection still says otherwise. The projection is read only while no dated row exists at all, the
    /// pre-backfill state of spec §14; once one exists it is the only source, both ways.
    /// </summary>
    private static async Task<User?> SupervisorHolderAsync(
        AppDbContext db, int buildingId, Shift shift, int exceptUserId, DateOnly businessDate)
    {
        var holderId = await (
                from period in db.SupervisorScopePeriods.AsNoTracking()
                join holder in db.Users.AsNoTracking() on period.SupervisorId equals holder.Id
                where period.BuildingId == buildingId && period.Shift == shift && period.SupervisorId != exceptUserId
                    && period.EffectiveFrom <= businessDate
                    && (period.EffectiveUntil == null || businessDate < period.EffectiveUntil.Value)
                    && holder.Role == UserRole.Supervisor && holder.IsActive
                select (int?)period.SupervisorId)
            .FirstOrDefaultAsync();
        if (holderId is not null)
        {
            return await HeldAsUserAsync(db, holderId.Value, buildingId, shift);
        }

        if (await db.SupervisorScopePeriods.AsNoTracking().AnyAsync())
        {
            // Dated rows exist and none of them gives that building and shift to anyone on that date: it is free
            return null;
        }

        return await ProjectionSupervisorAsync(db, buildingId, shift, exceptUserId);
    }

    /// <summary>
    /// The Supervisor the account projection alone says holds that building and shift: the pre-backfill membership of
    /// spec §14. Only <see cref="SupervisorHolderAsync"/> (before any dated row exists) and the post-race explanation may
    /// read it.
    /// </summary>
    private static Task<User?> ProjectionSupervisorAsync(AppDbContext db, int buildingId, Shift shift, int exceptUserId) =>
        db.Users.AsNoTracking().Include(u => u.Building)
            .FirstOrDefaultAsync(u => u.Id != exceptUserId && u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == buildingId && u.Shift == shift);

    /// <summary>
    /// The dated holder as the slot message reads it: <see cref="SlotTaken"/> names the holder's Building and Shift, and the
    /// dated rows may already have replaced that projection (a Supervisor moved from A to B still carries A on the account
    /// row until the edit lands), so the building and shift the claim is for take their place.
    /// </summary>
    private static async Task<User?> HeldAsUserAsync(AppDbContext db, int holderId, int buildingId, Shift shift)
    {
        var holder = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == holderId);
        if (holder is null)
        {
            return null;
        }

        holder.Building = await db.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == buildingId);
        holder.Shift = shift;
        return holder;
    }

    public static string SlotTaken(User holder) => holder.Role == UserRole.Cleaner
        ? $"กะ{ThaiLabels.ShiftName(holder.Shift!.Value)}ของ {holder.Area!.Code} มีแม่บ้านประจำแล้ว ({holder.DisplayName})"
        : SupervisorSlotTaken(holder.Building!.Code, holder.Shift!.Value, holder.DisplayName);

    /// <summary>The Supervisor slot words, also said for a building and shift a dated period already gives another person.</summary>
    public static string SupervisorSlotTaken(string buildingCode, Shift shift, string holderName) =>
        $"กะ{ThaiLabels.ShiftName(shift)}ของตึก {buildingCode} มีหัวหน้าประจำแล้ว ({holderName})";

    /// <summary>Unique ignoring case: MySQL's collation already treats "e1001" and "E1001" as one; SQLite would not.</summary>
    public static Task<bool> EmployeeIdTakenAsync(AppDbContext db, string employeeId, int exceptUserId)
    {
        var upper = employeeId.ToUpperInvariant();
        return db.Users.AnyAsync(u => u.Id != exceptUserId && u.EmployeeId != null && u.EmployeeId.ToUpper() == upper);
    }

    public static Task<bool> UsernameTakenAsync(AppDbContext db, string username, int exceptUserId)
    {
        var upper = username.ToUpperInvariant();
        return db.Users.AnyAsync(u => u.Id != exceptUserId && u.Username != null && u.Username.ToUpper() == upper);
    }

    /// <summary>
    /// After a DbUpdateException: the 409 to give when another save took the login name or the slot first.
    /// Null means the database failed for another reason, and the caller must rethrow.
    /// </summary>
    public static async Task<IResult?> ExplainRaceAsync(AppDbContext db, AccountClaim claim, DateOnly businessDate)
    {
        db.ChangeTracker.Clear();
        if (claim.Username is { } username && await UsernameTakenAsync(db, username, claim.UserId))
        {
            return Conflict(UsernameTaken);
        }

        if (claim.EmployeeId is { } employeeId && await EmployeeIdTakenAsync(db, employeeId, claim.UserId))
        {
            return Conflict(EmployeeIdTaken);
        }

        if (await HolderAsync(db, claim.Role, claim.AreaId, claim.BuildingId, claim.Shift, claim.UserId, businessDate) is { } holder)
        {
            return Conflict(SlotTaken(holder));
        }

        // The pre-check already refused every dated holder, so a DbUpdateException reaching here is by definition the
        // projection: users.supervisor_slot. In the pre-backfill state (spec §14) an active Supervisor with no dated row
        // at all still holds their account row's building and shift, which the dated lookup cannot see once any dated row
        // exists elsewhere. Explaining that as the slot conflict keeps it a 409 instead of a 500.
        if (claim is { Role: UserRole.Cleaner, AreaId: { } cleanerAreaId, Shift: { } cleanerShift }
            && await ProjectionCleanerAsync(db, cleanerAreaId, cleanerShift, claim.UserId) is { } projectedCleaner)
        {
            return Conflict(SlotTaken(projectedCleaner));
        }

        return claim is { Role: UserRole.Supervisor, BuildingId: { } buildingId, Shift: { } shift }
            && await ProjectionSupervisorAsync(db, buildingId, shift, claim.UserId) is { } projected
                ? Conflict(SlotTaken(projected))
                : null;
    }

    public static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);

    public static IResult Conflict(string message) => ApiResults.Message(StatusCodes.Status409Conflict, message);

    public static IResult NotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบบัญชีนี้");
}
