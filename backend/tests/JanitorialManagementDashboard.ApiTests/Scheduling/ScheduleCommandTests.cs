using System.Globalization;
using System.Text.Json;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §11 and acceptance A18, A19 and A27: one schedule command applies at most once, a repeated identity returns the
/// original result instead of a second mutation, a changed payload under the same identity is a conflict, a stale
/// expected revision writes nothing at all, a lost response is answered by the retained result, and an expired identity
/// can never restart a command even after its receipt has been purged.
/// <para>
/// Task 5 owns the command engine only, so the handler below is this file's synthetic stand-in for a real command kind:
/// it validates and writes one real <see cref="WeeklyDayOff"/> header, which is enough to prove the transaction, the
/// audit row, the revision coupling and the rollback against stored rows. <c>ScheduleCommandHandlers</c> and the real
/// kinds are Task 6's.
/// </para>
/// </summary>
public class ScheduleCommandTests
{
    /// <summary>A synthetic key: the real one is configuration, never a default in product code.</summary>
    private const string TestKey = "synthetic-scheduling-command-key-32-bytes-minimum";

    private static readonly DateOnly Week = new(2026, 11, 16);

    [Fact]
    public void Command_token_is_bound_to_actor_and_expires()
    {
        var service = new ScheduleCommandTokenService("synthetic-test-key-32-bytes-minimum-only");
        var now = DateTimeOffset.Parse("2026-11-17T00:00:00Z");
        var token = service.Issue(10, now);
        Assert.True(service.Verify(token.Value, 10, now).Accepted);
        Assert.False(service.Verify(token.Value, 11, now).Accepted);
        Assert.False(service.Verify(token.Value, 10, now.AddHours(25)).Accepted);
    }

    [Fact]
    public void A_command_token_lasts_exactly_twenty_four_hours_and_a_tampered_one_is_not_ours()
    {
        var service = new ScheduleCommandTokenService(TestKey);
        var now = DateTimeOffset.Parse("2026-11-17T00:00:00Z");

        var first = service.Issue(10, now);
        var second = service.Issue(10, now);

        // Every issue is a fresh identity: a client cannot choose the CommandId the server signs for it.
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(now.AddHours(24), first.ExpiresAt);
        Assert.Equal(first.Id, service.Verify(first.Value, 10, now).Id);

        // 24 hours is already too late; one tick less still starts a command.
        Assert.True(service.Verify(first.Value, 10, now.AddHours(24).AddTicks(-1)).Accepted);
        Assert.False(service.Verify(first.Value, 10, now.AddHours(24)).Accepted);

        // A value the server did not sign is never accepted, whatever it looks like. Tamper a character that carries
        // six whole signature bits: the final character of a 43-character base64url HMAC holds only four bits of
        // signature data, so swapping its padding bits decodes to the same bytes and the token would still verify.
        var separator = first.Value.LastIndexOf('.');
        var tampered = first.Value[..(separator + 1)]
            + (first.Value[separator + 1] == 'A' ? 'B' : 'A')
            + first.Value[(separator + 2)..];
        Assert.NotEqual(first.Value, tampered);
        Assert.False(service.Verify(tampered, 10, now).Accepted);
        Assert.False(service.Verify("not-a-token", 10, now).Accepted);
        Assert.False(service.Verify(string.Empty, 10, now).Accepted);
    }

    [Fact]
    public async Task The_same_identity_and_payload_returns_the_retained_result_and_writes_one_audit_row()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        var envelope = harness.NewEnvelope(admin, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0"));

        var first = await harness.ExecuteAsync(admin, envelope);
        var second = await harness.ExecuteAsync(admin, envelope);

        Assert.Equal(ScheduleCommandOutcome.Committed, first.Outcome);
        Assert.Equal(ScheduleCommandOutcome.Committed, second.Outcome);
        AssertSameRevisions(first.Revisions, second.Revisions);
        Assert.Equal(first.CommandId, second.CommandId);
        Assert.Empty(second.Problems);

        // One business result, one receipt, one audit row: the repeat is not a second mutation.
        await harness.WithDbAsync(async db =>
        {
            var header = await db.WeeklyDayOffs.Include(week => week.Choices).SingleAsync();
            Assert.Equal(1, header.Revision);
            Assert.Equal(new DateOnly(2026, 11, 17), Assert.Single(header.Choices).OffDate);
            Assert.Single(await db.ScheduleCommandReceipts.ToListAsync());
            var change = Assert.Single(await db.ScheduleChanges.ToListAsync());
            Assert.Equal("WeeklyDayOff", change.EntityType);
            Assert.Equal("create", change.Operation);
        });
    }

