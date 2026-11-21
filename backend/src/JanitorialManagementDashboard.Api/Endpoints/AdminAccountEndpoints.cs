using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Accounts;
using JanitorialManagementDashboard.Application.Auth;
using JanitorialManagementDashboard.Application.Buildings;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;
using static JanitorialManagementDashboard.Api.Endpoints.AccountChecks;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>
/// Wireframe D6, facility-0020/0054/0056: the Admin adds, edits, re-phones, resets and deactivates accounts one by one.
/// There is no delete. Every change is logged (facility-0051), never with a phone, a password or a hash (facility-0057).
/// </summary>
public static class AdminAccountEndpoints
{
    public static IEndpointRouteBuilder MapAdminAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/api/admin/accounts").RequireAuthorization(AuthSetup.AdminOnly);
        accounts.MapGet("", async (ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            await CurrentUser.LoadAsync(principal, db) is null ? Results.Unauthorized() : Results.Ok(await AccountView.LoadAsync(db, clock)));
        accounts.MapPost("", CreateAsync);
        accounts.MapPut("/{id:int}", UpdateAsync);
        accounts.MapPost("/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, SupervisorMembership membership, ScheduleStore store) =>
            SetActiveAsync(id, false, principal, db, clock, membership, store));
        accounts.MapPost("/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, SupervisorMembership membership, ScheduleStore store) =>
            SetActiveAsync(id, true, principal, db, clock, membership, store));
        accounts.MapPut("/{id:int}/phone", ChangePhoneAsync);
        accounts.MapPut("/{id:int}/password", ResetPasswordAsync);
        return app;
    }

    /// <summary>What the audit log keeps of an account. It has no secret on purpose: never add one.</summary>
    internal sealed record AccountSnapshot(string Role, string LoginName, string DisplayName, int? AreaId, int? BuildingId, string? Shift, bool IsActive);

    internal static AccountSnapshot Snapshot(User user) =>
        new(user.Role.ToString(), user.LoginName, user.DisplayName, user.AreaId, user.BuildingId, user.Shift?.ToString(), user.IsActive);

    private static async Task<IResult> CreateAsync(
        CreateAccountRequest request, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher hasher, TimeProvider clock,
        SupervisorMembership membership, ScheduleStore store)
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

        // Task 7: the regular/relief fields belong to a Cleaner's account; a Supervisor's building is the existing field.
        if (role != UserRole.Cleaner && (request.CleanerKind is not null || request.HomeBuildingId is not null))
        {
            return Bad("ประเภทแม่บ้านและตึกต้นทางใช้กับบัญชีแม่บ้านเท่านั้น");
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
                return Bad("บัญชีแอดมินใช้ชื่อผู้ใช้และรหัสผ่าน ไม่ใช้รหัสพนักงานหรือเบอร์โทร");
            }

            if (AccountRules.NormalizeUsername(request.Username) is not { } username)
            {
                return Bad("ชื่อผู้ใช้ต้องเป็นตัวอักษรอังกฤษ ตัวเลข . _ หรือ - ยาว 3–100 ตัว");
            }

            if (AccountRules.PasswordProblem(request.Password) is { } passwordProblem)
            {
                return Bad(passwordProblem);
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

        var (place, error) = await ReadPlaceAsync(
            db, role, userId: 0, request.AreaId, request.BuildingId, request.Shift, BusinessDate(clock),
            cleanerKind: request.CleanerKind, homeBuildingId: request.HomeBuildingId);
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
        // Spec §3: the new Supervisor's building and shift is a dated membership from the business date, written inside
        // the same transaction and only once the building/shift anchor says nobody else holds it.
        var conflict = await SaveAsync(
            db,
            claim,
            BusinessDate(clock),
            async () =>
            {
                // The audit row needs the new id, so it is a second save inside the same transaction
                AuditTrail.Add(
                    db, admin.Id, user.CreatedAt, "ACCOUNT_CREATE", "users", user.Id,
                    $"เพิ่มบัญชี{ThaiLabels.RoleName(role)} {user.LoginName} {user.DisplayName}",
                    before: null, after: Snapshot(user));
                await db.SaveChangesAsync();
                if (role == UserRole.Cleaner)
                {
                    var kind = request.CleanerKind ?? CleanerKind.Regular;
                    if (kind == CleanerKind.Relief || place.HomeBuildingId is not null)
                    {
                        await ScheduleSync.SyncCleanerAsync(
                            db, store, user, place.HomeBuildingId, kind, admin.Id,
                            BusinessDate(clock), clock.GetUtcNow().UtcDateTime);
                        await db.SaveChangesAsync();
                    }
                }
            },
            beforeSave: role == UserRole.Supervisor
                ? async () => (await membership.ApplyAsync(user, open: true, admin.Id)).Refusal
                : null);
        if (conflict is not null)
        {
            return conflict;
        }

        return Results.Created($"/api/admin/accounts/{user.Id}", await AccountView.LoadOneAsync(db, clock, user.Id));
    }

