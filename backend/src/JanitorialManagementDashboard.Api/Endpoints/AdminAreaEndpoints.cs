using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Areas;
using JanitorialManagementDashboard.Application.Buildings;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Signs;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>facility-0045: the Admin sets up Areas, their shift pattern and their Cleaners. Every change is logged (facility-0051).</summary>
public static class AdminAreaEndpoints
{
    public static IEndpointRouteBuilder MapAdminAreaEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/buildings", ListBuildingsAsync);
        admin.MapGet("/cleaners", ListCleanersAsync);
        admin.MapGet("/areas", async (ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            await CurrentUser.LoadAsync(principal, db) is null
                ? Results.Unauthorized()
                : Results.Ok(await AdminSetupView.LoadAreasAsync(db, businessDate: ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime))));
        admin.MapGet("/areas/{id:int}", async (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
        {
            if (await CurrentUser.LoadAsync(principal, db) is null)
            {
                return Results.Unauthorized();
            }

            var businessDate = ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime);
            return await AdminSetupView.LoadAreaAsync(db, id, businessDate) is { } area ? Results.Ok(area) : AreaNotFound();
        });
        admin.MapPost("/areas", CreateAsync);
        admin.MapPut("/areas/{id:int}", UpdateAsync);
        admin.MapPost("/areas/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
            OverviewMutation mutation, OverviewNotifications overview, ScheduleStore store, CancellationToken ct) =>
            SetActiveAsync(id, false, principal, db, clock, mutation, overview, store, ct));
        admin.MapPost("/areas/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
            OverviewMutation mutation, OverviewNotifications overview, ScheduleStore store, CancellationToken ct) =>
            SetActiveAsync(id, true, principal, db, clock, mutation, overview, store, ct));
        return app;
    }

    private static async Task<IResult> ListBuildingsAsync(ClaimsPrincipal principal, AppDbContext db) =>
        await CurrentUser.LoadAsync(principal, db) is null
            ? Results.Unauthorized()
            : Results.Ok(await db.Buildings.AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.Code)
                .Select(b => new BuildingDto(b.Id, b.Code, b.Name))
                .ToListAsync());

    /// <summary>Every active Regular Cleaner with their effective shift and current Area; Relief Cleaners are excluded.</summary>
    private static async Task<IResult> ListCleanersAsync(ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var businessDate = ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime);
        var cleaners = await db.Users.AsNoTracking()
            .Include(u => u.Area)
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive)
            .OrderBy(u => u.EmployeeId)
            .ToListAsync(ct);
        var effectiveByUser = await CleanerEffectiveAssignment.ResolveForDateAsync(
            db, cleaners, businessDate, useFallbackWeekdayArea: true, ct: ct);
        var areaIds = effectiveByUser.Values
            .Where(s => s.AreaId != null)
            .Select(s => s.AreaId!.Value)
            .Distinct()
            .ToList();
        var areaCodes = areaIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Areas.AsNoTracking()
                .Where(a => areaIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Code, ct);

        var options = cleaners
            .Where(u => effectiveByUser.GetValueOrDefault(u.Id)?.Kind != CleanerKind.Relief)
            .Select(u =>
            {
                var state = effectiveByUser.GetValueOrDefault(u.Id);
                var areaId = state?.AreaId;
                var areaCode = areaId is int aId ? areaCodes.GetValueOrDefault(aId) ?? u.Area?.Code : null;
                return new CleanerOptionDto(
                    u.Id, u.EmployeeId ?? string.Empty, u.DisplayName, state?.Shift, areaId, areaCode);
            })
            .ToList();
        return Results.Ok(options);
    }

    private sealed record AreaForm(string Name, Building Building, ShiftPattern Pattern, User? DayCleaner, User? NightCleaner);

    /// <summary>What the audit log keeps of an Area: no names of people, only ids.</summary>
    private sealed record AreaSnapshot(
        string Code, string Name, int BuildingId, string ShiftPattern, int? DayCleanerId, int? NightCleanerId, int? RemovedNightWindows = null);

    private static async Task<IResult> CreateAsync(CreateAreaRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
        OverviewMutation mutation, OverviewNotifications overview, ScheduleStore store, ScheduleNotifications notifications,
        CancellationToken ct)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (SignCodes.NormalizeAreaCode(request.Code) is not { } code)
        {
            return Bad("รหัสพื้นที่ต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 2–20 ตัว เช่น AR03");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var businessDate = ScheduleAuthority.BusinessDate(now);
        await using var change = await mutation.BeginAsync(ct);
        var (form, error) = await ReadFormAsync(
            db, areaId: null, businessDate, request.Name, request.BuildingId, request.ShiftPattern, request.DayCleanerId, request.NightCleanerId, ct);
        if (form is null)
        {
            return error!;
        }

        if (await db.Areas.AnyAsync(a => a.Code == code, ct))
        {
            return Conflict("รหัสพื้นที่นี้มีแล้ว");
        }

        var area = new Area { BuildingId = form.Building.Id, Code = code, Name = form.Name, ShiftPattern = form.Pattern, CreatedAt = now };
        // facility-0045: the Area's one check-in sign comes with it
        db.Signs.Add(new Sign { Area = area, Code = SignCodes.CheckIn(code), QrToken = SignCodes.NewQrToken(), QrIssuedAt = now });

        IReadOnlyList<int> affectedCleaners;
        try
        {
            await db.SaveChangesAsync(ct);
            var apply = await AreaCleaners.ApplyAsync(
                db, store, area.Id, area.BuildingId, form.DayCleaner, form.NightCleaner, admin.Id, businessDate, now, ct);
            if (apply.Conflict is { } overlap)
            {
                return Conflict($"กะ{ThaiLabels.ShiftName(overlap.Shift)}ของ {overlap.AreaCode} มีแม่บ้านประจำแล้ว ({overlap.HolderName})");
            }

            affectedCleaners = apply.AffectedCleaners;
            AuditTrail.Add(
                db, admin.Id, now, "AREA_CREATE", "areas", area.Id, $"เพิ่มพื้นที่ {code} {form.Name}",
                before: null, after: Snapshot(area, form.DayCleaner, form.NightCleaner));
            await db.SaveChangesAsync(ct);
            await overview.CommitAsync(change, ct);
        }
        catch (DbUpdateException)
        {
            return Conflict("มีการบันทึกซ้อนกัน ลองใหม่อีกครั้ง");
        }

        if (affectedCleaners.Count > 0)
        {
            var scopes = new[]
            {
                form.DayCleaner is not null ? new ScheduleScopeKey(area.BuildingId, Shift.Day) : null,
                form.NightCleaner is not null ? new ScheduleScopeKey(area.BuildingId, Shift.Night) : null,
            }.OfType<ScheduleScopeKey>().ToList();
            await notifications.ChangedAsync(Guid.NewGuid(), new ScheduleChangeScope([], scopes, affectedCleaners), ct);
        }

        return Results.Created($"/api/admin/areas/{area.Id}", await AdminSetupView.LoadAreaAsync(db, area.Id, businessDate));
    }

    private static async Task<IResult> UpdateAsync(int id, UpdateAreaRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
        OverviewMutation mutation, OverviewNotifications overview, ScheduleStore store, ScheduleNotifications notifications,
        CancellationToken ct)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var businessDate = ScheduleAuthority.BusinessDate(now);
        await using var change = await mutation.BeginAsync(ct);
        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (area is null)
        {
            return AreaNotFound();
        }

        var (form, error) = await ReadFormAsync(
            db, area.Id, businessDate, request.Name, request.BuildingId, request.ShiftPattern, request.DayCleanerId, request.NightCleanerId, ct);
        if (form is null)
        {
            return error!;
        }

        var allActiveCleaners = await db.Users.Where(u => u.Role == UserRole.Cleaner && u.IsActive).ToListAsync(ct);
        var effectiveByUser = await CleanerEffectiveAssignment.ResolveForDateAsync(
            db, allActiveCleaners, businessDate, useFallbackWeekdayArea: true, ct: ct);
        var current = allActiveCleaners
            .Where(u => effectiveByUser.TryGetValue(u.Id, out var state) && state.AreaId == area.Id)
            .ToList();
        var currentIds = current.Select(u => u.Id).ToHashSet();

        // facility-0091 decision 2: an Area that is off takes no new Cleaner; one who already holds it may stay, and a shift may be emptied
        if (!area.IsActive
            && new[] { form.DayCleaner, form.NightCleaner }.Any(c => c is not null && !currentIds.Contains(c.Id)))
        {
            return Bad(AreaRules.OffAreaTakesNoNewCleaner);
        }

        var before = Snapshot(
            area,
            current.FirstOrDefault(u => effectiveByUser[u.Id].Shift == Shift.Day),
            current.FirstOrDefault(u => effectiveByUser[u.Id].Shift == Shift.Night));
        var beforeBuildingId = area.BuildingId;
        var switchingToDayOnly = area.ShiftPattern == ShiftPattern.DayAndNight && form.Pattern == ShiftPattern.DayOnly;
        var cleanersChanged = before.DayCleanerId != form.DayCleaner?.Id
            || before.NightCleanerId != form.NightCleaner?.Id
            || beforeBuildingId != form.Building.Id
            || before.ShiftPattern != form.Pattern.ToString();

        // Guard switching DayAndNight -> DayOnly against live/future Night-shift schedules on this Area.
        if (switchingToDayOnly)
        {
            await store.LockRegularScopeAsync(area.BuildingId, Shift.Night, now, ct);
            var outgoingNightIds = current
                .Where(u => effectiveByUser[u.Id].Shift == Shift.Night)
                .Select(u => u.Id)
                .ToHashSet();
            if (await HasActiveOrFutureScheduleAsync(db, area.Id, businessDate, Shift.Night, outgoingNightIds, ct))
            {
                return Conflict($"พื้นที่ {area.Code} ยังมีตารางงาน/ทำแทน/ย้ายพื้นที่กะดึกตั้งแต่วันนี้เป็นต้นไป ยกเลิกรายการเหล่านั้นก่อนเปลี่ยนเป็นเฉพาะกะเช้า");
            }
        }

        area.Name = form.Name;
        area.BuildingId = form.Building.Id;
        area.ShiftPattern = form.Pattern;
        IReadOnlyList<int> affectedCleaners = [];
        try
        {
            List<PointRoundWindow> nightWindows = form.Pattern == ShiftPattern.DayOnly
                ? await db.PointRoundWindows.Where(w => w.Shift == Shift.Night && w.ServicePoint!.AreaId == area.Id).ToListAsync(ct)
                : [];
            // Wireframe D10: a day-only Area has no night rounds
            await RoundWindowRemoval.RemoveAsync(db, nightWindows);
            if (cleanersChanged)
            {
                var apply = await AreaCleaners.ApplyAsync(
                    db, store, area.Id, area.BuildingId, form.DayCleaner, form.NightCleaner, admin.Id, businessDate, now, ct);
                if (apply.Conflict is { } overlap)
                {
                    return Conflict($"กะ{ThaiLabels.ShiftName(overlap.Shift)}ของ {overlap.AreaCode} มีแม่บ้านประจำแล้ว ({overlap.HolderName})");
                }

                affectedCleaners = apply.AffectedCleaners;
            }

            var after = Snapshot(area, form.DayCleaner, form.NightCleaner);
            if (after != before || nightWindows.Count > 0)
            {
                AuditTrail.Add(
                    db, admin.Id, now, "AREA_UPDATE", "areas", area.Id, $"แก้พื้นที่ {area.Code} {area.Name}",
                    before, after with { RemovedNightWindows = nightWindows.Count });
                await db.SaveChangesAsync(ct);
            }

            await overview.CommitAsync(change, ct);
        }
        catch (DbUpdateException)
        {
            return Conflict("มีการบันทึกซ้อนกัน ลองใหม่อีกครั้ง");
        }

        if (affectedCleaners.Count > 0)
        {
            var scopes = new[] { beforeBuildingId, area.BuildingId }
                .Distinct()
                .SelectMany(b => new[] { new ScheduleScopeKey(b, Shift.Day), new ScheduleScopeKey(b, Shift.Night) })
                .ToList();
            await notifications.ChangedAsync(Guid.NewGuid(), new ScheduleChangeScope([], scopes, affectedCleaners), ct);
        }

        return Results.Ok(await AdminSetupView.LoadAreaAsync(db, area.Id, businessDate));
    }

    /// <summary>Wireframe D10: a deactivated Area and its signs leave the Dashboard and cannot be scanned; history stays.</summary>
    private static async Task<IResult> SetActiveAsync(int id, bool active, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock,
        OverviewMutation mutation, OverviewNotifications overview, ScheduleStore store, CancellationToken ct)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var businessDate = ScheduleAuthority.BusinessDate(now);
        await using var change = await mutation.BeginAsync(ct);
        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (area is null)
        {
            return AreaNotFound();
        }

        // facility-0090 decision 4: an Area of a building that is off stays off
        if (active && !area.IsActive
            && await db.Buildings.Where(b => b.Id == area.BuildingId && !b.IsActive).Select(b => b.Code).FirstOrDefaultAsync(ct) is { } offCode)
        {
            return Conflict(BuildingRules.OffForArea(offCode));
        }

        // facility-0091 decision 2: an Area is turned off only once no active Cleaner or live/future schedule holds it.
        // Lock both shift scope anchors so concurrent schedule writes cannot assign this Area while deactivating.
        if (!active && area.IsActive)
        {
            await store.LockRegularScopeAsync(area.BuildingId, Shift.Day, now, ct);
            await store.LockRegularScopeAsync(area.BuildingId, Shift.Night, now, ct);
            var holders = await RegularBaselineOverlap.ActiveAreaHoldersAsync(db, area.Id, businessDate, ct);
            if (AreaRules.DeactivateProblem(area.Code, holders) is { } problem)
            {
                return Conflict(problem);
            }

            if (await HasActiveOrFutureScheduleAsync(db, area.Id, businessDate, shiftFilter: null, excludedBaselineUsers: null, ct))
            {
                return Conflict($"พื้นที่ {area.Code} ยังมีตารางงาน/ทำแทน/ย้ายพื้นที่ที่ใช้งานอยู่ตั้งแต่วันนี้เป็นต้นไป ยกเลิกรายการเหล่านั้นก่อนปิดใช้งาน");
            }
        }

        if (area.IsActive != active)
        {
            area.IsActive = active;
            AuditTrail.Add(
                db, admin.Id, now, active ? "AREA_ACTIVATE" : "AREA_DEACTIVATE", "areas", area.Id,
                $"{(active ? "เปิด" : "ปิด")}ใช้งานพื้นที่ {area.Code}",
                before: new { IsActive = !active }, after: new { IsActive = active });
            await db.SaveChangesAsync(ct);
        }

        await overview.CommitAsync(change, ct);
        return Results.Ok(await AdminSetupView.LoadAreaAsync(db, area.Id, businessDate));
    }

    private static async Task<bool> HasActiveOrFutureScheduleAsync(
        AppDbContext db,
        int areaId,
        DateOnly businessDate,
        Shift? shiftFilter,
        HashSet<int>? excludedBaselineUsers,
        CancellationToken ct)
    {
        if (await db.ReliefAssignments.AsNoTracking().AnyAsync(r =>
            !r.IsCancelled && r.AreaId == areaId && r.ShiftDate >= businessDate
            && (shiftFilter == null || r.Shift == shiftFilter.Value), ct))
        {
            return true;
        }

        if (await db.ScheduleExceptions.AsNoTracking().AnyAsync(e =>
            !e.IsCancelled && e.Kind == ScheduleExceptionKind.Area && e.AreaId == areaId && e.ShiftDate >= businessDate
            && (shiftFilter == null || e.Shift == shiftFilter.Value), ct))
        {
            return true;
        }

        if (await db.CoverAssignments.AsNoTracking().AnyAsync(c =>
            c.CancelledAt == null && c.AreaId == areaId && c.ShiftDate >= businessDate
            && (shiftFilter == null || c.Shift == shiftFilter.Value), ct))
        {
            return true;
        }

        if (await db.AreaTransfers.AsNoTracking().AnyAsync(t =>
            !t.IsCancelled && (t.FromAreaId == areaId || t.ToAreaId == areaId) && t.ShiftDate >= businessDate
            && (shiftFilter == null || t.Shift == shiftFilter.Value), ct))
        {
            return true;
        }

        if (shiftFilter is Shift targetShift)
        {
            var futureBaselineUsers = await db.ScheduleBaselines.AsNoTracking()
                .Where(b => b.Kind == BaselineKind.Regular
                    && (b.EffectiveUntil == null || b.EffectiveUntil.Value > businessDate)
                    && b.Days.Any(d => !d.IsDayOff && d.AreaId == areaId))
                .Select(b => new { b.UserId, b.EffectiveFrom, b.EffectiveUntil })
                .ToListAsync(ct);
            if (futureBaselineUsers.Count > 0)
            {
                var candidateIds = futureBaselineUsers.Select(x => x.UserId).Distinct().ToList();
                var activeUsers = await db.Users.AsNoTracking()
                    .Where(u => candidateIds.Contains(u.Id) && u.IsActive && u.Role == UserRole.Cleaner)
                    .ToDictionaryAsync(u => u.Id, ct);
                var periods = (await db.CleanerProfilePeriods.AsNoTracking()
                    .Where(p => candidateIds.Contains(p.UserId))
                    .ToListAsync(ct))
                    .GroupBy(p => p.UserId)
                    .ToDictionary(g => g.Key, g => g.ToList());
                foreach (var b in futureBaselineUsers)
                {
                    if (!activeUsers.TryGetValue(b.UserId, out var u))
                    {
                        continue;
                    }
                    if (excludedBaselineUsers is not null && excludedBaselineUsers.Contains(b.UserId) && b.EffectiveFrom <= businessDate)
                    {
                        continue;
                    }

                    var start = b.EffectiveFrom > businessDate ? b.EffectiveFrom : businessDate;
                    var end = b.EffectiveUntil ?? DateOnly.MaxValue;
                    var matchesShift = periods.TryGetValue(b.UserId, out var userPeriods) && userPeriods.Count > 0
                        ? userPeriods.Any(p => p.Kind == CleanerKind.Regular && p.Shift == targetShift
                            && p.EffectiveFrom < end && (p.EffectiveUntil == null || p.EffectiveUntil.Value > start))
                        : u.Shift == targetShift;
                    if (matchesShift)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>The fields Create and Update share; returns the form, or the answer to give instead.</summary>
    private static async Task<(AreaForm? Form, IResult? Error)> ReadFormAsync(
        AppDbContext db, int? areaId, DateOnly businessDate, string? name, int buildingId, ShiftPattern? pattern,
        int? dayCleanerId, int? nightCleanerId, CancellationToken ct)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > 150)
        {
            return (null, Bad("ต้องใส่ชื่อพื้นที่ ไม่เกิน 150 ตัวอักษร"));
        }

        if (pattern is not { } shiftPattern || !Enum.IsDefined(shiftPattern))
        {
            return (null, Bad("ต้องเลือกรูปแบบกะ"));
        }

        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId, ct);
        if (building is null)
        {
            return (null, Bad("ไม่พบตึกนี้"));
        }

        if (!building.IsActive)
        {
            return (null, Bad("ต้องเลือกตึกที่ใช้งานอยู่"));
        }

        if (shiftPattern == ShiftPattern.DayOnly && nightCleanerId is not null)
        {
            return (null, Bad("พื้นที่ที่ทำเฉพาะกะเช้าไม่มีแม่บ้านกะดึก"));
        }

        User? day = null;
        User? night = null;
        foreach (var (shift, cleanerId) in new[] { (Shift.Day, dayCleanerId), (Shift.Night, nightCleanerId) })
        {
            if (cleanerId is null)
            {
                continue;
            }

            var cleaner = await db.Users.Include(u => u.Area)
                .FirstOrDefaultAsync(u => u.Id == cleanerId && u.Role == UserRole.Cleaner && u.IsActive, ct);
            if (cleaner is null)
            {
                return (null, Bad("ต้องเลือกแม่บ้านที่ใช้งานอยู่"));
            }

            var effective = await CleanerEffectiveAssignment.ResolveOneAsync(
                db, cleaner, businessDate, useFallbackWeekdayArea: true, ct: ct);

            if (effective.Kind == CleanerKind.Relief)
            {
                return (null, Bad("สแปร์ไม่มีพื้นที่ประจำ กำหนดในหน้าพื้นที่ไม่ได้"));
            }

            if (effective.Shift != shift)
            {
                return (null, Bad($"{cleaner.DisplayName} ประจำกะ{ShiftName(effective.Shift)} ใส่ในช่องกะ{ShiftName(shift)}ไม่ได้"));
            }

            // Wireframe D10: the picker offers only Cleaners without an Area in that shift
            if (effective.AreaId is { } otherAreaId && otherAreaId != areaId)
            {
                var otherAreaCode = await db.Areas.AsNoTracking()
                    .Where(a => a.Id == otherAreaId)
                    .Select(a => a.Code)
                    .FirstOrDefaultAsync(ct) ?? cleaner.Area?.Code ?? $"#{otherAreaId}";
                return (null, Conflict($"{cleaner.DisplayName} ประจำ {otherAreaCode} อยู่แล้ว ย้ายออกจากพื้นที่นั้นก่อน"));
            }

            if (shift == Shift.Day)
            {
                day = cleaner;
            }
            else
            {
                night = cleaner;
            }
        }

        return (new AreaForm(trimmed, building, shiftPattern, day, night), null);
    }

    private static AreaSnapshot Snapshot(Area area, User? day, User? night) =>
        new(area.Code, area.Name, area.BuildingId, area.ShiftPattern.ToString(), day?.Id, night?.Id);

    private static string ShiftName(Shift? shift) => shift switch
    {
        Shift.Day => "เช้า",
        Shift.Night => "ดึก",
        _ => "ที่ไม่ระบุ",
    };

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);

    private static IResult Conflict(string message) => ApiResults.Message(StatusCodes.Status409Conflict, message);

    private static IResult AreaNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบพื้นที่นี้");
}
