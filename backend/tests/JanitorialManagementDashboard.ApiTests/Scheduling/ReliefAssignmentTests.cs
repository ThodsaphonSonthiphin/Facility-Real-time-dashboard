using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §5, §6 and acceptance A08 with A01: a relief Cleaner is planned into a destination Area on one date, inheriting the
/// replaced person's first-break group and overriding it for the day, or choosing one explicitly when the work is
/// standalone. Nothing is guessed for a person nobody replaces, a row is refused as a whole when anything about it is
/// wrong, and the scoped week read exposes the rows a screen needs without showing another team's schedule.
/// </summary>
public class ReliefAssignmentTests
{
    [Fact]
    public async Task A_relief_cleaner_in_the_same_shift_with_no_regular_area_is_selectable()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");

        var response = await admin.GetAsync(
            $"/api/schedules/relief-candidates?employeeId=E3001&sourceBuildingId={sourceBuilding}"
            + $"&date={ReliefApi.Iso(ReliefApi.Wednesday)}&shift=Day");
        var candidate = await ReliefApi.ReadAsync<ReliefApi.CandidateAnswer>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(candidate);
        Assert.Equal(relief, candidate!.UserId);
        Assert.Equal("E3001", candidate.EmployeeId);
        Assert.Equal(sourceBuilding, candidate.HomeBuildingId);
        Assert.Equal("B", candidate.HomeBuildingCode);
        Assert.Equal("Day", candidate.Shift);
        Assert.True(candidate.Eligible);
        Assert.Equal(ReliefApi.Eligible, candidate.Status);
        Assert.Empty(candidate.Conflicts);

        // Minimal identity only: never another team's schedule, never a phone or a hash.
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("phone", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("days", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_candidate_from_another_home_building_or_not_relief_is_not_returned()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");

        // The relief belongs to building B. Asked for as a source of building A, the route answers nothing at all rather
        // than describing where they do belong (spec §6: only the identity and home building that are needed).
        var wrongBuilding = await admin.GetAsync(
            $"/api/schedules/relief-candidates?employeeId=E3001&sourceBuildingId={buildingA}"
            + $"&date={ReliefApi.Iso(ReliefApi.Wednesday)}&shift=Day");

        // 1000001 is an active regular Cleaner of Area AR01 in building A: they hold a regular Area, so they are not a relief.
        var regular = await admin.GetAsync(
            $"/api/schedules/relief-candidates?employeeId=1000001&sourceBuildingId={buildingA}"
            + $"&date={ReliefApi.Iso(ReliefApi.Wednesday)}&shift=Day");

        // An employee id nobody has is the same answer as a person who is not a relief of that building.
        var unknown = await admin.GetAsync(
            $"/api/schedules/relief-candidates?employeeId=E9999&sourceBuildingId={buildingA}"
            + $"&date={ReliefApi.Iso(ReliefApi.Wednesday)}&shift=Day");

        Assert.Equal(HttpStatusCode.NotFound, wrongBuilding.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, regular.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var body = await wrongBuilding.Content.ReadAsStringAsync();
        // One answer for every "not a relief of that building" reason: it never says which of them it was.
        Assert.Contains("ตึกต้นทาง", body);
        Assert.DoesNotContain("AR01", body);
        Assert.DoesNotContain("1000001", body);
    }

    [Fact]
    public async Task The_candidate_route_is_for_schedule_managers_and_wants_every_parameter()
    {
        using var factory = ReliefApi.NewFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");

        var forbidden = await cleaner.GetAsync(
            $"/api/schedules/relief-candidates?employeeId=1000001&sourceBuildingId={buildingA}"
            + $"&date={ReliefApi.Iso(ReliefApi.Wednesday)}&shift=Day");
        var missingDate = await admin.GetAsync(
            $"/api/schedules/relief-candidates?employeeId=1000001&sourceBuildingId={buildingA}&shift=Day");
        var unknownShift = await admin.GetAsync(
            $"/api/schedules/relief-candidates?employeeId=1000001&sourceBuildingId={buildingA}"
            + $"&date={ReliefApi.Iso(ReliefApi.Wednesday)}&shift=Evening");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingDate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownShift.StatusCode);
    }

    [Theory]
    [InlineData(ScheduleStore.ReliefShiftMismatch)]
    [InlineData(ScheduleStore.ReliefExistingAssignment)]
    [InlineData(ScheduleStore.ReliefExistingDuty)]
    [InlineData(ScheduleStore.ReliefOnLeave)]
    public async Task The_candidate_route_names_the_stored_fact_that_blocks_the_slot(string conflict)
    {
        // Spec §6: a screen must not offer a person the command then refuses, so each stored fact that blocks the slot is
        // one refusal the lookup itself answers, with the code the command refuses the same fact under.
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var relief = conflict == ScheduleStore.ReliefShiftMismatch
            // The person belongs to B but works nights, so the day slot asked for is somebody else's.
            ? await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Night)
            : await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        switch (conflict)
        {
            case ScheduleStore.ReliefExistingAssignment:
                await TestData.AddAreaAsync(factory, "AR03", "B", ShiftPattern.DayOnly);
                await ReliefApi.AddStoredReliefAsync(factory, relief, await AdminSetupApi.AreaIdAsync(factory, "AR03"),
                    ReliefApi.Wednesday, Shift.Day, sourceBuilding);
                break;
            case ScheduleStore.ReliefExistingDuty:
                await TestData.AddAreaAsync(factory, "AR03", "B");
                // Their own applicable plan already works an Area in that slot: nothing may be moved or cancelled.
                await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "E3001", new DateOnly(2026, 10, 1), "AR03");
                break;
            case ScheduleStore.ReliefOnLeave:
                await ReliefApi.AddLeaveAsync(factory, relief, ReliefApi.Wednesday, Shift.Day);
                break;
        }