    private static async Task<IResult> UpdateAsync(
        int id, UpdateAccountRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
        SupervisorMembership membership, ScheduleStore store)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (AccountRules.NormalizeDisplayName(request.DisplayName) is not { } displayName)
        {
            return Bad($"ต้องใส่ชื่อที่แสดง ไม่เกิน {AccountRules.MaxDisplayName} ตัวอักษร");
        }

        if (user.Role != UserRole.Cleaner && (request.CleanerKind is not null || request.HomeBuildingId is not null))
        {
            return Bad("ประเภทแม่บ้านและตึกต้นทางใช้กับบัญชีแม่บ้านเท่านั้น");
        }

        // Decision 4: a deactivated Cleaner holds no Area; the Area's slot may already be someone else's
        if (!user.IsActive && user.Role == UserRole.Cleaner && request.AreaId is not null)
        {
            return Bad("เปิดใช้งานบัญชีก่อน แล้วค่อยเลือกพื้นที่");
        }

        // Task 7: an absent CleanerKind keeps the identity the person already has, so a form that predates this task changes
        // nothing about the kind. A relief being edited keeps their dated home building when the form does not resend it.
        var businessDate = BusinessDate(clock);
        var profileNow = user.Role == UserRole.Cleaner
            ? await CurrentCleanerProfileAsync(db, user.Id, businessDate)
            : null;
        var kind = request.CleanerKind ?? profileNow?.Kind ?? CleanerKind.Regular;
        var homeBuildingId = request.HomeBuildingId
            ?? (kind == CleanerKind.Relief || (request.AreaId is null && request.CleanerKind is null)
                ? profileNow?.HomeBuildingId
                : null);

        // Only an explicitly requested regular identity needs an Area: the defaulted one keeps the pre-existing behaviour
        // where a Cleaner may be left without one, and a relief is recognised whichever way its kind was decided.
        var placeKind = kind == CleanerKind.Relief ? CleanerKind.Relief : request.CleanerKind;

        var (place, error) = await ReadPlaceAsync(
            db, user.Role, user.Id, request.AreaId, request.BuildingId, request.Shift, businessDate,
            checkSlot: user.IsActive, currentAreaId: user.AreaId, cleanerKind: placeKind,
            homeBuildingId: homeBuildingId);
        if (place is null)
        {
            return error!;
        }

        var before = Snapshot(user);
        // The identity the stored records state, before this edit: the account row's own projection when the person has no
        // dated profile, so "saving the same values" still writes nothing (spec §14).
        var beforeIdentity = user.Role == UserRole.Cleaner
            ? await CurrentCleanerIdentityAsync(db, user, businessDate)
            : null;
        var identityChanged = beforeIdentity is not null
            && beforeIdentity != new CleanerIdentity(kind, place.HomeBuildingId, place.Shift);
        // The building and shift the form replaces: only that pair's membership ends, so another building the same
        // Supervisor also holds stays theirs (spec §3: several buildings per Supervisor is the normal case).
        var replaced = user.BuildingId is int beforeBuilding && user.Shift is Shift beforeShift
            ? (BuildingId: beforeBuilding, Shift: beforeShift)
            : ((int BuildingId, Shift Shift)?)null;
        user.DisplayName = displayName;
        user.AreaId = place.Area?.Id;
        user.BuildingId = place.Building?.Id;
        user.Shift = place.Shift;
        var after = Snapshot(user);
        if (after == before && !identityChanged)
        {
            return Results.Ok(await AccountView.LoadOneAsync(db, clock, user.Id));
        }

