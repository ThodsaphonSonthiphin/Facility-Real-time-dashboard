using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>Where an account works (Area or building + shift), and the checks that keep login names and slots unique.</summary>
internal static class AccountChecks
{
    public const string EmployeeIdTaken = "รหัสพนักงานนี้มีแล้ว";
    public const string UsernameTaken = "ชื่อผู้ใช้นี้มีแล้ว";

    /// <summary>The place a form asks for, checked against the role. Area and Building are tracked entities or null.</summary>
    public sealed record AccountPlace(Area? Area, Building? Building, Shift? Shift);

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
    /// <paramref name="checkSlot"/> is false for a deactivated account, which holds no slot; activation checks it later.
    /// <paramref name="currentAreaId"/> is the Cleaner's Area now: keeping it is allowed even after the Area was deactivated
    /// (Area deactivation does not unassign Cleaners), but moving to an inactive Area is not.
    /// </summary>
    public static async Task<(AccountPlace? Place, IResult? Error)> ReadPlaceAsync(
        AppDbContext db, UserRole role, int userId, int? areaId, int? buildingId, Shift? shift, bool checkSlot = true,
        int? currentAreaId = null)
    {
        if (shift is { } given && !Enum.IsDefined(given))
        {
            return (null, Bad("กะต้องเป็น Day หรือ Night"));
        }

        switch (role)
        {
            case UserRole.Admin:
                return areaId is null && buildingId is null && shift is null
                    ? (new AccountPlace(null, null, null), null)
                    : (null, Bad("บัญชี Admin ไม่มี Area ตึก หรือกะ"));

            case UserRole.Cleaner:
            {
                if (buildingId is not null)
                {
                    return (null, Bad("แม่บ้านประจำ Area ไม่ได้ประจำตึก"));
                }

                if (shift is not { } cleanerShift)
                {
                    return (null, Bad("ต้องเลือกกะของแม่บ้าน"));
                }

                if (areaId is null)
                {
                    return (new AccountPlace(null, null, cleanerShift), null);
                }

                var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == areaId && (a.IsActive || a.Id == currentAreaId));
                if (area is null)
                {
                    return (null, Bad("ต้องเลือก Area ที่ใช้งานอยู่"));
                }

                // Not HasShift: it is false for an inactive Area, and a kept inactive Area must still pass when the shift fits
                if (cleanerShift == Shift.Night && area.ShiftPattern != ShiftPattern.DayAndNight)
                {
                    return (null, Bad($"{area.Code} ทำเฉพาะกะเช้า เลือกกะดึกไม่ได้"));
                }

                return checkSlot && await HolderAsync(db, role, area.Id, null, cleanerShift, userId) is { } holder
                    ? (null, Conflict(SlotTaken(holder)))
                    : (new AccountPlace(area, null, cleanerShift), null);
            }

            default:
            {
                if (areaId is not null)
                {
                    return (null, Bad("หัวหน้าประจำตึก ไม่ได้ประจำ Area"));
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

                return checkSlot && await HolderAsync(db, role, null, building.Id, supervisorShift, userId) is { } holder
                    ? (null, Conflict(SlotTaken(holder)))
                    : (new AccountPlace(null, building, supervisorShift), null);
            }
        }
    }

    /// <summary>The other active account in that Area-shift (Cleaner) or building-shift (Supervisor) slot, with its Area or Building loaded.</summary>
    public static Task<User?> HolderAsync(AppDbContext db, UserRole role, int? areaId, int? buildingId, Shift? shift, int exceptUserId) =>
        role switch
        {
            UserRole.Cleaner when areaId is not null && shift is not null => db.Users.AsNoTracking().Include(u => u.Area)
                .FirstOrDefaultAsync(u => u.Id != exceptUserId && u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == areaId && u.Shift == shift),
            UserRole.Supervisor when buildingId is not null && shift is not null => db.Users.AsNoTracking().Include(u => u.Building)
                .FirstOrDefaultAsync(u => u.Id != exceptUserId && u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == buildingId && u.Shift == shift),
            _ => Task.FromResult<User?>(null),
        };

    public static string SlotTaken(User holder) => holder.Role == UserRole.Cleaner
        ? $"กะ{ThaiLabels.ShiftName(holder.Shift!.Value)}ของ {holder.Area!.Code} มีแม่บ้านประจำแล้ว ({holder.DisplayName})"
        : $"กะ{ThaiLabels.ShiftName(holder.Shift!.Value)}ของตึก {holder.Building!.Code} มีหัวหน้าประจำแล้ว ({holder.DisplayName})";

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
    public static async Task<IResult?> ExplainRaceAsync(AppDbContext db, AccountClaim claim)
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

        return await HolderAsync(db, claim.Role, claim.AreaId, claim.BuildingId, claim.Shift, claim.UserId) is { } holder
            ? Conflict(SlotTaken(holder))
            : null;
    }

    public static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);

    public static IResult Conflict(string message) => ApiResults.Message(StatusCodes.Status409Conflict, message);

    public static IResult NotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบบัญชีนี้");
}
