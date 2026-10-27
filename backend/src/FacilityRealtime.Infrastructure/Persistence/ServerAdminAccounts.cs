using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Infrastructure.Persistence;

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
}