        // Only a real building/shift change is a membership write: renaming a Supervisor must not end a building
        // they also hold (a dated write ends the pair it replaces).
        var moved = (before.BuildingId, before.Shift) != (after.BuildingId, after.Shift);
        var ended = new List<EndedScope>();
        if (await SaveAsync(
                db,
                ClaimOf(user),
                businessDate,
                async () =>
                {
                    // The period rows say only the dates, so the audit line names the buildings and shifts this edit ended
                    AuditTrail.Add(
                        db, admin.Id, clock.GetUtcNow().UtcDateTime, "ACCOUNT_UPDATE", "users", user.Id,
                        await UpdateSummaryAsync(db, user, ended), before, after);
                    await db.SaveChangesAsync();
                },
                beforeSave: async () =>
                {
                    if (user.Role == UserRole.Supervisor && moved)
                    {
                        var outcome = await membership.ApplyAsync(user, open: user.IsActive, admin.Id, replaced);
                        ended.AddRange(outcome.Ended);
                        return outcome.Refusal;
                    }

                    if (user.Role == UserRole.Cleaner
                        && (identityChanged || before.AreaId != after.AreaId)
                        && (kind == CleanerKind.Relief || place.HomeBuildingId is not null || beforeIdentity is not null))
                    {
                        await ScheduleSync.SyncCleanerAsync(
                            db, store, user, place.HomeBuildingId, kind, admin.Id,
                            businessDate, clock.GetUtcNow().UtcDateTime);
                        return null;
                    }

                    return null;
                }) is { } conflict)
        {
            return conflict;
        }