    [Fact]
    public async Task The_same_identity_with_a_changed_payload_is_a_conflict()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        var token = harness.Issue(admin);
        var committed = await harness.ExecuteAsync(admin, harness.EnvelopeOf(token, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0")));

        // A18: the same identity with different content is refused and changes nothing.
        var conflict = await harness.ExecuteAsync(admin,
            harness.EnvelopeOf(token, Payload(person, Week, "2026-11-18"), Revision(person, Week, "0")));

        Assert.Equal(ScheduleCommandOutcome.Committed, committed.Outcome);
        Assert.Equal(ScheduleCommandOutcome.PayloadMismatch, conflict.Outcome);
        Assert.Empty(conflict.Revisions);
        await harness.WithDbAsync(async db =>
        {
            var header = await db.WeeklyDayOffs.Include(week => week.Choices).SingleAsync();
            Assert.Equal(new DateOnly(2026, 11, 17), Assert.Single(header.Choices).OffDate);
            Assert.Single(await db.ScheduleCommandReceipts.ToListAsync());
            Assert.Single(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_stale_expected_revision_writes_nothing_and_returns_the_current_revision()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        await harness.ExecuteAsync(admin, harness.NewEnvelope(admin, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0")));

        var stale = await harness.ExecuteAsync(admin, harness.NewEnvelope(admin, Payload(person, Week, "2026-11-18"), Revision(person, Week, "0")));

        Assert.Equal(ScheduleCommandOutcome.Rejected, stale.Outcome);
        var problem = Assert.Single(stale.Problems);
        Assert.Equal(ScheduleCommandErrorCode.StaleRevision, problem.Code);
        Assert.Equal(RevisionKey(person, Week), problem.RowId);

        // The client keeps its input and compares against the current revision the answer names.
        Assert.Equal("1", stale.Revisions[RevisionKey(person, Week)]);
        await harness.WithDbAsync(async db =>
        {
            var header = await db.WeeklyDayOffs.Include(week => week.Choices).SingleAsync();
            Assert.Equal(1, header.Revision);
            Assert.Equal(new DateOnly(2026, 11, 17), Assert.Single(header.Choices).OffDate);
            Assert.Single(await db.ScheduleCommandReceipts.ToListAsync()); // no receipt for the refused command
        });
    }

    [Fact]
    public async Task One_bad_row_rolls_the_whole_command_back_and_leaves_no_receipt()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        var payload = Payload(person, Week, "2026-11-17", failRow: $"day:2026-11-18");

        var result = await harness.ExecuteAsync(admin, harness.NewEnvelope(admin, payload, Revision(person, Week, "0")));

        Assert.Equal(ScheduleCommandOutcome.Rejected, result.Outcome);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(result.Problems).Code);

        // The handler had already written the row it rejects: the rollback leaves no row, no audit and no receipt.
        await harness.WithDbAsync(async db =>
        {
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());
            Assert.Empty(await db.WeeklyDayOffChoices.ToListAsync());
            Assert.Empty(await db.ScheduleCommandReceipts.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
            Assert.Empty(await db.ScheduleAnchors.ToListAsync());
            Assert.Empty(await db.ScheduleBaselineAnchors.ToListAsync());
        });
        Assert.Empty(harness.Notifier.Calls);
    }

    [Fact]
    public async Task The_command_holds_the_person_slot_and_the_baseline_head_anchor()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");

        await harness.ExecuteAsync(admin, harness.NewEnvelope(admin, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0")));

        // Spec §11: a create locks the stable anchors even though no assignment row existed yet.
        await harness.WithDbAsync(async db =>
        {
            var slot = Assert.Single(await db.ScheduleAnchors.ToListAsync());
            Assert.Equal(person, slot.PersonId);
            Assert.Equal(new DateOnly(2026, 11, 17), slot.ShiftDate);
            Assert.Equal(Shift.Day, slot.Shift);
            Assert.Equal(person, Assert.Single(await db.ScheduleBaselineAnchors.ToListAsync()).PersonId);
        });
    }

    [Fact]
    public async Task Status_after_a_lost_response_returns_the_original_committed_result()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        var envelope = harness.NewEnvelope(admin, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0"));

        // A19: the response never reached the client, so it asks for the identity it already holds.
        var committed = await harness.ExecuteAsync(admin, envelope);
        harness.Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var status = await harness.ReadResultAsync(admin, envelope.CommandId);

        Assert.NotNull(status);
        Assert.Equal(ScheduleCommandOutcome.Committed, status!.Outcome);
        AssertSameRevisions(committed.Revisions, status.Revisions);
        Assert.Equal(committed.CommandId, status.CommandId);
        Assert.Single(await harness.ReceiptsAsync());
    }

    [Fact]
    public async Task An_expired_identity_cannot_restart_a_command_after_its_receipt_is_purged()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        var envelope = harness.NewEnvelope(admin, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0"));
        Assert.Equal(ScheduleCommandOutcome.Committed, (await harness.ExecuteAsync(admin, envelope)).Outcome);

        // A27: once the retained detail is purged the result is unavailable, and an old identity is still refused.
        await harness.WithDbAsync(async db =>
        {
            db.ScheduleChanges.RemoveRange(await db.ScheduleChanges.ToListAsync());
            db.ScheduleCommandReceipts.RemoveRange(await db.ScheduleCommandReceipts.ToListAsync());
            await db.SaveChangesAsync();
        });
        harness.Factory.Clock.Advance(TimeSpan.FromHours(25));

        var again = await harness.ExecuteAsync(admin, envelope);

        Assert.Equal(ScheduleCommandOutcome.Expired, again.Outcome);
        Assert.Empty(again.Revisions);
        Assert.Null(await harness.ReadResultAsync(admin, envelope.CommandId));
        await harness.WithDbAsync(async db =>
        {
            Assert.Equal(1, (await db.WeeklyDayOffs.SingleAsync()).Revision);
            Assert.Equal(new DateOnly(2026, 11, 17), Assert.Single(await db.WeeklyDayOffChoices.ToListAsync()).OffDate);
            Assert.Empty(await db.ScheduleCommandReceipts.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_foreign_actor_cannot_inspect_the_result()
    {
        using var harness = new Harness();
        await TestData.AddBuildingAsync(harness.Factory, "B");
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        var otherSupervisor = await TestData.AddSupervisorAsync(harness.Factory, "2000002", "0892222222", "B", Shift.Day);
        var envelope = harness.NewEnvelope(admin, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0"));
        await harness.ExecuteAsync(admin, envelope);

        // ReadResultAsync: another actor gets "not found" — no payload, no enumeration, no hint that it exists.
        Assert.Null(await harness.ReadResultAsync(otherSupervisor, envelope.CommandId));

        // ExecuteAsync: the same identity under another actor's own token is refused, and nothing changes.
        var stolen = harness.EnvelopeOf(harness.Issue(otherSupervisor), Payload(person, Week, "2026-11-18"), Revision(person, Week, "1"));
        var attempt = await harness.ExecuteAsync(otherSupervisor, stolen with { CommandId = envelope.CommandId });
        Assert.Equal(ScheduleCommandOutcome.NotAuthorised, attempt.Outcome);
        Assert.Empty(attempt.Revisions);
        await harness.WithDbAsync(async db =>
        {
            Assert.Equal(1, (await db.WeeklyDayOffs.SingleAsync()).Revision);
            Assert.Single(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task Status_rechecks_the_current_scope_and_not_only_the_identity()
    {
        using var harness = new Harness();
        await TestData.AddBuildingAsync(harness.Factory, "B");
        var supervisor = await TestData.AddSupervisorAsync(harness.Factory, "2000002", "0892222222", "B", Shift.Day);
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        var envelope = harness.NewEnvelope(supervisor, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0"));
        Assert.Equal(ScheduleCommandOutcome.Committed, (await harness.ExecuteAsync(supervisor, envelope)).Outcome);
        Assert.NotNull(await harness.ReadResultAsync(supervisor, envelope.CommandId));

        // Spec §11: losing the authority the command needed makes the result unavailable, never readable. This kind records
        // no building/shift scope, so what it loses here is the whole membership — the scope-specific case is the test below.
        await harness.WithDbAsync(async db =>
        {
            var account = await db.Users.SingleAsync(user => user.Id == supervisor);
            account.BuildingId = null;
            account.Shift = null;
            await db.SaveChangesAsync();
        });

        Assert.Null(await harness.ReadResultAsync(supervisor, envelope.CommandId));
    }

    [Fact]
    public async Task Status_requires_the_scope_the_command_applied_to_and_not_any_scope_held_today()
    {
        using var harness = new Harness();
        var buildingHeld = await TestData.AddBuildingAsync(harness.Factory, "B");
        await TestData.AddBuildingAsync(harness.Factory, "C");
        var today = BusinessDate(harness);
        var supervisor = await TestData.AddSupervisorAsync(harness.Factory, "2000002", "0892222222", "B", Shift.Day);
        await TestData.AddSupervisorScopeAsync(harness.Factory, "2000002", "B", Shift.Day, today.AddDays(-7));
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");

        var envelope = harness.NewEnvelope(
            supervisor, Payload(person, Week, "2026-11-17", buildingId: buildingHeld), Revision(person, Week, "0"));
        Assert.Equal(ScheduleCommandOutcome.Committed, (await harness.ExecuteAsync(supervisor, envelope)).Outcome);

        // Spec §11: the building/shift the command applied to is retained with its result.
        var receipt = Assert.Single(await harness.ReceiptsAsync());
        Assert.NotNull(receipt.ScopeJson);
        using var stored = JsonDocument.Parse(receipt.ScopeJson!);
        Assert.Equal(buildingHeld, Assert.Single(stored.RootElement.EnumerateArray()).GetProperty("BuildingId").GetInt32());

        // While the Supervisor still holds that building, the status answer is available.
        Assert.NotNull(await harness.ReadResultAsync(supervisor, envelope.CommandId));

        // Still an active Supervisor whose period covers today — but for another building. Holding a different building is
        // not holding the one this command changed, so the result is no longer theirs to read.
        await harness.WithDbAsync(async db =>
        {
            db.SupervisorScopePeriods.RemoveRange(await db.SupervisorScopePeriods.ToListAsync());
            await db.SaveChangesAsync();
        });
        await TestData.AddSupervisorScopeAsync(harness.Factory, "2000002", "C", Shift.Day, today.AddDays(-7));

        Assert.Null(await harness.ReadResultAsync(supervisor, envelope.CommandId));

        // A repeated submission of the same identity is a status answer too, so it refuses for the same reason instead of
        // handing back a result from a building this actor no longer holds — and it still writes nothing.
        var repeated = await harness.ExecuteAsync(supervisor, envelope);
        Assert.Equal(ScheduleCommandOutcome.NotAuthorised, repeated.Outcome);
        Assert.Empty(repeated.Revisions);

        // A read refusal changes nothing: the command it committed is still stored.
        await harness.WithDbAsync(async db =>
        {
            Assert.Equal(1, (await db.WeeklyDayOffs.SingleAsync()).Revision);
            Assert.Single(await db.ScheduleCommandReceipts.ToListAsync());
            Assert.Single(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_cleaner_cannot_start_a_command_and_nothing_is_written()
    {
        using var harness = new Harness();
        var cleaner = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");

        // The token is genuinely this actor's, issued by the server: what refuses the command is the authority gate, not
        // the identity. Spec §11: no account at all reaches a handler, so a Cleaner may never change a schedule.
        var result = await harness.ExecuteAsync(
            cleaner, harness.NewEnvelope(cleaner, Payload(cleaner, Week, "2026-11-17"), Revision(cleaner, Week, "0")));

        Assert.Equal(ScheduleCommandOutcome.NotAuthorised, result.Outcome);
        Assert.Empty(result.Revisions);
        Assert.Empty(result.Problems);
        Assert.Empty(harness.Notifier.Calls);
        await harness.WithDbAsync(async db =>
        {
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());
            Assert.Empty(await db.WeeklyDayOffChoices.ToListAsync());
            Assert.Empty(await db.ScheduleAnchors.ToListAsync());
            Assert.Empty(await db.ScheduleBaselineAnchors.ToListAsync());
            Assert.Empty(await db.ScheduleScopeAnchors.ToListAsync());
            Assert.Empty(await db.ScheduleCommandReceipts.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_supervisor_whose_periods_do_not_cover_today_cannot_start_a_command()
    {
        using var harness = new Harness();
        await TestData.AddBuildingAsync(harness.Factory, "B");
        var today = BusinessDate(harness);
        var supervisor = await TestData.AddSupervisorAsync(harness.Factory, "2000002", "0892222222", "B", Shift.Day);

        // One dated period that ended yesterday replaces the compatibility projection, so the Supervisor holds no scope
        // today: the same gate that refuses a Cleaner refuses a Supervisor without a covering period.
        await TestData.AddSupervisorScopeAsync(harness.Factory, "2000002", "B", Shift.Day, today.AddDays(-30), today);
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");

        var result = await harness.ExecuteAsync(
            supervisor, harness.NewEnvelope(supervisor, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0")));

        Assert.Equal(ScheduleCommandOutcome.NotAuthorised, result.Outcome);
        Assert.Empty(result.Revisions);
        await harness.WithDbAsync(async db =>
        {
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());
            Assert.Empty(await db.ScheduleAnchors.ToListAsync());
            Assert.Empty(await db.ScheduleCommandReceipts.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task The_notification_runs_after_the_commit_and_never_for_a_rejected_command()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");

        var committed = await harness.ExecuteAsync(admin, harness.NewEnvelope(admin, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0")));
        var stale = await harness.ExecuteAsync(admin, harness.NewEnvelope(admin, Payload(person, Week, "2026-11-18"), Revision(person, Week, "0")));

        Assert.Equal(ScheduleCommandOutcome.Committed, committed.Outcome);
        Assert.Equal(ScheduleCommandOutcome.Rejected, stale.Outcome);
        var notified = Assert.Single(harness.Notifier.Calls);

        // Ordering, not just counts: the commit is logged before the push, and the rejection was rolled back.
        var entries = harness.Log.Entries;
        var commitAt = entries.ToList().IndexOf("COMMIT");
        var notifyAt = entries.ToList().IndexOf("NOTIFY");
        Assert.True(commitAt >= 0, string.Join(",", entries));
        Assert.True(notifyAt > commitAt, string.Join(",", entries));
        Assert.Contains("ROLLBACK", entries);
        Assert.Equal(committed.CommandId, notified.CommandId);
    }

    [Fact]
    public async Task A_failed_notification_does_not_undo_the_committed_command()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        harness.Notifier.Failure = new InvalidOperationException("the push failed");

        var result = await harness.ExecuteAsync(admin, harness.NewEnvelope(admin, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0")));

        // Spec §12: a failed push never rolls back committed facts.
        Assert.Equal(ScheduleCommandOutcome.Committed, result.Outcome);
        await harness.WithDbAsync(async db =>
        {
            Assert.Equal(1, (await db.WeeklyDayOffs.SingleAsync()).Revision);
            Assert.Single(await db.ScheduleCommandReceipts.ToListAsync());
        });
    }

    [Fact]
    public async Task Reordered_payload_keys_are_the_same_fingerprint_but_reordered_rows_are_not()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        var token = harness.Issue(admin);

        using var ordered = JsonDocument.Parse($$"""{"personId":{{person}},"weekStart":"2026-11-16","offDates":["2026-11-17","2026-11-18"]}""");
        using var reorderedKeys = JsonDocument.Parse($$"""{"offDates":["2026-11-17","2026-11-18"],"weekStart":"2026-11-16","personId":{{person}}}""");
        using var reorderedRows = JsonDocument.Parse($$"""{"personId":{{person}},"weekStart":"2026-11-16","offDates":["2026-11-18","2026-11-17"]}""");
        var revisions = Revision(person, Week, "0");

        var first = harness.EnvelopeOf(token, ordered.RootElement.Clone(), revisions);
        var sameKeys = harness.EnvelopeOf(token, reorderedKeys.RootElement.Clone(), revisions);
        var sameRows = harness.EnvelopeOf(token, reorderedRows.RootElement.Clone(), revisions);

        // JSON object order is not content; array order is.
        Assert.Equal(ScheduleCommandService.Fingerprint(first), ScheduleCommandService.Fingerprint(sameKeys));
        Assert.NotEqual(ScheduleCommandService.Fingerprint(first), ScheduleCommandService.Fingerprint(sameRows));

        // And the same identity resubmitted with reordered keys is the same command, not a conflict.
        Assert.Equal(ScheduleCommandOutcome.Committed, (await harness.ExecuteAsync(admin, first)).Outcome);
        var repeated = await harness.ExecuteAsync(admin, sameKeys with { CommandId = first.CommandId, Token = first.Token });
        Assert.Equal(ScheduleCommandOutcome.Committed, repeated.Outcome);
        var conflict = await harness.ExecuteAsync(admin, sameRows with { CommandId = first.CommandId, Token = first.Token });
        Assert.Equal(ScheduleCommandOutcome.PayloadMismatch, conflict.Outcome);
        await harness.WithDbAsync(async db => Assert.Single(await db.ScheduleChanges.ToListAsync()));
    }

    [Fact]
    public void The_expected_revision_order_does_not_change_the_fingerprint()
    {
        var token = new ScheduleCommandTokenService(TestKey).Issue(1, DateTimeOffset.UtcNow);
        var payload = JsonSerializer.SerializeToElement(new { personId = 1 });
        var one = new ScheduleCommandEnvelope(token.Value, token.Id,
            new Dictionary<string, string> { ["week#1#2026-11-16"] = "3", ["baseline#1"] = "7" }, "kind", payload);
        var other = new ScheduleCommandEnvelope(token.Value, token.Id,
            new Dictionary<string, string> { ["baseline#1"] = "7", ["week#1#2026-11-16"] = "3" }, "kind", payload);
        var different = new ScheduleCommandEnvelope(token.Value, token.Id,
            new Dictionary<string, string> { ["baseline#1"] = "8", ["week#1#2026-11-16"] = "3" }, "kind", payload);

        Assert.Equal(ScheduleCommandService.Fingerprint(one), ScheduleCommandService.Fingerprint(other));
        Assert.NotEqual(ScheduleCommandService.Fingerprint(one), ScheduleCommandService.Fingerprint(different));
    }

    [Fact]
    public async Task An_expired_identity_still_answers_for_the_command_it_committed()
    {
        using var harness = new Harness();
        var admin = await harness.AdminIdAsync();
        var person = await AdminSetupApi.UserIdAsync(harness.Factory, "1000001");
        var envelope = harness.NewEnvelope(admin, Payload(person, Week, "2026-11-17"), Revision(person, Week, "0"));
        var committed = await harness.ExecuteAsync(admin, envelope);

        // A19: looking up the retained result comes before judging the start window, so an old identity can still report
        // what it committed — and only that.
        harness.Factory.Clock.Advance(TimeSpan.FromHours(25));
        var repeated = await harness.ExecuteAsync(admin, envelope);

        Assert.Equal(ScheduleCommandOutcome.Committed, repeated.Outcome);
        AssertSameRevisions(committed.Revisions, repeated.Revisions);
        await harness.WithDbAsync(async db =>
        {
            Assert.Equal(1, (await db.WeeklyDayOffs.SingleAsync()).Revision);
            Assert.Single(await db.ScheduleCommandReceipts.ToListAsync());
            Assert.Single(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public void A_missing_signing_key_does_not_break_startup_and_only_a_command_needs_it()
    {
        // The real registration, with no Scheduling:CommandSigningKey configured: the host still builds and serves.
        using var factory = new JanitorialApiFactory();
        using var scope = factory.Services.CreateScope();

        var settings = scope.ServiceProvider.GetRequiredService<SchedulingSettings>();
        Assert.False(settings.HasCommandSigningKey);

        var refused = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<ScheduleCommandTokenService>());
        Assert.Contains(SchedulingSettings.CommandSigningKeyName, refused.Message);
    }

    private static string RevisionKey(int personId, DateOnly weekStart) =>
        $"week#{personId}#{weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    /// <summary>The business date the engine's authority rule asks about, from the same clock the API under test uses.</summary>
    private static DateOnly BusinessDate(Harness harness) =>
        ScheduleAuthority.BusinessDate(harness.Factory.Clock.GetUtcNow().UtcDateTime);

    /// <summary>The same revision map, compared by key order rather than by dictionary enumeration order.</summary>
    private static void AssertSameRevisions(
        IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(
            expected.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"),
            actual.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static Dictionary<string, string> Revision(int personId, DateOnly weekStart, string revision) =>
        new(StringComparer.Ordinal) { [RevisionKey(personId, weekStart)] = revision };

    /// <summary>
    /// The synthetic kind's content. <paramref name="buildingId"/> is the building/shift the command applies to: a command
    /// that names one locks and records that scope, which is what the status lookup has to recheck.
    /// </summary>
    private static object Payload(int personId, DateOnly weekStart, string offDate, string? failRow = null, int? buildingId = null) => new
    {
        personId,
        weekStart = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        offDates = new[] { offDate },
        failRow,
        buildingId,
    };

    /// <summary>
    /// The engine, its clock, its keys and one synthetic command kind: the whole command path runs through the real DI
    /// registrations, so a service that only works when hand-wired fails here.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        public Harness()
        {
            Notifier = new RecordingNotifier(Log);
            Factory = new JanitorialApiFactory
            {
                ConfigureTestServices = services =>
                {
                    services.AddSingleton(new SchedulingSettings(TestKey)
                    {
                        HistoryStartsAt = new DateOnly(2026, 11, 17),
                    });
                    services.AddSingleton<IScheduleCommandNotifier>(Notifier);
                    services.AddScoped<IScheduleCommandHandler, SetWeeklyDaysOffHandler>();
                    services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(Log));
                },
            };
        }

        public JanitorialApiFactory Factory { get; }

        public CommandLog Log { get; } = new();

        public RecordingNotifier Notifier { get; }

        public Task WithDbAsync(Func<AppDbContext, Task> action) => Factory.WithDbAsync(action);

        public async Task<int> AdminIdAsync()
        {
            var id = 0;
            await Factory.WithDbAsync(async db => id = (await db.Users.SingleAsync(user => user.Username == "admin")).Id);
            return id;
        }

        public async Task<List<ScheduleCommandReceipt>> ReceiptsAsync()
        {
            var receipts = new List<ScheduleCommandReceipt>();
            await Factory.WithDbAsync(async db => receipts = await db.ScheduleCommandReceipts.ToListAsync());
            return receipts;
        }

        public IssuedCommandToken Issue(int actorId)
        {
            using var scope = Factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ScheduleCommandTokenService>()
                .Issue(actorId, Factory.Clock.GetUtcNow());
        }

        public ScheduleCommandEnvelope NewEnvelope(int actorId, object payload, Dictionary<string, string> revisions) =>
            EnvelopeOf(Issue(actorId), payload, revisions);

        public ScheduleCommandEnvelope EnvelopeOf(IssuedCommandToken token, object payload, Dictionary<string, string> revisions) =>
            new(token.Value, token.Id, revisions, SetWeeklyDaysOffHandler.CommandKind,
                JsonSerializer.SerializeToElement(payload));

        public ScheduleCommandEnvelope EnvelopeOf(IssuedCommandToken token, JsonElement payload, Dictionary<string, string> revisions) =>
            new(token.Value, token.Id, revisions, SetWeeklyDaysOffHandler.CommandKind, payload);

        public async Task<CommandResult> ExecuteAsync(int actorId, ScheduleCommandEnvelope command)
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ScheduleCommandService>()
                .ExecuteAsync(actorId, command, CancellationToken.None);
        }

        public async Task<CommandResult?> ReadResultAsync(int actorId, Guid commandId)
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ScheduleCommandService>()
                .ReadResultAsync(actorId, commandId, CancellationToken.None);
        }

        public void Dispose()
        {
            Notifier.Failure = null;
            Factory.Dispose();
        }
    }

    /// <summary>
    /// A stand-in command kind that is not Task 6's <c>ScheduleCommandHandlers</c>: it validates one real weekly day-off
    /// header and its revision, so the engine's transaction, audit and rollback are proved against stored rows. It never
    /// saves: the engine owns the single SaveChanges, which is what makes "any row failure rolls the whole command back"
    /// true rather than a handler-by-handler promise.
    /// </summary>
    private sealed class SetWeeklyDaysOffHandler : IScheduleCommandHandler
    {
        public const string CommandKind = "test-set-weekly-days-off";

        public string Kind => CommandKind;

        public Task<ScheduleCommandLockSet> PlanLocksAsync(
            ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default)
        {
            var payload = Read(request.Payload);
            return Task.FromResult(new ScheduleCommandLockSet(
                payload.OffDates.Select(date => new ScheduleSlotKey(payload.PersonId, date, Shift.Day)).ToList(),
                [payload.PersonId],
                payload.BuildingId is int buildingId ? [new ScheduleScopeKey(buildingId, Shift.Day)] : []));
        }

        public async Task<ScheduleCommandApplication> ValidateAndApplyAsync(
            ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default)
        {
            var (personId, weekStart, offDates, failRow, _) = Read(request.Payload);
            var key = RevisionKey(personId, weekStart);
            var header = await db.WeeklyDayOffs.Include(week => week.Choices)
                .FirstOrDefaultAsync(week => week.UserId == personId && week.WeekStart == weekStart, ct);
            var current = (header?.Revision ?? 0).ToString(CultureInfo.InvariantCulture);
            var revisions = new Dictionary<string, string>(StringComparer.Ordinal) { [key] = current };

            if (request.ExpectedRevisions.TryGetValue(key, out var expected) && expected != current)
            {
                return new ScheduleCommandApplication(revisions,
                    [new RowProblem(key, ScheduleCommandErrorCode.StaleRevision, $"expected {expected}, current {current}")]);
            }

            var created = header is null;
            header ??= new WeeklyDayOff
            {
                UserId = personId,
                WeekStart = weekStart,
                CreatedById = request.ActorId,
                CreatedAt = request.Now.UtcDateTime,
                UpdatedAt = request.Now.UtcDateTime,
            };
            if (created)
            {
                db.WeeklyDayOffs.Add(header);
            }

            // A choice-changing writer bumps the week header revision: the weekly day-off digest only moves when
            // WeeklyDayOff.Revision moves (controller ruling 5, spec §11). A new header starts at its own first version.
            header.Choices.Clear();
            foreach (var date in offDates)
            {
                header.Choices.Add(new WeeklyDayOffChoice { OffDate = date });
            }

            if (!created)
            {
                header.Revision += 1;
            }

            header.UpdatedAt = request.Now.UtcDateTime;

            db.ScheduleChanges.Add(new ScheduleChange
            {
                Receipt = receipt,
                EntityType = "WeeklyDayOff",
                EntityId = header.Id == 0 ? null : header.Id,
                Operation = created ? "create" : "update",
                BeforeJson = created ? null : $$"""{"revision":{{current}}}""",
                AfterJson = $$"""{"revision":{{header.Revision}}}""",
                CreatedAt = request.Now.UtcDateTime,
            });

            if (failRow is not null)
            {
                // Written, then refused: the engine's rollback is what keeps this row out of the database.
                return new ScheduleCommandApplication(revisions,
                    [new RowProblem(failRow, ScheduleCommandErrorCode.RowInvalid, "row rejected")]);
            }

            revisions[key] = header.Revision.ToString(CultureInfo.InvariantCulture);
            return new ScheduleCommandApplication(revisions, []);
        }

        private static (int PersonId, DateOnly WeekStart, List<DateOnly> OffDates, string? FailRow, int? BuildingId) Read(
            JsonElement payload) =>
            (payload.GetProperty("personId").GetInt32(),
                DateOnly.Parse(payload.GetProperty("weekStart").GetString()!, CultureInfo.InvariantCulture),
                payload.GetProperty("offDates").EnumerateArray()
                    .Select(date => DateOnly.Parse(date.GetString()!, CultureInfo.InvariantCulture)).ToList(),
                payload.TryGetProperty("failRow", out var failRow) ? failRow.GetString() : null,
                payload.TryGetProperty("buildingId", out var building) && building.ValueKind == JsonValueKind.Number
                    ? building.GetInt32()
                    : null);
    }

    /// <summary>Records the transaction outcomes the engine reaches, so a test can prove the push came after the commit.</summary>
    private sealed class CommandLog : DbTransactionInterceptor
    {
        private readonly List<string> _entries = [];

        public IReadOnlyList<string> Entries
        {
            get
            {
                lock (_entries)
                {
                    return _entries.ToList();
                }
            }
        }

        public void Add(string entry)
        {
            lock (_entries)
            {
                _entries.Add(entry);
            }
        }

        public override Task TransactionCommittedAsync(
            System.Data.Common.DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Add("COMMIT");
            return Task.CompletedTask;
        }

        public override Task TransactionRolledBackAsync(
            System.Data.Common.DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Add("ROLLBACK");
            return Task.CompletedTask;
        }

        public override void TransactionCommitted(System.Data.Common.DbTransaction transaction, TransactionEndEventData eventData) => Add("COMMIT");

        public override void TransactionRolledBack(System.Data.Common.DbTransaction transaction, TransactionEndEventData eventData) => Add("ROLLBACK");
    }

    /// <summary>The stand-in for Task 12's live notifications: it records the call and can be made to fail.</summary>
    private sealed class RecordingNotifier : IScheduleCommandNotifier
    {
        private readonly CommandLog _log;
        private readonly List<CommandResult> _calls = [];
        private readonly List<ScheduleChangeScope> _scopes = [];

        public RecordingNotifier(CommandLog log) => _log = log;

        public IReadOnlyList<CommandResult> Calls => _calls;

        /// <summary>The identities each committed command told the fan-out about — slots and building/shift scopes only.</summary>
        public IReadOnlyList<ScheduleChangeScope> Scopes => _scopes;

        public Exception? Failure { get; set; }

        public Task NotifyCommittedAsync(CommandResult result, ScheduleChangeScope scope, CancellationToken ct = default)
        {
            _calls.Add(result);
            _scopes.Add(scope);
            _log.Add("NOTIFY");
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
