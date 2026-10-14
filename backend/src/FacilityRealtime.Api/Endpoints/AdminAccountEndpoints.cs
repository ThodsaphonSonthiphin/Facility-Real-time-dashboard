using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static FacilityRealtime.Api.Endpoints.AccountChecks;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// Wireframe D6, facility-0020/0054/0056: the Admin adds, edits, re-phones, resets and deactivates accounts one by one.
/// There is no delete. Every change is logged (facility-0051), never with a phone, a password or a hash (facility-0057).
/// </summary>
public static class AdminAccountEndpoints
{
    public static IEndpointRouteBuilder MapAdminAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/api/admin/accounts").RequireAuthorization(AuthSetup.AdminOnly);
        accounts.MapGet("", async (ClaimsPrincipal principal, AppDbContext db) =>
            await CurrentUser.LoadAsync(principal, db) is null ? Results.Unauthorized() : Results.Ok(await AccountView.LoadAsync(db)));
        accounts.MapPost("", CreateAsync);
        return app;
    }

    /// <summary>What the audit log keeps of an account. It has no secret on purpose: never add one.</summary>
    internal sealed record AccountSnapshot(string Role, string LoginName, string DisplayName, int? AreaId, int? BuildingId, string? Shift, bool IsActive);

    internal static AccountSnapshot Snapshot(User user) =>
        new(user.Role.ToString(), user.LoginName, user.DisplayName, user.AreaId, user.BuildingId, user.Shift?.ToString(), user.IsActive);

    private static async Task<IResult> CreateAsync(
        CreateAccountRequest request, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher hasher, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.Role is not { } role || !Enum.IsDefined(role))
        {
            return Bad("ต้องเลือกบทบาท");
        }

        if (AccountRules.NormalizeDisplayName(request.DisplayName) is not { } displayName)
        {
            return Bad($"ต้องใส่ชื่อที่แสดง ไม่เกิน {AccountRules.MaxDisplayName} ตัวอักษร");
        }

        var user = new User { Role = role, DisplayName = displayName, CreatedAt = clock.GetUtcNow().UtcDateTime };
        string secret;
        if (role == UserRole.Admin)
        {
            if (request.EmployeeId is not null || request.Phone is not null)
            {
                return Bad("บัญชี Admin ใช้ชื่อผู้ใช้และรหัสผ่าน ไม่ใช้รหัสพนักงานหรือเบอร์โทร");
            }

            if (AccountRules.NormalizeUsername(request.Username) is not { } username)
            {
                return Bad("ชื่อผู้ใช้ต้องเป็นตัวอักษรอังกฤษ ตัวเลข . _ หรือ - ยาว 3–100 ตัว");
            }

            if (!AccountRules.IsAllowedPassword(request.Password))
            {
                return Bad($"รหัสผ่านชั่วคราวต้องยาว {AccountRules.MinPassword}–{AccountRules.MaxPassword} ตัวอักษร");
            }

            if (await UsernameTakenAsync(db, username, exceptUserId: 0))
            {
                return Conflict(UsernameTaken);
            }

            user.Username = username;
            secret = request.Password!;
        }
        else
        {
            if (request.Username is not null || request.Password is not null)
            {
                return Bad("แม่บ้านและหัวหน้าใช้รหัสพนักงานกับเบอร์โทร ไม่ใช้ชื่อผู้ใช้หรือรหัสผ่าน");
            }

            if (AccountRules.NormalizeEmployeeId(request.EmployeeId) is not { } employeeId)
            {
                return Bad("รหัสพนักงานต้องเป็นตัวอักษรอังกฤษ ตัวเลข หรือ - ไม่เกิน 20 ตัว");
            }

            var phone = PhoneNumber.Normalize(request.Phone);
            if (!PhoneNumber.IsComplete(phone))
            {
                return Bad("เบอร์โทรต้องเป็นเลขอารบิก 10 หลัก ขึ้นต้นด้วย 0");
            }

            if (await EmployeeIdTakenAsync(db, employeeId, exceptUserId: 0))
            {
                return Conflict(EmployeeIdTaken);
            }

            user.EmployeeId = employeeId;
            secret = phone;
        }

        var (place, error) = await ReadPlaceAsync(db, role, userId: 0, request.AreaId, request.BuildingId, request.Shift);
        if (place is null)
        {
            return error!;
        }

        user.AreaId = place.Area?.Id;
        user.BuildingId = place.Building?.Id;
        user.Shift = place.Shift;
        // facility-0054: the phone is stored only as a PBKDF2 hash, like a password
        user.SecretHash = hasher.Hash(secret);
        db.Users.Add(user);

        var claim = ClaimOf(user);
        var conflict = await SaveAsync(db, claim, async () =>
        {
            // The audit row needs the new id, so it is a second save inside the same transaction
            AuditTrail.Add(
                db, admin.Id, user.CreatedAt, "ACCOUNT_CREATE", "users", user.Id,
                $"เพิ่มบัญชี{ThaiLabels.RoleName(role)} {user.LoginName} {user.DisplayName}",
                before: null, after: Snapshot(user));
            await db.SaveChangesAsync();
        });
        if (conflict is not null)
        {
            return conflict;
        }

        return Results.Created($"/api/admin/accounts/{user.Id}", await AccountView.LoadOneAsync(db, user.Id));
    }

    /// <summary>
    /// Saves what is tracked, then runs <paramref name="afterSave"/>, all in one transaction. Another save that took the
    /// login name or the slot first becomes a 409; any other database error is rethrown.
    /// </summary>
    internal static async Task<IResult?> SaveAsync(AppDbContext db, AccountClaim claim, Func<Task>? afterSave = null)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.SaveChangesAsync();
            if (afterSave is not null)
            {
                await afterSave();
            }

            await transaction.CommitAsync();
            return null;
        }
        catch (DbUpdateException)
        {
            if (await ExplainRaceAsync(db, claim) is { } conflict)
            {
                return conflict;
            }

            throw;
        }
    }
}