        return Results.Ok(await AccountView.LoadOneAsync(db, clock, user.Id));
    }

    /// <summary>The clock's Thai business date: the date a dated membership write takes effect.</summary>
    private static DateOnly BusinessDate(TimeProvider clock) => ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime);

    /// <summary>
    /// The ACCOUNT_UPDATE line, naming the buildings and shifts the edit ended, e.g. "… ปิดความรับผิดชอบ ตึก A กะเช้า".
    /// The period row keeps only the dates, and a period that started today is removed rather than stored, so this line is
    /// the only record of which responsibility the change took away.
    /// </summary>
    private static async Task<string> UpdateSummaryAsync(AppDbContext db, User user, IReadOnlyList<EndedScope> ended)
    {
        var summary = $"แก้บัญชี{ThaiLabels.RoleName(user.Role)} {user.LoginName} {user.DisplayName}";
        if (ended.Count == 0)
        {
            return summary;
        }

        var ids = ended.Select(e => e.BuildingId).Distinct().ToList();
        var codes = await db.Buildings.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .Select(b => new { b.Id, b.Code })
            .ToDictionaryAsync(b => b.Id, b => b.Code);
        var names = ended.Select(e =>
            $"ตึก {codes.GetValueOrDefault(e.BuildingId, $"#{e.BuildingId}")} กะ{ThaiLabels.ShiftName(e.Shift)}");
        return $"{summary} ปิดความรับผิดชอบ {string.Join(" ", names)}";
    }

    /// <summary>
    /// facility-0020: no delete, only deactivate. A deactivated account cannot log in and every refresh token is revoked
    /// (facility-0014), so its devices drop within 5 minutes. History keeps its name.
    /// </summary>
    private static async Task<IResult> SetActiveAsync(
        int id, bool active, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
        SupervisorMembership membership, ScheduleStore store)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        // facility-0020: so one active Admin always remains
        if (!active && user.Id == admin.Id)
        {
            return Bad("ปิดบัญชีของตัวเองไม่ได้");
        }

        if (user.IsActive == active)
        {
            return Results.Ok(await AccountView.LoadOneAsync(db, clock, user.Id));
        }

        // facility-0090 decision 4: a Supervisor of a building that is off stays off
        if (active && user.Role == UserRole.Supervisor && user.BuildingId is int buildingId
            && await db.Buildings.Where(b => b.Id == buildingId && !b.IsActive).Select(b => b.Code).FirstOrDefaultAsync() is { } offCode)
        {
            return Conflict(BuildingRules.OffForSupervisor(offCode));
        }

        if (active && await HolderAsync(db, user.Role, user.AreaId, user.BuildingId, user.Shift, user.Id, BusinessDate(clock)) is { } holder)
        {
            return Conflict(SlotTaken(holder));
        }

        var before = Snapshot(user);
        var originalAreaId = user.AreaId;
        user.IsActive = active;
        if (!active && user.Role == UserRole.Cleaner)
        {
            // Decision 3: the Area's slot opens at once, so the Area form (D10) can offer it to someone else
            user.AreaId = null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        AuditTrail.Add(
            db, admin.Id, now, active ? "ACCOUNT_ACTIVATE" : "ACCOUNT_DEACTIVATE", "users", user.Id,
            $"{(active ? "เปิด" : "ปิด")}ใช้งานบัญชี{ThaiLabels.RoleName(user.Role)} {user.LoginName} {user.DisplayName}",
            before, Snapshot(user));
        Func<Task>? revokeSessions = active ? null : () => db.RevokeUserSessionsAsync(user.Id, now);
        // Spec §3: a deactivated account holds no scope, so its membership is closed on the business date; activating
        // opens the period again once the building/shift anchor shows the slot is free.
        var businessDate = BusinessDate(clock);
        var conflict = await SaveAsync(
            db,
            ClaimOf(user),
            businessDate,
            revokeSessions,
            beforeSave: async () =>
            {
                if (user.Role == UserRole.Supervisor)
                {
                    return (await membership.ApplyAsync(user, open: active, admin.Id)).Refusal;
                }

                if (user.Role == UserRole.Cleaner)
                {
                    var profileNow = await CurrentCleanerProfileAsync(db, user.Id, businessDate);
                    var kind = profileNow?.Kind ?? CleanerKind.Regular;
                    var homeBuildingId = profileNow?.HomeBuildingId
                        ?? (originalAreaId is int areaId
                            ? await db.Areas.AsNoTracking().Where(a => a.Id == areaId)
                                .Select(a => (int?)a.BuildingId).FirstOrDefaultAsync()
                            : null);
                    if (kind == CleanerKind.Relief || homeBuildingId is not null)
                    {
                        await ScheduleSync.SyncCleanerAsync(db, store, user, homeBuildingId, kind, admin.Id, businessDate, now);
                    }
                }

                return null;
            });
        if (conflict is not null)
        {
            return conflict;
        }

        return Results.Ok(await AccountView.LoadOneAsync(db, clock, user.Id));
    }

    /// <summary>facility-0054 rule 4: the Admin types the Cleaner's or Supervisor's new phone; every device of theirs must log in again.</summary>
    private static async Task<IResult> ChangePhoneAsync(
        int id, AccountPhoneRequest request, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher hasher, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Role == UserRole.Admin)
        {
            return Bad("บัญชีแอดมินไม่มีเบอร์โทร ใช้ปุ่มรีเซ็ตรหัสผ่าน");
        }

        var phone = PhoneNumber.Normalize(request.Phone);
        if (!PhoneNumber.IsComplete(phone))
        {
            return Bad("เบอร์โทรต้องเป็นเลขอารบิก 10 หลัก ขึ้นต้นด้วย 0");
        }

        await ReplaceSecretAsync(db, hasher, clock, admin, user, phone, "ACCOUNT_PHONE", $"แก้เบอร์โทรของ {user.LoginName} {user.DisplayName}");
        return Results.NoContent();
    }

    /// <summary>facility-0020: the Admin gives another Admin (or themselves) a new temporary password, told by voice or on paper.</summary>
    private static async Task<IResult> ResetPasswordAsync(
        int id, AccountPasswordRequest request, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher hasher, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Role != UserRole.Admin)
        {
            return Bad("แม่บ้านและหัวหน้าไม่มีรหัสผ่าน ใช้ปุ่มแก้เบอร์โทร");
        }

        if (AccountRules.PasswordProblem(request.Password) is { } passwordProblem)
        {
            return Bad(passwordProblem);
        }

        await ReplaceSecretAsync(db, hasher, clock, admin, user, request.Password!, "ACCOUNT_PASSWORD", $"รีเซ็ตรหัสผ่านของ {user.LoginName} {user.DisplayName}");
        return Results.NoContent();
    }

    /// <summary>
    /// The same secret again changes nothing and logs nothing. A new one is hashed, logged with no before or after
    /// (facility-0057), and every refresh token of the person is revoked (facility-0014).
    /// </summary>
    private static async Task ReplaceSecretAsync(
        AppDbContext db, IPasswordHasher hasher, TimeProvider clock, User admin, User user, string secret, string action, string summary)
    {
        if (hasher.Verify(secret, user.SecretHash))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        user.SecretHash = hasher.Hash(secret);
        AuditTrail.Add(db, admin.Id, now, action, "users", user.Id, summary, before: null, after: null);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        await db.RevokeUserSessionsAsync(user.Id, now);
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Saves what is tracked, then runs <paramref name="afterSave"/>, all in one transaction. Another save that took the
    /// login name or the slot first becomes a 409; any other database error is rethrown. <paramref name="beforeSave"/> is
    /// a check that runs inside the transaction before the save and can refuse it with its own answer.
    /// </summary>
    internal static async Task<IResult?> SaveAsync(
        AppDbContext db, AccountClaim claim, DateOnly businessDate, Func<Task>? afterSave = null,
        Func<Task<IResult?>>? beforeSave = null)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (beforeSave is not null && await beforeSave() is { } refused)
            {
                // Disposing the transaction rolls back whatever the check had to write (the anchor row, a closed period)
                return refused;
            }

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
            if (await ExplainRaceAsync(db, claim, businessDate) is { } conflict)
            {
                return conflict;
            }

            throw;
        }
    }
}