        var response = await admin.GetAsync(
            $"/api/schedules/relief-candidates?employeeId=E3001&sourceBuildingId={sourceBuilding}"
            + $"&date={ReliefApi.Iso(ReliefApi.Wednesday)}&shift=Day");
        var candidate = await ReliefApi.ReadAsync<ReliefApi.CandidateAnswer>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(candidate);
        Assert.False(candidate!.Eligible);
        Assert.Equal(ReliefApi.Conflict, candidate.Status);
        Assert.Equal(conflict, Assert.Single(candidate.Conflicts));
    }

    [Fact]
    public async Task Standalone_relief_work_without_a_break_group_is_refused_and_writes_nothing()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (client, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var answer = await ReliefApi.AssignAsync(client,
            ReliefApi.Assignment(relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: null));

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal(ReliefApi.Rejected, answer.Answer!.Outcome);
        Assert.Equal("row-invalid", Assert.Single(answer.Answer.Problems).Code);
        Assert.Contains("กลุ่มพัก", Assert.Single(answer.Answer.Problems).Message);
        var reliefKey = ReliefApi.ReliefKey(relief, ReliefApi.Wednesday, "Day");
        Assert.Equal("0", answer.Answer.Revisions[reliefKey]);
        await ReliefApi.AssertNoReliefAsync(factory);
    }

    [Fact]
    public async Task A_standalone_assignment_records_the_destination_actor_attestation_and_one_audit_row()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        // One Supervisor holds the source building too, so the loan gate asks for nothing and this test is about the row.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (client, auth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var answer = await ReliefApi.AssignAsync(client,
            ReliefApi.Assignment(relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 2));

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        Assert.Equal(ReliefApi.Committed, answer.Answer!.Outcome);
        Assert.Equal("1", answer.Answer.Revisions[ReliefApi.ReliefKey(relief, ReliefApi.Wednesday, "Day")]);
        var stored = await ReliefApi.ReadSingleReliefAsync(factory);
        Assert.Equal((relief, destination, "Day", 2, sourceBuilding), (
            stored.UserId, stored.AreaId, stored.Shift.ToString(), stored.BreakGroup, stored.SourceBuildingId));
        Assert.Null(stored.ReplacedUserId);
        Assert.Null(stored.SourceSupervisorId);
        Assert.False(stored.AgreementReported);
        Assert.False(stored.IsCancelled);
        // The row names the person who recorded it; the recorded agreement is that actor's own note, never a signature.
        Assert.Equal(auth.User.Id, stored.CreatedById);
        var change = await ReliefApi.ReadSingleChangeAsync(factory);
        Assert.Equal(("ReliefAssignment", "create"), (change.EntityType, change.Operation));
        Assert.Contains("\"revision\":\"1\"", change.AfterJson);
    }

    [Fact]
    public async Task The_replaced_persons_dated_break_group_is_inherited_over_their_pattern()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        // 1000001 works AR01 in building A with break group 1 in their repeating week, and group 2 for this one day.
        await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001", breakGroup: 1);
        await ScheduleWriteApi.AddDatedBreakAsync(factory, "1000001", ReliefApi.Wednesday, 2);
        var replaced = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (client, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var answer = await ReliefApi.AssignAsync(client, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, replacedUserId: replaced));

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        Assert.Equal(ReliefApi.Committed, answer.Answer!.Outcome);
        var stored = await ReliefApi.ReadSingleReliefAsync(factory);
        Assert.Equal(2, stored.BreakGroup);
        Assert.Equal(replaced, stored.ReplacedUserId);
        // The replaced person's own plan is not rewritten by the relief row (spec §6: no broad source edit).
        var replacedDated = await AdminSetupApi.ReadAsync(factory, db => db.ScheduleExceptions
            .Where(row => row.UserId == replaced).ToListAsync());
        Assert.Equal(2, Assert.Single(replacedDated).BreakGroup);
    }

    [Fact]
    public async Task A_replaced_person_whose_plan_is_another_area_is_refused()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        // 1000003 works AR02, so replacing them in AR01 describes no duty anyone holds.
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000003", new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000003", new DateOnly(2026, 10, 1), "AR02");
        var replaced = await AdminSetupApi.UserIdAsync(factory, "1000003");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (client, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var answer = await ReliefApi.AssignAsync(client, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, replacedUserId: replaced));

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal("row-invalid", Assert.Single(answer.Answer!.Problems).Code);
        Assert.Contains("ไม่ใช่พื้นที่", Assert.Single(answer.Answer!.Problems).Message);
        await ReliefApi.AssertNoReliefAsync(factory);
    }

    [Fact]
    public async Task A_relief_assignment_outside_the_actors_buildings_is_refused()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "AR03", "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "A", Shift.Day);
        var destinationB = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "A");
        var (client, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var answer = await ReliefApi.AssignAsync(client, ReliefApi.Assignment(
            relief, destinationB, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal("row-invalid", Assert.Single(answer.Answer!.Problems).Code);
        await ReliefApi.AssertNoReliefAsync(factory);
    }

    [Fact]
    public async Task Only_a_dated_relief_profile_in_the_payload_shift_can_be_assigned()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Night);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (client, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        // A relief of the night shift is not a day-shift candidate, and a regular Cleaner is not one either.
        var wrongShift = await ReliefApi.AssignAsync(client, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));
        var regular = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var notRelief = await ReliefApi.AssignAsync(client, ReliefApi.Assignment(
            regular, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));

        Assert.Equal(HttpStatusCode.Conflict, wrongShift.Response.StatusCode);
        Assert.Contains("กะ", Assert.Single(wrongShift.Answer!.Problems).Message);
        Assert.Equal(HttpStatusCode.Conflict, notRelief.Response.StatusCode);
        Assert.Contains("สแปร์", Assert.Single(notRelief.Answer!.Problems).Message);
        await ReliefApi.AssertNoReliefAsync(factory);
    }

    [Fact]
    public async Task A_stale_relief_revision_writes_nothing_and_answers_the_current_one()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        // One Supervisor holds the source building too, so the loan gate asks for nothing and the first write can commit.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (client, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var payload = ReliefApi.Assignment(relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1);
        await ReliefApi.AssignAsync(client, payload);

        var stale = await ReliefApi.AssignAsync(client, payload, new Dictionary<string, string>
        {
            [ReliefApi.ReliefKey(relief, ReliefApi.Wednesday, "Day")] = "0",
        });

        Assert.Equal(HttpStatusCode.Conflict, stale.Response.StatusCode);
        var problem = Assert.Single(stale.Answer!.Problems);
        Assert.Equal("stale-revision", problem.Code);
        Assert.Equal("1", stale.Answer.Revisions[ReliefApi.ReliefKey(relief, ReliefApi.Wednesday, "Day")]);
        await factory.WithDbAsync(async db => Assert.Single(await db.ReliefAssignments.ToListAsync()));
    }

    [Fact]
    public async Task An_unreadable_relief_payload_is_a_row_problem_not_a_server_error()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");

        // A key that is present but unreadable is a refusal, never "absent": a typo may not become a different assignment.
        var answer = await ReliefApi.AssignAsync(admin, new
        {
            userId = 1,
            areaId = 1,
            shiftDate = ReliefApi.Iso(ReliefApi.Wednesday),
            shift = "Day",
            sourceBuildingId = buildingA,
            breakGroup = "first",
        });

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal("payload", Assert.Single(answer.Answer!.Problems).RowId);
        await ReliefApi.AssertNoReliefAsync(factory);
    }

    [Fact]
    public async Task The_week_read_shows_relief_rows_scoped_to_the_destination_building()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddBuildingAsync(factory, "C");
        // One Supervisor holds A and B, so the loan needs no second actor; C is another team entirely.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var buildingB = await AdminSetupApi.BuildingIdAsync(factory, "B");
        await ReliefApi.AddSupervisorAsync(factory, admin, "2000002", "C", Shift.Day);
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = buildingB;
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var (other, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000002", ReliefApi.Phone("2000002"));
        var answer = await ReliefApi.AssignAsync(supervisor, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));
        var (reliefClient, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E3001", ReliefApi.Phone("E3001"));

        Assert.Equal(ReliefApi.Committed, answer.Answer!.Outcome);

        var adminWeek = await ReliefApi.WeekAsync(admin, ReliefApi.Week);
        var supervisorWeek = await ReliefApi.WeekAsync(supervisor, ReliefApi.Week);
        var otherWeek = await ReliefApi.WeekAsync(other, ReliefApi.Week);
        var reliefWeek = await ReliefApi.MyWeekAsync(reliefClient, ReliefApi.Week);

        var row = Assert.Single(adminWeek.Relief);
        Assert.Equal((relief, "AR01", "B", "Day"), (row.UserId, row.AreaCode, row.SourceBuildingCode, row.Shift));
        Assert.Equal("แม่บ้านทดสอบ E3001", row.DisplayName);
        Assert.Equal(1, row.BreakGroup);
        Assert.False(row.IsCancelled);
        Assert.Equal("1", row.Revision);
        Assert.Single(supervisorWeek.Relief);
        Assert.Empty(otherWeek.Relief);
        Assert.Equal(relief, Assert.Single(reliefWeek.Relief).UserId);

        // Ruling: a cancelled row is retained with its state rather than disappearing from the read.
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.ReliefAssignments.SingleAsync();
            stored.IsCancelled = true;
            stored.Revision += 1;
            await db.SaveChangesAsync();
        });
        var cancelled = Assert.Single((await ReliefApi.WeekAsync(admin, ReliefApi.Week)).Relief);
        Assert.True(cancelled.IsCancelled);
        Assert.Equal("2", cancelled.Revision);
    }
}

