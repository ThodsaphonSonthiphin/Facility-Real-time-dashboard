using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>Who a username names for reset-admin (facility-0092 decision 5).</summary>
public abstract record RecoveryTarget
{
    /// <summary>An Admin with this username: reset its password and turn it on.</summary>
    public sealed record Admin(User Account) : RecoveryTarget;

    /// <summary>Nobody has it: create a new Admin.</summary>
    public sealed record Nobody : RecoveryTarget;

    /// <summary>A Cleaner's or Supervisor's employee ID: refuse, so their login name never doubles as an Admin's.</summary>
    public sealed record Staff(UserRole Role) : RecoveryTarget;
}

/// <summary>
/// facility-0092: Admin accounts made or recovered on the server itself, with no Admin logged in, so the audit row's
/// actor is null ("ระบบ"). Callers check the username, display name and password with AccountRules first.
/// </summary>
public static class ServerAdminAccounts
{
    public const string FirstCreateAction = "ADMIN_FIRST_CREATE";
    public const string RecoverCreateAction = "ADMIN_RECOVER_CREATE";
    public const string RecoverResetAction = "ADMIN_RECOVER_RESET";

    /// <summary>A new active Admin and its audit row, saved in one transaction. The row's snapshot has no password.</summary>
    public static async Task<User> CreateAsync(
        AppDbContext db, IPasswordHasher hasher, TimeProvider clock, string username, string displayName, string password, string action, string summary)
    {
        var admin = new User
        {
            Role = UserRole.Admin,
            Username = username,
            DisplayName = displayName,
            SecretHash = hasher.Hash(password),
            IsActive = true,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Users.Add(admin);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        // The audit row needs the new id, so it is a second save inside the same transaction
        AuditTrail.Add(
            db, actorId: null, admin.CreatedAt, action, "users", admin.Id, summary,
            before: null, after: new { Role = admin.Role.ToString(), admin.LoginName, admin.DisplayName, admin.IsActive });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return admin;
    }

    /// <summary>Only Admins have a username; employee IDs are checked too. Both in any letter case, like the accounts page.</summary>
    public static async Task<RecoveryTarget> FindAsync(AppDbContext db, string username)
    {
        var upper = username.ToUpperInvariant();
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Username != null && u.Username.ToUpper() == upper);
        if (admin is not null)
        {
            return new RecoveryTarget.Admin(admin);
        }

        var staff = await db.Users.FirstOrDefaultAsync(u => u.EmployeeId != null && u.EmployeeId.ToUpper() == upper);
        return staff is null ? new RecoveryTarget.Nobody() : new RecoveryTarget.Staff(staff.Role);
    }

    /// <summary>
    /// A new password, the account turned on, and every refresh token revoked (facility-0014), in one transaction. The
    /// audit row keeps no before or after, like a password reset on the accounts page (facility-0057).
    /// </summary>
    public static async Task ResetAsync(AppDbContext db, IPasswordHasher hasher, TimeProvider clock, User admin, string password)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var turnedOn = !admin.IsActive;
        admin.SecretHash = hasher.Hash(password);
        admin.IsActive = true;
        AuditTrail.Add(
            db, actorId: null, now, RecoverResetAction, "users", admin.Id,
            $"กู้คืนแอดมิน {admin.Username} {admin.DisplayName} ด้วยคำสั่ง reset-admin บนเซิร์ฟเวอร์: ตั้งรหัสผ่านใหม่{(turnedOn ? " และเปิดใช้บัญชี" : "")}",
            before: null, after: null);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        await db.RevokeUserSessionsAsync(admin.Id, now);
        await transaction.CommitAsync();
    }
}
