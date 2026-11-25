using System.Data;
using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Hubs;
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
        accounts.MapPost("/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
            SupervisorMembership membership, ScheduleStore store, ScheduleNotifications notifications,
            ScanHubConnections hubConnections, CancellationToken ct) =>
            SetActiveAsync(id, false, principal, db, clock, membership, store, notifications, hubConnections, ct));
        accounts.MapPost("/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
            SupervisorMembership membership, ScheduleStore store, ScheduleNotifications notifications,
            ScanHubConnections hubConnections, CancellationToken ct) =>
            SetActiveAsync(id, true, principal, db, clock, membership, store, notifications, hubConnections, ct));
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
        SupervisorMembership membership, ScheduleStore store, ScheduleNotifications notifications, CancellationToken ct)
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

        var now = clock.GetUtcNow().UtcDateTime;
        var businessDate = ScheduleAuthority.BusinessDate(now);
        var (place, error) = await ReadPlaceAsync(
            db, role, userId: 0, request.AreaId, request.BuildingId, request.Shift, businessDate,
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
        if (role == UserRole.Admin)
        {
            db.Users.Add(user);
        }

        var claim = ClaimOf(user);
        var cleanerKind = request.CleanerKind ?? CleanerKind.Regular;
        // Spec §3: the new Supervisor's building and shift is a dated membership from the business date, written inside
        // the same transaction and only once the building/shift anchor says nobody else holds it.
        var conflict = await SaveAsync(
            db,
            claim,
            businessDate,
            async () =>
            {
                // The audit row needs the new id, so it is a second save inside the same transaction
                AuditTrail.Add(
                    db, admin.Id, user.CreatedAt, "ACCOUNT_CREATE", "users", user.Id,
                    $"เพิ่มบัญชี{ThaiLabels.RoleName(role)} {user.LoginName} {user.DisplayName}",
                    before: null, after: Snapshot(user));
                await db.SaveChangesAsync(ct);
                if (role == UserRole.Cleaner && (cleanerKind == CleanerKind.Relief || place.HomeBuildingId is not null))
                {
                    await ScheduleSync.SyncCleanerAsync(
                        db, store, user, place.HomeBuildingId, cleanerKind, admin.Id,
                        businessDate, now, ct);
                    await db.SaveChangesAsync(ct);
                }
            },
            beforeSave: async () =>
            {
                if (role == UserRole.Supervisor)
                {
                    // Add the Supervisor only after scope locking and overlap checks succeed so anchor creation never flushes user early.
                    return (await membership.ApplyAsync(
                        user,
                        open: true,
                        admin.Id,
                        persistUserAsync: async () =>
                        {
                            db.Users.Add(user);
                            await db.SaveChangesAsync(ct);
                        },
                        ct: ct)).Refusal;
                }

                if (role == UserRole.Cleaner)
                {
                    if (place.Area is not null && place.Shift is Shift cleanerShift)
                    {
                        await store.LockRegularScopeAsync(place.Area.BuildingId, cleanerShift, now, ct);
                        var overlap = await RegularBaselineOverlap.FindSlotConflictAsync(
                            db, excludeUserId: 0, place.Area.Id, cleanerShift, businessDate, ct: ct);
                        if (overlap is not null)
                        {
                            return Conflict($"กะ{ThaiLabels.ShiftName(overlap.Shift)}ของ {overlap.AreaCode} มีแม่บ้านประจำแล้ว ({overlap.HolderName})");
                        }

                        await ClearStaleCleanerProjectionAsync(db, place.Area.Id, cleanerShift, exceptUserId: 0, ct);
                    }

                    // Add the new Cleaner only after scope locking and overlap checks finish so anchor creation never flushes user early.
                    db.Users.Add(user);
                }

                return null;
            });
        if (conflict is not null)
        {
            return conflict;
        }

        if (role == UserRole.Cleaner && (cleanerKind == CleanerKind.Relief || place.HomeBuildingId is not null))
        {
            await NotifyCleanerScopesAsync(notifications, [place.HomeBuildingId], [place.Shift], user.Id, ct);
        }

        return Results.Created($"/api/admin/accounts/{user.Id}", await AccountView.LoadOneAsync(db, clock, user.Id));
    }

    private static async Task<IResult> UpdateAsync(
        int id, UpdateAccountRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
        SupervisorMembership membership, ScheduleStore store, ScheduleNotifications notifications, CancellationToken ct)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
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

        var now = clock.GetUtcNow().UtcDateTime;
        var businessDate = ScheduleAuthority.BusinessDate(now);
        var beforeEffective = user.Role == UserRole.Cleaner
            ? await CleanerEffectiveAssignment.ResolveOneAsync(db, user, businessDate, useFallbackWeekdayArea: true, ct: ct)
            : null;

        // Task 7: an absent CleanerKind keeps the identity the person already has, so a form that predates this task changes
        // nothing about the kind. A relief being edited keeps their dated home building when the form does not resend it.
        var kind = request.CleanerKind ?? beforeEffective?.Kind ?? CleanerKind.Regular;
        var homeBuildingId = request.HomeBuildingId
            ?? (kind == CleanerKind.Relief || (request.AreaId is null && request.CleanerKind is null)
                ? beforeEffective?.HomeBuildingId
                : null);

        // Only an explicitly requested regular identity needs an Area: the defaulted one keeps the pre-existing behaviour
        // where a Cleaner may be left without one, and a relief is recognised whichever way its kind was decided.
        var placeKind = kind == CleanerKind.Relief ? CleanerKind.Relief : request.CleanerKind;

        var (place, error) = await ReadPlaceAsync(
            db, user.Role, user.Id, request.AreaId, request.BuildingId, request.Shift, businessDate,
            checkSlot: user.IsActive, currentAreaId: beforeEffective?.AreaId ?? user.AreaId, cleanerKind: placeKind,
            homeBuildingId: homeBuildingId);
        if (place is null)
        {
            return error!;
        }

        var before = Snapshot(user);
        var beforeKind = beforeEffective?.Kind ?? CleanerKind.Regular;
        var scheduleChanged = user.Role == UserRole.Cleaner && beforeEffective is not null
            && (beforeKind != kind
                || beforeEffective.HomeBuildingId != place.HomeBuildingId
                || beforeEffective.Shift != place.Shift
                || beforeEffective.AreaId != place.Area?.Id);

        // The building and shift the form replaces: only that pair's membership ends, so another building the same
        // Supervisor also holds stays theirs (spec §3: several buildings per Supervisor is the normal case).
        var replaced = user.BuildingId is int beforeBuilding && user.Shift is Shift beforeShift
            ? (BuildingId: beforeBuilding, Shift: beforeShift)
            : ((int BuildingId, Shift Shift)?)null;

        var deferCleanerMutation = user.Role == UserRole.Cleaner && scheduleChanged;
        var deferSupervisorMutation = user.Role == UserRole.Supervisor
            && (before.BuildingId, before.Shift) != (place.Building?.Id, place.Shift?.ToString());
        if (!deferCleanerMutation && !deferSupervisorMutation)
        {
            user.DisplayName = displayName;
            if (user.Role != UserRole.Cleaner)
            {
                user.AreaId = place.Area?.Id;
                user.BuildingId = place.Building?.Id;
                user.Shift = place.Shift;
            }
        }

        var after = (deferCleanerMutation || deferSupervisorMutation)
            ? new AccountSnapshot(user.Role.ToString(), user.LoginName, displayName, place.Area?.Id, place.Building?.Id, place.Shift?.ToString(), user.IsActive)
            : Snapshot(user);
        if (after == before && !scheduleChanged)
        {
            return Results.Ok(await AccountView.LoadOneAsync(db, clock, user.Id));
        }

        var claim = (deferCleanerMutation || deferSupervisorMutation)
            ? new AccountClaim(user.Id, user.Role, user.Username, user.EmployeeId, user.IsActive ? place.Area?.Id : null, user.IsActive ? place.Building?.Id : null, place.Shift)
            : ClaimOf(user);

        // Only a real building/shift change is a membership write: renaming a Supervisor must not end a building
        // they also hold (a dated write ends the pair it replaces).
        var moved = (before.BuildingId, before.Shift) != (after.BuildingId, after.Shift);
        var ended = new List<EndedScope>();
        var syncedCleaner = false;
        if (await SaveAsync(
                db,
                claim,
                businessDate,
                async () =>
                {
                    // The period rows say only the dates, so the audit line names the buildings and shifts this edit ended
                    AuditTrail.Add(
                        db, admin.Id, now, "ACCOUNT_UPDATE", "users", user.Id,
                        await UpdateSummaryAsync(db, user, ended), before, after);
                    await db.SaveChangesAsync(ct);
                },
                beforeSave: async () =>
                {
                    if (user.Role == UserRole.Supervisor && moved)
                    {
                        var outcome = await membership.ApplyAsync(
                            user,
                            open: user.IsActive,
                            admin.Id,
                            replaced,
                            targetOverride: (place.Building?.Id, place.Shift),
                            persistUserAsync: () =>
                            {
                                user.DisplayName = displayName;
                                user.AreaId = place.Area?.Id;
                                user.BuildingId = place.Building?.Id;
                                user.Shift = place.Shift;
                                return Task.CompletedTask;
                            },
                            ct: ct);
                        ended.AddRange(outcome.Ended);
                        return outcome.Refusal;
                    }

                    if (deferCleanerMutation)
                    {
                        var shouldSync = kind == CleanerKind.Relief
                            || place.HomeBuildingId is not null
                            || beforeEffective?.HasDatedIdentity == true;
                        if (shouldSync)
                        {
                            // Global lock order: baseline head before scope anchors (matches ScheduleCommandService & AreaCleaners).
                            await store.LockBaselineHeadAsync(user.Id, now, ct, advanceRevision: false);
                            var scopeLocks = new List<(int BuildingId, Shift Shift)>();
                            if (beforeEffective?.HomeBuildingId is int oldB && beforeEffective.Shift is Shift oldS)
                            {
                                scopeLocks.Add((oldB, oldS));
                            }
                            if (place.Area is not null && place.Shift is Shift newS)
                            {
                                scopeLocks.Add((place.Area.BuildingId, newS));
                            }
                            foreach (var scope in scopeLocks.Distinct().OrderBy(s => s.BuildingId).ThenBy(s => s.Shift))
                            {
                                await store.LockRegularScopeAsync(scope.BuildingId, scope.Shift, now, ct);
                            }

                            if (user.IsActive && place.Area is not null && place.Shift is Shift targetShift)
                            {
                                var overlap = await RegularBaselineOverlap.FindSlotConflictAsync(
                                    db, user.Id, place.Area.Id, targetShift, businessDate, ct: ct);
                                if (overlap is not null)
                                {
                                    return Conflict($"กะ{ThaiLabels.ShiftName(overlap.Shift)}ของ {overlap.AreaCode} มีแม่บ้านประจำแล้ว ({overlap.HolderName})");
                                }

                                await ClearStaleCleanerProjectionAsync(db, place.Area.Id, targetShift, user.Id, ct);
                            }
                        }

                        user.DisplayName = displayName;
                        user.AreaId = place.Area?.Id;
                        user.BuildingId = place.Building?.Id;
                        user.Shift = place.Shift;

                        if (shouldSync)
                        {
                            await ScheduleSync.SyncCleanerAsync(
                                db, store, user, place.HomeBuildingId, kind, admin.Id,
                                businessDate, now, ct);
                            syncedCleaner = true;
                        }

                        return null;
                    }

                    return null;
                }) is { } conflict)
        {
            return conflict;
        }

        if (syncedCleaner)
        {
            await NotifyCleanerScopesAsync(
                notifications,
                [beforeEffective?.HomeBuildingId, place.HomeBuildingId],
                [beforeEffective?.Shift, place.Shift],
                user.Id,
                ct);
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
        SupervisorMembership membership, ScheduleStore store, ScheduleNotifications notifications,
        ScanHubConnections hubConnections, CancellationToken ct)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
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
            && await db.Buildings.Where(b => b.Id == buildingId && !b.IsActive).Select(b => b.Code).FirstOrDefaultAsync(ct) is { } offCode)
        {
            return Conflict(BuildingRules.OffForSupervisor(offCode));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var businessDate = ScheduleAuthority.BusinessDate(now);
        if (active && await HolderAsync(db, user.Role, user.AreaId, user.BuildingId, user.Shift, user.Id, businessDate) is { } holder)
        {
            return Conflict(SlotTaken(holder));
        }

        var before = Snapshot(user);
        var originalAreaId = user.AreaId;
        var beforeEffective = user.Role == UserRole.Cleaner
            ? await CleanerEffectiveAssignment.ResolveOneAsync(db, user, businessDate, useFallbackWeekdayArea: true, ct: ct)
            : null;
        var targetAreaId = !active && user.Role == UserRole.Cleaner ? null : user.AreaId;
        var expectedAfter = before with { IsActive = active, AreaId = targetAreaId };
        var claim = new AccountClaim(
            user.Id,
            user.Role,
            user.Username,
            user.EmployeeId,
            active ? targetAreaId : null,
            active ? user.BuildingId : null,
            user.Shift);

        // Spec §3: a deactivated account holds no scope, so its membership is closed on the business date; activating
        // opens the period again once the building/shift anchor shows the slot is free.
        int? notifyHomeBuildingId = beforeEffective?.HomeBuildingId;
        var syncedCleaner = false;
        var conflict = await SaveAsync(
            db,
            claim,
            businessDate,
            afterSave: async () =>
            {
                AuditTrail.Add(
                    db, admin.Id, now, active ? "ACCOUNT_ACTIVATE" : "ACCOUNT_DEACTIVATE", "users", user.Id,
                    $"{(active ? "เปิด" : "ปิด")}ใช้งานบัญชี{ThaiLabels.RoleName(user.Role)} {user.LoginName} {user.DisplayName}",
                    before, expectedAfter);
                await db.SaveChangesAsync(ct);
                if (!active)
                {
                    await db.RevokeUserSessionsAsync(user.Id, now);
                }
            },
            beforeSave: async () =>
            {
                if (!active && user.Role == UserRole.Admin)
                {
                    // facility-0020: serialize concurrent Admin deactivations so the last two Admins cannot
                    // mutually deactivate each other and leave zero active Admins.
                    var isSqlite = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
                    var activeAdminIds = isSqlite
                        ? await db.Users.AsNoTracking()
                            .Where(u => u.Role == UserRole.Admin && u.IsActive)
                            .Select(u => u.Id)
                            .ToListAsync(ct)
                        : await db.Database
                            .SqlQueryRaw<int>("SELECT id AS Value FROM users WHERE role = 'ADMIN' AND is_active = 1 ORDER BY id FOR UPDATE")
                            .ToListAsync(ct);
                    if (!activeAdminIds.Contains(admin.Id))
                    {
                        return Results.Unauthorized();
                    }

                    if (activeAdminIds.Count(aId => aId != user.Id) == 0)
                    {
                        return Conflict("ต้องเหลือบัญชีแอดมินที่ใช้งานอยู่อย่างน้อย 1 บัญชี");
                    }

                    user.IsActive = active;
                    return null;
                }

                if (user.Role == UserRole.Supervisor)
                {
                    return (await membership.ApplyAsync(
                        user,
                        open: active,
                        admin.Id,
                        persistUserAsync: () =>
                        {
                            user.IsActive = active;
                            return Task.CompletedTask;
                        },
                        ct: ct)).Refusal;
                }

                if (user.Role == UserRole.Cleaner)
                {
                    var profileNow = await CurrentCleanerProfileAsync(db, user.Id, businessDate, ct);
                    var kind = profileNow?.Kind ?? CleanerKind.Regular;
                    var homeBuildingId = profileNow?.HomeBuildingId
                        ?? (originalAreaId is int areaId
                            ? await db.Areas.AsNoTracking().Where(a => a.Id == areaId)
                                .Select(a => (int?)a.BuildingId).FirstOrDefaultAsync(ct)
                            : null);
                    notifyHomeBuildingId ??= homeBuildingId;
                    if (kind == CleanerKind.Relief || homeBuildingId is not null || !active)
                    {
                        // Lock baseline head before mutating User so anchor creation cannot flush User prematurely.
                        await store.LockBaselineHeadAsync(user.Id, now, ct, advanceRevision: false);
                        user.IsActive = active;
                        if (!active)
                        {
                            // Decision 3: the Area's slot opens at once, so the Area form (D10) can offer it to someone else
                            user.AreaId = null;
                        }

                        await ScheduleSync.SyncCleanerAsync(
                            db, store, user, homeBuildingId, kind, admin.Id, businessDate, now, ct,
                            preserveExistingShift: true);
                        syncedCleaner = true;
                    }
                    else
                    {
                        user.IsActive = active;
                    }

                    return null;
                }

                user.IsActive = active;
                return null;
            });
        if (conflict is not null)
        {
            return conflict;
        }

        if (!active)
        {
            hubConnections.DisconnectUser(user.Id);
        }

        if (syncedCleaner)
        {
            await NotifyCleanerScopesAsync(notifications, [notifyHomeBuildingId], [beforeEffective?.Shift, user.Shift], user.Id, ct);
        }

        return Results.Ok(await AccountView.LoadOneAsync(db, clock, user.Id));
    }

    /// <summary>
    /// Clears stale <c>users.AreaId</c> rows on other active Cleaners once <see cref="RegularBaselineOverlap"/> under lock
    /// has confirmed no dated baseline overlaps the target slot, avoiding false <c>users.cleaner_slot</c> collisions.
    /// </summary>
    private static async Task ClearStaleCleanerProjectionAsync(
        AppDbContext db, int areaId, Shift shift, int exceptUserId, CancellationToken ct)
    {
        var stale = await db.Users
            .Where(u => u.Id != exceptUserId && u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == areaId && u.Shift == shift)
            .ToListAsync(ct);
        if (stale.Count == 0)
        {
            return;
        }

        foreach (var holder in stale)
        {
            holder.AreaId = null;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task NotifyCleanerScopesAsync(
        ScheduleNotifications notifications,
        IEnumerable<int?> buildingIds,
        IEnumerable<Shift?> shifts,
        int cleanerId,
        CancellationToken ct)
    {
        var buildings = buildingIds.OfType<int>().Distinct().ToList();
        var shiftList = shifts.OfType<Shift>().Distinct().ToList();
        if (shiftList.Count == 0)
        {
            shiftList.Add(Shift.Day);
            shiftList.Add(Shift.Night);
        }

        var scopes = buildings
            .SelectMany(b => shiftList.Select(s => new ScheduleScopeKey(b, s)))
            .Distinct()
            .ToList();
        await notifications.ChangedAsync(Guid.NewGuid(), new ScheduleChangeScope([], scopes, [cleanerId]), ct);
    }

    /// <summary>facility-0054 rule 4: the Admin types the Cleaner's or Supervisor's new phone; every device of theirs must log in again.</summary>
    private static async Task<IResult> ChangePhoneAsync(
        int id, AccountPhoneRequest request, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher hasher,
        TimeProvider clock, ScanHubConnections hubConnections)
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

        await ReplaceSecretAsync(db, hasher, clock, hubConnections, admin, user, phone, "ACCOUNT_PHONE", $"แก้เบอร์โทรของ {user.LoginName} {user.DisplayName}");
        return Results.NoContent();
    }

    /// <summary>facility-0020: the Admin gives another Admin (or themselves) a new temporary password, told by voice or on paper.</summary>
    private static async Task<IResult> ResetPasswordAsync(
        int id, AccountPasswordRequest request, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher hasher,
        TimeProvider clock, ScanHubConnections hubConnections)
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

        await ReplaceSecretAsync(db, hasher, clock, hubConnections, admin, user, request.Password!, "ACCOUNT_PASSWORD", $"รีเซ็ตรหัสผ่านของ {user.LoginName} {user.DisplayName}");
        return Results.NoContent();
    }

    /// <summary>
    /// The same secret again changes nothing and logs nothing. A new one is hashed, logged with no before or after
    /// (facility-0057), and every refresh token of the person is revoked (facility-0014).
    /// </summary>
    private static async Task ReplaceSecretAsync(
        AppDbContext db, IPasswordHasher hasher, TimeProvider clock, ScanHubConnections hubConnections,
        User admin, User user, string secret, string action, string summary)
    {
        if (hasher.Verify(secret, user.SecretHash))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        user.SecretHash = hasher.Hash(secret);
        AuditTrail.Add(db, admin.Id, now, action, "users", user.Id, summary, before: null, after: null);
        var isSqlite = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        await using var transaction = await db.Database.BeginTransactionAsync(
            isSqlite ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted);
        await db.SaveChangesAsync();
        await db.RevokeUserSessionsAsync(user.Id, now);
        await transaction.CommitAsync();
        hubConnections.DisconnectUser(user.Id);
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
            var isSqlite = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
            await using var transaction = await db.Database.BeginTransactionAsync(
                isSqlite ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted);
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