/// <summary>
/// The relief fixtures and calls the two relief test files share: a Relief Cleaner created through the accounts endpoint (so
/// the dated profile is written by the product, not by the test), a Supervisor of a building, the command call with its
/// expected revisions, and the week answer that carries <c>relief</c> beside the roster rows.
/// </summary>
internal static class ReliefApi
{
    public const string AssignRelief = "assign-relief";
    public const string Committed = "committed";
    public const string Rejected = "rejected";
    public const string Eligible = "eligible";
    public const string Conflict = "conflict";

    public static readonly DateOnly Week = new(2026, 11, 16);

    /// <summary>Tuesday 17 November 2026: the business date of every relief test.</summary>
    public static readonly DateOnly Today = new(2026, 11, 17);

    /// <summary>An editable future date in the same week.</summary>
    public static readonly DateOnly Wednesday = new(2026, 11, 18);

    public static JanitorialApiFactory NewFactory() => ScheduleWriteApi.NewFactory();

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ReliefKey(int userId, DateOnly date, string shift) =>
        $"relief:{userId}:{Iso(date)}:{shift}";

    /// <summary>A Relief Cleaner through the accounts endpoint, so the profile period is the product's own dated row.</summary>
    public static async Task<int> AddReliefAsync(
        JanitorialApiFactory factory, HttpClient admin, string employeeId, string buildingCode, Shift shift)
    {
        var home = await AdminSetupApi.BuildingIdAsync(factory, buildingCode);
        var response = await admin.PostAsJsonAsync("/api/admin/accounts", new
        {
            role = "Cleaner",
            displayName = $"แม่บ้านทดสอบ {employeeId}",
            employeeId,
            phone = Phone(employeeId),
            shift = shift.ToString(),
            cleanerKind = "Relief",
            homeBuildingId = home,
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return (await AccountApi.ReadAsync(response)).Id;
    }

    /// <summary>A Supervisor of one building through the accounts endpoint, which writes their dated scope from today.</summary>
    public static async Task<int> AddSupervisorAsync(
        JanitorialApiFactory factory, HttpClient admin, string employeeId, string buildingCode, Shift shift)
    {
        var building = await AdminSetupApi.BuildingIdAsync(factory, buildingCode);
        var response = await admin.PostAsJsonAsync("/api/admin/accounts", new
        {
            role = "Supervisor",
            displayName = $"หัวหน้าทดสอบ {employeeId}",
            employeeId,
            phone = Phone(employeeId),
            buildingId = building,
            shift = shift.ToString(),
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return (await AccountApi.ReadAsync(response)).Id;
    }

    /// <summary>A phone nobody else uses, derived from the employee id so two tests never collide on one.</summary>
    public static string Phone(string employeeId) =>
        "089" + new string(employeeId.Where(char.IsDigit).Select(c => c).DefaultIfEmpty('0').Take(7).ToArray()).PadRight(7, '0');

    public static object Assignment(
        int userId, int areaId, DateOnly date, int sourceBuildingId, int? replacedUserId = null, int? breakGroup = null,
        int? sourceSupervisorId = null, bool agreementReported = false, string? reason = null) => new
    {
        userId,
        areaId,
        shiftDate = Iso(date),
        shift = "Day",
        replacedUserId,
        breakGroup,
        sourceBuildingId,
        sourceSupervisorId,
        agreementReported,
        reason,
    };

    public static Task<(HttpResponseMessage Response, ScheduleWriteApi.CommandAnswer? Answer)> AssignAsync(
        HttpClient client, object payload, Dictionary<string, string>? expected = null) =>
        ScheduleWriteApi.ExecuteAsync(client, AssignRelief, payload, expected);

    public static async Task<T?> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(body) ? default : JsonSerializer.Deserialize<T>(body, JsonSerializerOptions.Web);
    }

    public static async Task<WeekAnswer> WeekAsync(HttpClient client, DateOnly weekStart)
    {
        var response = await client.GetAsync($"/api/schedules?weekStart={Iso(weekStart)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync<WeekAnswer>(response))!;
    }

    /// <summary>A Cleaner's own week: the route that carries no filter a caller could widen (spec §3).</summary>
    public static async Task<WeekAnswer> MyWeekAsync(HttpClient client, DateOnly weekStart)
    {
        var response = await client.GetAsync($"/api/schedules/me?weekStart={Iso(weekStart)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync<WeekAnswer>(response))!;
    }

    public static async Task<Domain.Entities.Scheduling.ReliefAssignment> ReadSingleReliefAsync(JanitorialApiFactory factory) =>
        await AdminSetupApi.ReadAsync(factory, db => db.ReliefAssignments.AsNoTracking().SingleAsync());

    /// <summary>A relief row somebody already recorded, written straight to the database as the stored fact to contend with.</summary>
    public static Task AddStoredReliefAsync(
        JanitorialApiFactory factory, int userId, int areaId, DateOnly date, Shift shift, int sourceBuildingId) =>
        factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            db.ReliefAssignments.Add(new Domain.Entities.Scheduling.ReliefAssignment
            {
                UserId = userId,
                AreaId = areaId,
                ShiftDate = date,
                Shift = shift,
                BreakGroup = 1,
                SourceBuildingId = sourceBuildingId,
                Revision = 1,
                CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    /// <summary>An agreed leave interval covering the whole shift, as the leave command stores it.</summary>
    public static Task AddLeaveAsync(JanitorialApiFactory factory, int userId, DateOnly date, Shift shift) =>
        factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            var start = date.ToDateTime(new TimeOnly(7, 0), DateTimeKind.Utc);
            db.ScheduleExceptions.Add(new Domain.Entities.Scheduling.ScheduleException
            {
                UserId = userId,
                Kind = Domain.Entities.Scheduling.ScheduleExceptionKind.Leave,
                ShiftDate = date,
                Shift = shift,
                StartsAt = start,
                EndsAt = start.AddHours(12),
                Revision = 1,
                CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    public static async Task<Domain.Entities.Scheduling.ScheduleChange> ReadSingleChangeAsync(JanitorialApiFactory factory) =>
        await AdminSetupApi.ReadAsync(factory, db => db.ScheduleChanges.AsNoTracking().SingleAsync());

    /// <summary>Nothing at all was stored: no relief row, no audit row, no receipt for a refused command.</summary>
    public static Task AssertNoReliefAsync(JanitorialApiFactory factory) =>
        factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.ReliefAssignments.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });

    public sealed record CandidateAnswer(
        int UserId, string EmployeeId, string DisplayName, int HomeBuildingId, string HomeBuildingCode, string Shift,
        bool Eligible, string Status, List<string> Conflicts);

    public sealed record WeekAnswer(DateOnly WeekStart, string Revision, List<ReliefAnswer> Relief);

    public sealed record ReliefAnswer(
        int Id, int UserId, string DisplayName, DateOnly ShiftDate, string Shift, int AreaId, string AreaCode,
        int DestinationBuildingId, string DestinationBuildingCode, int? BreakGroup, int? ReplacedUserId,
        string? ReplacedDisplayName, int SourceBuildingId, string SourceBuildingCode, int? SourceSupervisorId,
        string? SourceSupervisorName, bool AgreementReported, bool IsCancelled, string Revision);
}
