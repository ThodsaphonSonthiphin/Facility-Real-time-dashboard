using System.Net;
using System.Net.Http.Json;
using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Inspections;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §3: the dated Supervisor scope is the authority for the Supervisor's building page, the inspection scan, the
/// temporary cover decision and the account membership writes. Seeded 2000001 is the Day Supervisor of building A; a
/// dated row elsewhere replaces that projection, and only an active account holds any scope at all.
/// </summary>
public class ScheduleScopeEndpointTests
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    [Fact]
    public async Task A_supervisor_reads_and_inspects_both_assigned_buildings()
    {
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, Oct1);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, Oct1);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var page = (await supervisor.GetFromJsonAsync<MyBuildingModel>("/api/my-building"))!;

        Assert.Equal(new[] { "A", "B" }, page.Buildings.Select(b => b.BuildingCode));
        Assert.Equal(new[] { false, false }, page.Buildings.Select(b => b.IsCover));
        Assert.Equal(new[] { true, true }, page.Buildings.Select(b => b.CanInspect));
        var scanned = await InspectionApi.ScannedAsync(supervisor, InspectionApi.BuildingBPoint);
        Assert.False(scanned.IsCover);
        Assert.Equal(0, await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.CountAsync()));
    }

    [Fact]
    public async Task A_dated_scope_replaces_the_current_projection()
    {
        // The account still carries BuildingId A, but the dated rows are the authority: only B is theirs
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, Oct1);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var page = (await supervisor.GetFromJsonAsync<MyBuildingModel>("/api/my-building"))!;

        Assert.Equal("B", Assert.Single(page.Buildings).BuildingCode);
        Assert.True(page.Buildings[0].CanInspect);
        Assert.Equal(HttpStatusCode.Forbidden, (await InspectionApi.ScanAsync(supervisor, InspectionApi.MenRestroom)).StatusCode);
        Assert.Equal(BlockReason.OtherBuilding, await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.Select(b => b.Reason).SingleAsync()));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.InspectionRecords.ToListAsync()));
    }

    [Fact]
    public async Task An_assigned_building_in_the_other_shift_is_a_blocked_scan_that_stores_no_inspection()
    {
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, Oct1);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));

        var response = await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var blocked = (await response.Content.ReadFromJsonAsync<BlockedModel>())!;
        Assert.Equal(nameof(BlockReason.OutsideShift), blocked.Reason);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.InspectionRecords.ToListAsync()));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.ScanRecords.ToListAsync()));
    }

    [Fact]
    public async Task An_ended_membership_grants_nothing_on_or_after_its_until_date()
    {
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, Oct1, until: Oct8);

        factory.Clock.SetUtcNow(ThaiClock.At(7, 8, 30));
        var beforeTheEnd = (await supervisor.GetFromJsonAsync<MyBuildingModel>("/api/my-building"))!;
        Assert.Equal(new DateOnly(2026, 10, 7), beforeTheEnd.ShiftDate);
        Assert.Equal("B", Assert.Single(beforeTheEnd.Buildings).BuildingCode);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var onTheEnd = (await supervisor.GetFromJsonAsync<MyBuildingModel>("/api/my-building"))!;
        Assert.Empty(onTheEnd.Buildings);
        Assert.Equal(HttpStatusCode.Forbidden, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);
    }

    [Fact]
    public async Task A_live_account_holds_no_scope_once_it_is_deactivated()
    {
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, Oct1);
        var supervisorId = await AdminSetupApi.UserIdAsync(factory, "2000001");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        await admin.PostAsync($"{AccountApi.Path}/{supervisorId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await supervisor.GetAsync("/api/my-building")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);
        // The membership was closed on the business date, so the history stays readable
        Assert.Equal(Oct8, await AdminSetupApi.ReadAsync(factory, db =>
            db.SupervisorScopePeriods.Where(p => p.SupervisorId == supervisorId).Select(p => p.EffectiveUntil!.Value).SingleAsync()));
    }

    [Fact]
    public async Task A_supervisor_cannot_forge_a_building_or_another_membership()
    {
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddBuildingAsync(factory, "C");
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var supervisorId = await AdminSetupApi.UserIdAsync(factory, "2000001");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        // The building page reads no client building: a query naming another building changes nothing
        var forgedPage = (await supervisor.GetFromJsonAsync<MyBuildingModel>("/api/my-building?buildingId=999"))!;
        Assert.Equal("A", Assert.Single(forgedPage.Buildings).BuildingCode);

        // The account and cover forms are Admin-only, so a Supervisor cannot hand itself a building
        var forgedMembership = await supervisor.PutAsJsonAsync(
            $"{AccountApi.Path}/{supervisorId}",
            new { displayName = "ปลอมสิทธิ์", buildingId = await AdminSetupApi.BuildingIdAsync(factory, "C"), shift = "Day" });
        var forgedCover = await supervisor.PostAsJsonAsync(
            "/api/admin/cover-assignments",
            new { userId = supervisorId, buildingId = await AdminSetupApi.BuildingIdAsync(factory, "B"), shiftDate = Oct8, shift = "Day" });

        Assert.Equal(HttpStatusCode.Forbidden, forgedMembership.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forgedCover.StatusCode);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.SupervisorScopePeriods.ToListAsync()));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.CoverAssignments.ToListAsync()));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.AuditLog.ToListAsync()));
        Assert.Equal("A", await AdminSetupApi.ReadAsync(factory,
            db => db.Buildings.Where(b => b.Id == db.Users.Single(u => u.Id == supervisorId).BuildingId).Select(b => b.Code).SingleAsync()));
    }


    [Fact]
    public async Task A_building_and_shift_a_dated_period_already_holds_is_a_conflict_that_writes_nothing()
    {
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var buildingB = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        // The account row still says building A, but the dated row is the authority for building B on days
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, Oct1);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AccountApi.CreateSupervisorAsync(admin, "S2002", "0000000021", buildingB, "Day");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของตึก B มีหัวหน้าประจำแล้ว (สมปอง ตรวจดี)", await AdminSetupApi.MessageAsync(response));
        Assert.False(await AdminSetupApi.ReadAsync(factory, db => db.Users.AnyAsync(u => u.EmployeeId == "S2002")));
        Assert.Equal(1, await AdminSetupApi.ReadAsync(factory, db => db.SupervisorScopePeriods.CountAsync()));
        // The anchor the refused write had to create is rolled back with it
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.ScheduleScopeAnchors.ToListAsync()));
    }

    [Fact]
    public async Task Creating_a_supervisor_records_a_dated_membership_from_the_business_date()
    {
        using var factory = new JanitorialApiFactory();
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        Assert.Equal(HttpStatusCode.Created,
            (await AccountApi.CreateSupervisorAsync(admin, "S2002", "0000000021", buildingB, "Night")).StatusCode);

        var period = await AdminSetupApi.ReadAsync(factory, db => db.SupervisorScopePeriods.AsNoTracking().SingleAsync());
        Assert.Equal((buildingB, Shift.Night, Oct8, (DateOnly?)null), (period.BuildingId, period.Shift, period.EffectiveFrom, period.EffectiveUntil));
        Assert.Equal(adminAuth.User.Id, period.CreatedById);
        Assert.Equal(1, period.Revision);
        // The building/shift anchor exists, so the next writer serialises on it
        var anchor = await AdminSetupApi.ReadAsync(factory, db => db.ScheduleScopeAnchors.AsNoTracking().SingleAsync());
        Assert.Equal((buildingB, Shift.Night), (anchor.BuildingId, anchor.Shift));
    }

    [Fact]
    public async Task Moving_a_supervisor_closes_the_old_period_and_opens_the_new_one()
    {
        using var factory = new JanitorialApiFactory();
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, Oct1);
        var supervisorId = await AdminSetupApi.UserIdAsync(factory, "2000001");
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await admin.PutAsJsonAsync(
            $"{AccountApi.Path}/{supervisorId}",
            new { displayName = "สมปอง ตรวจดี", buildingId = buildingB, shift = "Day" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { Oct8, Oct8 }, await AdminSetupApi.ReadAsync(factory, db =>
            db.SupervisorScopePeriods.AsNoTracking().OrderBy(p => p.Id).Select(p => p.EffectiveUntil ?? p.EffectiveFrom).ToListAsync()));
        Assert.Equal(new[] { Oct1, Oct8 }, await AdminSetupApi.ReadAsync(factory, db =>
            db.SupervisorScopePeriods.AsNoTracking().OrderBy(p => p.Id).Select(p => p.EffectiveFrom).ToListAsync()));
        // The write takes the anchor of the building and shift it claims, which is what a second writer must wait for
        var anchor = await AdminSetupApi.ReadAsync(factory, db => db.ScheduleScopeAnchors.AsNoTracking().SingleAsync());
        Assert.Equal((buildingB, Shift.Day), (anchor.BuildingId, anchor.Shift));

        // The changed scope is read on the very next request, in the same shift
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var page = (await supervisor.GetFromJsonAsync<MyBuildingModel>("/api/my-building"))!;
        Assert.Equal("B", Assert.Single(page.Buildings).BuildingCode);
    }

    [Fact]
    public async Task A_period_that_started_today_is_removed_rather_than_left_empty()
    {
        using var factory = new JanitorialApiFactory();
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, Oct8);
        var supervisorId = await AdminSetupApi.UserIdAsync(factory, "2000001");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await admin.PutAsJsonAsync(
            $"{AccountApi.Path}/{supervisorId}",
            new { displayName = "สมปอง ตรวจดี", buildingId = buildingB, shift = "Day" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // An empty range cannot be stored, and an inverted one would be refused: the row for the replaced day goes
        var period = await AdminSetupApi.ReadAsync(factory, db => db.SupervisorScopePeriods.AsNoTracking().SingleAsync());
        Assert.Equal((buildingB, Oct8, (DateOnly?)null), (period.BuildingId, period.EffectiveFrom, period.EffectiveUntil));
        // Nothing in the period table remembers the removed building, so the audit line names it
        var audit = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Contains("ปิดความรับผิดชอบ ตึก A กะเช้า", audit.Summary);
    }

    /// <summary>
    /// The review's Critical finding: a single-field edit replaces the building and shift the form carried and nothing
    /// else. Building B is still theirs (spec §3), so its period stays open and its scans keep working.
    /// </summary>
    [Fact]
    public async Task Editing_the_shift_of_one_building_leaves_the_other_building_held()
    {
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var buildingB = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var supervisorId = await AdminSetupApi.UserIdAsync(factory, "2000001");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, Oct1);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, Oct1);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        // The typo is on building A: A moves to nights, and B must stay exactly as it was
        var response = await admin.PutAsJsonAsync(
            $"{AccountApi.Path}/{supervisorId}",
            new { displayName = "สมปอง ตรวจดี", buildingId = buildingA, shift = "Night" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var periods = await AdminSetupApi.ReadAsync(factory, db =>
            db.SupervisorScopePeriods.AsNoTracking().OrderBy(p => p.Id).ToListAsync());
        Assert.Equal(3, periods.Count);
        Assert.Equal((buildingA, Shift.Day, Oct1, (DateOnly?)Oct8),
            (periods[0].BuildingId, periods[0].Shift, periods[0].EffectiveFrom, periods[0].EffectiveUntil));
        Assert.Equal((buildingB, Shift.Day, Oct1, (DateOnly?)null),
            (periods[1].BuildingId, periods[1].Shift, periods[1].EffectiveFrom, periods[1].EffectiveUntil));
        Assert.Equal((buildingA, Shift.Night, Oct8, (DateOnly?)null),
            (periods[2].BuildingId, periods[2].Shift, periods[2].EffectiveFrom, periods[2].EffectiveUntil));
        // The audit names only the building the edit ended
        var audit = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Contains("ปิดความรับผิดชอบ ตึก A กะเช้า", audit.Summary);
        Assert.DoesNotContain("ตึก B", audit.Summary);

        // The next scan at B in the day shift is still theirs, and it is not a Blocked Scan
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var scanned = await InspectionApi.ScannedAsync(supervisor, InspectionApi.BuildingBPoint);
        Assert.False(scanned.IsCover);
        Assert.Equal(0, await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.CountAsync()));
    }

    /// <summary>
    /// The edit side of the slot conflict: the create side is covered above, and a refused move must leave the account, the
    /// periods and the audit log exactly as they were.
    /// </summary>
    [Fact]
    public async Task Moving_a_supervisor_onto_a_slot_a_dated_period_holds_is_refused_and_changes_nothing()
    {
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var buildingB = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "B", Shift.Day);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, Oct1);
        await TestData.AddSupervisorScopeAsync(factory, "S2002", "B", Shift.Day, Oct1);
        var supervisorId = await AdminSetupApi.UserIdAsync(factory, "2000001");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await admin.PutAsJsonAsync(
            $"{AccountApi.Path}/{supervisorId}",
            new { displayName = "สมปอง ตรวจดี", buildingId = buildingB, shift = "Day" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของตึก B มีหัวหน้าประจำแล้ว (หัวหน้าทดสอบ S2002)", await AdminSetupApi.MessageAsync(response));
        Assert.Equal("A", await AdminSetupApi.ReadAsync(factory, db => db.Buildings
            .Where(b => b.Id == db.Users.Single(u => u.Id == supervisorId).BuildingId).Select(b => b.Code).SingleAsync()));
        var periods = await AdminSetupApi.ReadAsync(factory, db => db.SupervisorScopePeriods.AsNoTracking().ToListAsync());
        Assert.Equal(2, periods.Count);
        Assert.All(periods, p => Assert.Null(p.EffectiveUntil));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.AuditLog.ToListAsync()));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.ScheduleScopeAnchors.ToListAsync()));
    }

    /// <summary>
    /// The pre-check reads the dated rows, so the holder it names is the one the dates give the slot even when another
    /// account's projection also claims it. Nothing is written: no period, no anchor and no audit row.
    /// </summary>
    [Fact]
    public async Task A_dated_period_holding_a_slot_is_named_by_the_pre_check_before_anything_is_written()
    {
        using var factory = new JanitorialApiFactory();
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        // The account rows claim A/day for 2000001 and A/night for S2002, but the dates give A/day to S2002
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        await TestData.AddSupervisorScopeAsync(factory, "S2002", "A", Shift.Day, Oct1);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Night, Oct1);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AccountApi.CreateSupervisorAsync(admin, "S2003", "0820000003", buildingA, "Day");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของตึก A มีหัวหน้าประจำแล้ว (หัวหน้าทดสอบ S2002)", await AdminSetupApi.MessageAsync(response));
        Assert.False(await AdminSetupApi.ReadAsync(factory, db => db.Users.AnyAsync(u => u.EmployeeId == "S2003")));
        Assert.Equal(2, await AdminSetupApi.ReadAsync(factory, db => db.SupervisorScopePeriods.CountAsync()));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.ScheduleScopeAnchors.ToListAsync()));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.AuditLog.ToListAsync()));
    }

    /// <summary>
    /// Fix round 2: the pre-backfill state of spec §14 is mixed as soon as one dated row exists somewhere. Here 2000001
    /// holds A/day through a dated row, which switches the projection gate off, while S2002 holds B/day through its
    /// account row alone and has no dated row at all. The pre-check cannot name S2002, so the write gets as far as the
    /// <c>users.supervisor_slot</c> unique index: that refusal must read as the same slot conflict, never a server error,
    /// and must leave no account, period, anchor or audit row behind.
    /// </summary>
    [Fact]
    public async Task A_projection_only_supervisor_keeps_the_slot_conflict_a_409_after_a_dated_row_exists_elsewhere()
    {
        using var factory = new JanitorialApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var buildingB = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        // One dated row exists, and it is for a different person and slot: the global gate is off, B/day has no dated row
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, Oct1);
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "B", Shift.Day);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AccountApi.CreateSupervisorAsync(admin, "S2003", "0820000003", buildingB, "Day");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของตึก B มีหัวหน้าประจำแล้ว (หัวหน้าทดสอบ S2002)", await AdminSetupApi.MessageAsync(response));
        Assert.False(await AdminSetupApi.ReadAsync(factory, db => db.Users.AnyAsync(u => u.EmployeeId == "S2003")));
        // The dated row the write would have added, and the anchor it took to claim the slot, are rolled back with it
        Assert.Equal(1, await AdminSetupApi.ReadAsync(factory, db => db.SupervisorScopePeriods.CountAsync()));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.ScheduleScopeAnchors.ToListAsync()));
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.AuditLog.ToListAsync()));
    }

    [Fact]
    public async Task Deactivating_closes_the_membership_and_activating_opens_it_again()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, Oct1);
        var supervisorId = await AdminSetupApi.UserIdAsync(factory, "2000001");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"{AccountApi.Path}/{supervisorId}/deactivate", null)).StatusCode);
        Assert.Equal(Oct8, await AdminSetupApi.ReadAsync(factory, db =>
            db.SupervisorScopePeriods.AsNoTracking().Select(p => p.EffectiveUntil!.Value).SingleAsync()));

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"{AccountApi.Path}/{supervisorId}/activate", null)).StatusCode);
        var periods = await AdminSetupApi.ReadAsync(factory, db =>
            db.SupervisorScopePeriods.AsNoTracking().OrderBy(p => p.EffectiveFrom).ToListAsync());
        Assert.Equal(2, periods.Count);
        Assert.Equal((Oct1, (DateOnly?)Oct8), (periods[0].EffectiveFrom, periods[0].EffectiveUntil));
        Assert.Equal((Oct8, (DateOnly?)null), (periods[1].EffectiveFrom, periods[1].EffectiveUntil));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var page = (await supervisor.GetFromJsonAsync<MyBuildingModel>("/api/my-building"))!;
        Assert.True(Assert.Single(page.Buildings).CanInspect);
    }


    [Fact]
    public async Task The_live_update_reader_names_the_supervisors_the_dated_scope_gives_the_building()
    {
        using var factory = new JanitorialApiFactory();
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        await TestData.AddSupervisorAsync(factory, "S2003", "0820000003", "B", Shift.Day);
        var dayA = await AdminSetupApi.UserIdAsync(factory, "2000001");
        var nightA = await AdminSetupApi.UserIdAsync(factory, "S2002");
        var dayB = await AdminSetupApi.UserIdAsync(factory, "S2003");
        // Both A supervisors now hold the building through dated rows; the B supervisor holds only B
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, Oct1);
        await TestData.AddSupervisorScopeAsync(factory, "S2002", "A", Shift.Night, Oct1);
        await TestData.AddSupervisorScopeAsync(factory, "S2003", "B", Shift.Day, Oct1);

        var recipients = await AdminSetupApi.ReadAsync(factory, db =>
            ScheduleScopeReader.SupervisorsOfAsync(db, buildingA, Oct8));

        Assert.Equal(new[] { dayA, nightA }.OrderBy(x => x), recipients.OrderBy(x => x));
        Assert.DoesNotContain(dayB, recipients);
        Assert.Equal(new[] { dayB }, await AdminSetupApi.ReadAsync(factory,
            db => ScheduleScopeReader.SupervisorsOfAsync(db, buildingB, Oct8)));
    }

    [Fact]
    public async Task The_live_update_reader_skips_a_deactivated_supervisor()
    {
        using var factory = new JanitorialApiFactory();
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var supervisorId = await AdminSetupApi.UserIdAsync(factory, "2000001");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, Oct1);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        await admin.PostAsync($"{AccountApi.Path}/{supervisorId}/deactivate", null);

        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db =>
            ScheduleScopeReader.SupervisorsOfAsync(db, buildingA, Oct8)));
    }

    [Fact]
    public async Task The_live_update_reader_falls_back_to_the_projection_before_any_dated_row()
    {
        // Spec §14 compatibility projection: before any dated row exists the current projection is the only membership
        using var factory = new JanitorialApiFactory();
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);

        Assert.Equal(
            new[] { await AdminSetupApi.UserIdAsync(factory, "2000001") },
            await AdminSetupApi.ReadAsync(factory, db => ScheduleScopeReader.SupervisorsOfAsync(db, buildingA, Oct8)));
    }

    /// <summary>
    /// The fan-out itself, not just the reader behind it: the account rows say building A for 2000001 (day) and S2003
    /// (night), while the dated rows give A to S2002 only, so a real point change at A must reach S2002 and neither of the
    /// two the projection names.
    /// </summary>
    [Fact]
    public async Task A_point_change_reaches_the_supervisors_the_dated_scope_names()
    {
        var hub = new RecordingHub();
        using var factory = new JanitorialApiFactory
        {
            ConfigureTestServices = services => services.AddSingleton<IHubContext<ScanHub>>(hub),
        };
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "B", Shift.Day);
        await TestData.AddSupervisorAsync(factory, "S2003", "0820000003", "A", Shift.Night);
        var projectedDayA = await AdminSetupApi.UserIdAsync(factory, "2000001");
        var heldA = await AdminSetupApi.UserIdAsync(factory, "S2002");
        var projectedNightA = await AdminSetupApi.UserIdAsync(factory, "S2003");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, Oct1);
        await TestData.AddSupervisorScopeAsync(factory, "S2002", "A", Shift.Day, Oct1);
        await TestData.AddSupervisorScopeAsync(factory, "S2003", "B", Shift.Night, Oct1);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", new
        {
            qrToken = InspectionApi.MenRestroom, status = "Normal",
            latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var recipients = hub.Sends.Where(s => s.Method == "ScanRecorded").SelectMany(s => s.UserIds).ToList();
        Assert.Contains(heldA.ToString(), recipients);
        Assert.DoesNotContain(projectedDayA.ToString(), recipients);
        Assert.DoesNotContain(projectedNightA.ToString(), recipients);
    }

    /// <summary>Records who each push was sent to, so a test can assert the fan-out and not only the query behind it.</summary>
    private sealed class RecordingHub : IHubContext<ScanHub>
    {
        private readonly RecordingClients _clients = new();

        public List<(string Method, IReadOnlyList<string> UserIds)> Sends => _clients.Sends;

        public IHubClients Clients => _clients;

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class RecordingClients : IHubClients
    {
        private readonly IClientProxy _nowhere = new QuietProxy();

        public List<(string Method, IReadOnlyList<string> UserIds)> Sends { get; } = [];

        public IClientProxy All => _nowhere;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _nowhere;

        public IClientProxy Client(string connectionId) => _nowhere;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _nowhere;

        public IClientProxy Group(string groupName) => _nowhere;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _nowhere;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _nowhere;

        public IClientProxy User(string userId) => _nowhere;

        public IClientProxy Users(IReadOnlyList<string> userIds) => new RecordingProxy(this, userIds);

        private sealed class RecordingProxy(RecordingClients owner, IReadOnlyList<string> userIds) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                owner.Sends.Add((method, userIds));
                return Task.CompletedTask;
            }
        }

        private sealed class QuietProxy : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }
}

