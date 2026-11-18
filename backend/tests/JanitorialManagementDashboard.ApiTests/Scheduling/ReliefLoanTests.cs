using System.Net;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §6 with acceptance A09 and A10: a cross-building loan is recorded by the destination actor after the two Supervisors
/// agreed outside the web. The stored fact is that actor's attestation — never a source signature and never an in-app
/// approval — and it only excuses the loan when the source building and shift actually belongs to a different Supervisor.
/// A person who already has a duty, a leave interval or a live relief assignment is refused, and their source work is left
/// exactly as it was: no automatic cancel, no move, no conversion of a legacy cover row.
/// </summary>
public class ReliefLoanTests
{
    [Fact]
    public async Task A_loan_from_another_supervisors_building_needs_the_reported_agreement()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        var sourceSupervisor = await ReliefApi.AddSupervisorAsync(factory, admin, "2000002", "B", Shift.Day);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (actor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        // Nothing said about the source: no attestation, no named source Supervisor.
        var silent = await ReliefApi.AssignAsync(actor,
            ReliefApi.Assignment(relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));
        // A named Supervisor without the destination actor's attestation is not enough either.
        var unattested = await ReliefApi.AssignAsync(actor, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1,
            sourceSupervisorId: sourceSupervisor));
        // An attestation that names nobody cannot be checked against who actually holds the source.
        var unnamed = await ReliefApi.AssignAsync(actor, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1, agreementReported: true));

        Assert.Equal(HttpStatusCode.Conflict, silent.Response.StatusCode);
        Assert.Equal("row-invalid", Assert.Single(silent.Answer!.Problems).Code);
        Assert.Equal(HttpStatusCode.Conflict, unattested.Response.StatusCode);
        Assert.Contains("ตกลง", Assert.Single(unattested.Answer!.Problems).Message);
        Assert.Equal(HttpStatusCode.Conflict, unnamed.Response.StatusCode);
        await ReliefApi.AssertNoReliefAsync(factory);

        var committed = await ReliefApi.AssignAsync(actor, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1,
            sourceSupervisorId: sourceSupervisor, agreementReported: true));

        Assert.Equal(HttpStatusCode.OK, committed.Response.StatusCode);
        Assert.Equal(ReliefApi.Committed, committed.Answer!.Outcome);
        var stored = await ReliefApi.ReadSingleReliefAsync(factory);
        Assert.Equal((sourceBuilding, sourceSupervisor, true), (
            stored.SourceBuildingId, stored.SourceSupervisorId, stored.AgreementReported));
        // The audit row records an attestation on one side; it does not claim the server verified a conversation.
        var change = await ReliefApi.ReadSingleChangeAsync(factory);
        Assert.Contains("\"agreementReported\":true", change.AfterJson);
        Assert.Contains("\"attestationOf\"", change.AfterJson);
    }

    [Fact]
    public async Task A_scoped_cross_building_replacement_inherits_the_destination_group_and_names_the_relief_home_source()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "AR03", "B");
        var sourceSupervisor = await ReliefApi.AddSupervisorAsync(factory, admin, "2000002", "B", Shift.Day);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        await ReliefApi.AddReliefAsync(factory, admin, "E3002", "B", Shift.Night);
        var source = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var otherDestination = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var replaced = await ScheduleWriteApi.SetUpWeekAsync(factory, "1000001", breakGroup: 1);
        var (actor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var date = ReliefApi.Iso(ReliefApi.Wednesday);
        var lookup = $"/api/schedules/relief-candidates?sourceBuildingId={source}&date={date}&shift=Day";
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.GetAsync($"{lookup}&employeeId=E3001&destinationAreaId={otherDestination}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.GetAsync($"{lookup}&ownUserId={relief}&destinationAreaId={destination}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await actor.GetAsync($"{lookup}&employeeId=E3002&destinationAreaId={destination}")).StatusCode);

        var candidate = await actor.GetAsync($"{lookup}&employeeId=E3001&destinationAreaId={destination}");
        Assert.Equal(HttpStatusCode.OK, candidate.StatusCode);
        using var body = JsonDocument.Parse(await candidate.Content.ReadAsStringAsync());
        Assert.Equal(sourceSupervisor, body.RootElement.GetProperty("sourceSupervisorId").GetInt32());
        Assert.True(body.RootElement.GetProperty("agreementRequired").GetBoolean());
        Assert.Equal("0", body.RootElement.GetProperty("revision").GetString());

        var assigned = await ReliefApi.AssignAsync(actor, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, source, replacedUserId: replaced,
            sourceSupervisorId: sourceSupervisor, agreementReported: true));
        Assert.Equal(HttpStatusCode.OK, assigned.Response.StatusCode);
        var stored = await ReliefApi.ReadSingleReliefAsync(factory);
        Assert.Equal(source, stored.SourceBuildingId);
        Assert.Equal(sourceSupervisor, stored.SourceSupervisorId);
        Assert.Equal(replaced, stored.ReplacedUserId);
        Assert.Equal(1, stored.BreakGroup);
    }

    [Fact]
    public async Task A_named_source_supervisor_who_does_not_hold_the_source_building_is_refused()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await ReliefApi.AddSupervisorAsync(factory, admin, "2000002", "B", Shift.Day);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var actorId = await AdminSetupApi.UserIdAsync(factory, "2000001");
        var (actor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        // The actor is not the Supervisor of building B, so naming themselves does not stand in for B's Supervisor.
        var answer = await ReliefApi.AssignAsync(actor, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1,
            sourceSupervisorId: actorId, agreementReported: true));

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Equal("row-invalid", Assert.Single(answer.Answer!.Problems).Code);
        await ReliefApi.AssertNoReliefAsync(factory);
    }

    [Fact]
    public async Task One_supervisor_holding_both_buildings_needs_no_second_actor()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        // The same Supervisor holds A and B on the date: there is no second person whose agreement could be invented.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (actor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var answer = await ReliefApi.AssignAsync(actor,
            ReliefApi.Assignment(relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));

        Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);
        Assert.Equal(ReliefApi.Committed, answer.Answer!.Outcome);
        var stored = await ReliefApi.ReadSingleReliefAsync(factory);
        Assert.Null(stored.SourceSupervisorId);
        Assert.False(stored.AgreementReported);
        Assert.Equal(sourceBuilding, stored.SourceBuildingId);
    }

    [Fact]
    public async Task A_person_who_already_holds_a_regular_duty_is_refused_and_their_own_plan_is_untouched()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "AR03", "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        // One Supervisor holds the source building too, so the loan gate asks for nothing and the conflict is what answers.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        // Their own applicable plan for Wednesday already works in an Area of building B.
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "E3001", new DateOnly(2026, 10, 1), "AR03");
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (actor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var answer = await ReliefApi.AssignAsync(actor,
            ReliefApi.Assignment(relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        var problem = Assert.Single(answer.Answer!.Problems);
        Assert.Equal("row-invalid", problem.Code);
        Assert.Contains("งานประจำ", problem.Message);

        // The duty the person already holds is exactly as it was: no auto cancel, no auto move, no new relief row.
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.ReliefAssignments.ToListAsync());
            Assert.Empty(await db.ScheduleExceptions.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
            var baseline = await db.ScheduleBaselines.Include(entry => entry.Days).SingleAsync();
            Assert.Equal(5, baseline.Days.Count);
        });
    }

    [Fact]
    public async Task A_person_with_a_live_relief_assignment_cannot_be_assigned_again()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "AR03", "B", ShiftPattern.DayOnly);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        // One Supervisor holds the source building too, so the loan gate asks for nothing and the conflict is what answers.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var existing = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        await ReliefApi.AddStoredReliefAsync(factory, relief, existing, ReliefApi.Wednesday, Shift.Day, sourceBuilding);
        var (actor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var answer = await ReliefApi.AssignAsync(actor,
            ReliefApi.Assignment(relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Contains("งานสแปร์", Assert.Single(answer.Answer!.Problems).Message);
        Assert.Equal("1", answer.Answer.Revisions[ReliefApi.ReliefKey(relief, ReliefApi.Wednesday, "Day")]);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.ReliefAssignments.SingleAsync();
            Assert.Equal((existing, 1L, false), (stored.AreaId, stored.Revision, stored.IsCancelled));
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_person_on_leave_cannot_be_loaned()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        // One Supervisor holds the source building too, so the loan gate asks for nothing and the conflict is what answers.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        await ReliefApi.AddLeaveAsync(factory, relief, ReliefApi.Wednesday, Shift.Day);
        var (actor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var answer = await ReliefApi.AssignAsync(actor,
            ReliefApi.Assignment(relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));

        Assert.Equal(HttpStatusCode.Conflict, answer.Response.StatusCode);
        Assert.Contains("ลา", Assert.Single(answer.Answer!.Problems).Message);
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.ReliefAssignments.ToListAsync());
            // The leave row itself is not cancelled or shortened by the refusal.
            var leave = await db.ScheduleExceptions.SingleAsync();
            Assert.False(leave.IsCancelled);
            Assert.Equal(1L, leave.Revision);
        });
    }

    [Fact]
    public async Task A_loan_from_a_building_with_no_responsible_supervisor_needs_the_note_and_names_nobody()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var sourceSupervisor = await ReliefApi.AddSupervisorAsync(factory, admin, "2000002", "B", Shift.Day);
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var (actor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        // Deactivating B's Supervisor is enough: the dated rows still exist, but none of them gives B to an active holder.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/accounts/{sourceSupervisor}/deactivate", null)).StatusCode);

        // Naming the person who used to hold B is a row problem: nobody is the responsible Supervisor to attest for, so the
        // client's id is refused instead of being stored as an unexplained fact.
        var named = await ReliefApi.AssignAsync(actor, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1,
            sourceSupervisorId: sourceSupervisor, agreementReported: true));
        // And with no responsible Supervisor to ask, the destination actor's note is still what the row needs.
        var unattested = await ReliefApi.AssignAsync(actor, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1));

        Assert.Equal(HttpStatusCode.Conflict, named.Response.StatusCode);
        Assert.Equal("row-invalid", Assert.Single(named.Answer!.Problems).Code);
        Assert.Contains("หัวหน้าผู้รับผิดชอบ", Assert.Single(named.Answer.Problems).Message);
        Assert.Equal(HttpStatusCode.Conflict, unattested.Response.StatusCode);
        Assert.Equal("row-invalid", Assert.Single(unattested.Answer!.Problems).Code);
        await ReliefApi.AssertNoReliefAsync(factory);

        var committed = await ReliefApi.AssignAsync(actor, ReliefApi.Assignment(
            relief, destination, ReliefApi.Wednesday, sourceBuilding, breakGroup: 1, agreementReported: true));

        Assert.Equal(HttpStatusCode.OK, committed.Response.StatusCode);
        Assert.Equal(ReliefApi.Committed, committed.Answer!.Outcome);
        var stored = await ReliefApi.ReadSingleReliefAsync(factory);
        Assert.Null(stored.SourceSupervisorId);
        Assert.True(stored.AgreementReported);
        Assert.Equal(sourceBuilding, stored.SourceBuildingId);
    }

    [Fact]
    public async Task A_legacy_cover_candidate_list_still_offers_a_regular_cleaner_without_a_relief_profile()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "AR03", "B");
        await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var areaB = await AdminSetupApi.AreaIdAsync(factory, "AR03");

        // facility-0041 is untouched: an active Cleaner of the same shift with their own Area is still a candidate, with no
        // relief profile, break group or new required input anywhere on the legacy path.
        var response = await admin.GetAsync(
            $"/api/admin/cover-assignments/candidates?areaId={areaB}"
            + $"&shiftDate={ReliefApi.Iso(ReliefApi.Wednesday)}&shift=Day");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("1000001", body);
        // A relief has no regular Area, so the legacy candidate list does not offer them, exactly as before this task.
        Assert.DoesNotContain("E3001", body);
    }
}