/// <summary>
/// Spec §11 and ADR facility-0140: the building/shift anchor is what stops two people claiming the same regular
/// responsibility. MySQL only, because SQLite cannot show two transactions waiting on one row lock; alone in the MySQL
/// collection, because it owns a throwaway database.
/// </summary>
[Collection("MySQL")]
public class ScheduleScopeMySqlTests
{
    /// <summary>
    /// The anchor already exists, so the serialisation point both writers take is its revision UPDATE (the row lock) and
    /// the <c>FOR UPDATE</c> read of the periods behind it. The loser must reach the overlap check and hear
    /// <c>scope-overlap</c>, never a raw duplicate-key failure.
    /// </summary>
    [MySqlFact]
    public async Task Two_writers_cannot_both_claim_one_building_and_shift()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;
        await using var setup = new AppDbContext(options);
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.MigrateAsync();
            var building = new Domain.Entities.Building { Code = "M", Name = "ตึก M", CreatedAt = DateTime.UtcNow };
            setup.Buildings.Add(building);
            setup.Users.AddRange(Supervisor("3000001"), Supervisor("3000002"));
            await setup.SaveChangesAsync();
            var buildingId = building.Id;
            var first = (await setup.Users.SingleAsync(u => u.EmployeeId == "3000001")).Id;
            var second = (await setup.Users.SingleAsync(u => u.EmployeeId == "3000002")).Id;
            var businessDate = new DateOnly(2026, 11, 17);
            var now = new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc);
            // Created before either writer: what both of them then contend for is the anchor's row lock
            setup.ScheduleScopeAnchors.Add(new ScheduleScopeAnchor
            {
                BuildingId = buildingId,
                Shift = Shift.Day,
                Revision = 1,
                UpdatedAt = now,
            });
            await setup.SaveChangesAsync();

            // Signals, never a sleep: the first writer holds the anchor until the second one is claiming it
            var firstLocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondIsClaiming = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstMayCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var firstWriter = Task.Run(async () =>
            {
                await using var db = new AppDbContext(options);
                await using var transaction = await db.Database.BeginTransactionAsync();
                var store = new ScheduleStore(db);
                await store.LockRegularScopeAsync(buildingId, Shift.Day, now);
                firstLocked.SetResult();
                await firstMayCommit.Task;
                await store.EnsureRegularScopeAvailableUnderLockAsync(buildingId, Shift.Day, businessDate, null);
                db.SupervisorScopePeriods.Add(Period(first, buildingId, businessDate));
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            });

            await firstLocked.Task;
            var secondWriter = Task.Run(async () =>
            {
                await using var db = new AppDbContext(options);
                await using var transaction = await db.Database.BeginTransactionAsync();
                var store = new ScheduleStore(db);
                secondIsClaiming.SetResult();
                // Waits here for the first writer's row lock; its period is committed by the time this returns
                await store.LockRegularScopeAsync(buildingId, Shift.Day, now);
                await store.EnsureRegularScopeAvailableUnderLockAsync(buildingId, Shift.Day, businessDate, null);
                db.SupervisorScopePeriods.Add(Period(second, buildingId, businessDate));
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            });

            await secondIsClaiming.Task;
            firstMayCommit.SetResult();
            await firstWriter;
            var refused = await Assert.ThrowsAsync<ScheduleConflictException>(() => secondWriter);
            Assert.Equal("scope-overlap", refused.Code);
            Assert.Equal(first, await setup.SupervisorScopePeriods.Select(p => p.SupervisorId).SingleAsync());
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    private static Domain.Entities.User Supervisor(string employeeId) => new()
    {
        Role = UserRole.Supervisor,
        EmployeeId = employeeId,
        DisplayName = $"หัวหน้า {employeeId}",
        SecretHash = "not-a-real-hash",
        Shift = Shift.Day,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static SupervisorScopePeriod Period(int supervisorId, int buildingId, DateOnly from) => new()
    {
        SupervisorId = supervisorId,
        BuildingId = buildingId,
        Shift = Shift.Day,
        EffectiveFrom = from,
        Revision = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
