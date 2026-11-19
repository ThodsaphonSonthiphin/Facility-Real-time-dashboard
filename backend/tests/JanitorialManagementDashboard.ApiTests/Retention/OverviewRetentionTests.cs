using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JanitorialManagementDashboard.ApiTests.Retention;

public class OverviewRetentionTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Lower = new(2025, 10, 30, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CaptureStarted = Lower.AddMonths(-6);
    private static readonly RetentionSettings Settings = new() { BatchSize = 1 };

    [Fact]
    public async Task Prunes_old_target_images_in_batches_without_counting_them_as_expired_source_records()
    {
        using var factory = new JanitorialApiFactory();
        var ids = await AnnualRetentionTests.PrepareAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var anchor = Revision(4, Lower);
            var head = Revision(5, Now.AddDays(-1));
            await SeedRevisions(db, head, Revision(1, CaptureStarted), Revision(2, Lower.AddDays(-3)),
                Revision(3, Lower.AddDays(-1)), anchor);

            var sign = await db.Signs.SingleAsync(s => s.Id == ids.Sign);
            var annualCutoff = Now.AddYears(-1);
            db.ShiftAttendances.AddRange(
                new() { UserId = ids.User, AreaId = sign.AreaId, ShiftDate = new(2025, 11, 3),
                    OccurredAt = annualCutoff, Longitude = 1 },
                new() { UserId = ids.User, AreaId = sign.AreaId, ShiftDate = new(2025, 11, 3),
                    OccurredAt = annualCutoff.AddMinutes(1), EventType = AttendanceEvent.ShiftOut, Longitude = 1 });
            var archived = AnnualRetentionTests.Scan(ids, 1);
            var retained = AnnualRetentionTests.Scan(ids, 1);
            retained.ShiftDate = new(2025, 11, 1);
            retained.SubmittedAt = new(2025, 11, 1, 1, 0, 0, DateTimeKind.Utc);
            db.ScanRecords.AddRange(archived, retained);
            db.InspectionRecords.Add(new() { ScanRecord = archived, ServicePointId = ids.Point,
                SupervisorId = ids.Supervisor, InspectedAt = archived.SubmittedAt.AddMinutes(1),
                Result = InspectionResult.Passed });
            await db.SaveChangesAsync();

            var result = await new RetentionRunner(db, factory.Clock, Settings).RunAsync(default);

            Assert.Equal(new RetentionResult(2, 1, 3), result);
            db.ChangeTracker.Clear();
            Assert.Equal(new long[] { anchor.Id, head.Id }, await RetainedIds(db));
            Assert.Equal(head.Id, (await db.OverviewTargetStates.SingleAsync()).LatestRevisionId);
            var retainedAnchor = await db.OverviewTargetRevisions.SingleAsync(r => r.Id == anchor.Id);
            Assert.Equal(anchor.Payload, retainedAnchor.Payload);
            Assert.Equal(CaptureStarted, JsonSerializer.Deserialize<TargetImage>(retainedAnchor.Payload)!.CaptureStartedAtUtc);
            Assert.Null((await db.ShiftAttendances.SingleAsync()).Longitude);
            Assert.Equal(retained.Id, (await db.ScanRecords.SingleAsync()).Id);
            Assert.Empty(await db.InspectionRecords.ToListAsync());
            Assert.Equal(new DateOnly(2025, 10, 1), (await db.PointMonthlySummaries.SingleAsync()).Month);

            Assert.Equal(new RetentionResult(), await new RetentionRunner(db, factory.Clock, Settings).RunAsync(default));
        });
    }

    [Fact]
    public async Task No_predecessor_keeps_every_revision_and_legacy_capture_start_unknown()
    {
        using var factory = new JanitorialApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(Now));
        await factory.WithDbAsync(async db =>
        {
            var first = Revision(1, Lower.AddMinutes(1), legacy: true);
            var head = Revision(2, Now.AddDays(-1), legacy: true);
            await SeedRevisions(db, head, first);

            Assert.Equal(0, await OverviewTargetRetention.PruneAsync(db, Now, Settings, default));
            Assert.Equal(new long[] { first.Id, head.Id }, await RetainedIds(db));

            var newHeadId = await OverviewHistoryBootstrap.EnsureAsync(db,
                factory.Services.GetRequiredService<AttendanceSettings>(), factory.Clock, default);
            var newHead = await db.OverviewTargetRevisions.SingleAsync(r => r.Id == newHeadId);
            Assert.NotEqual(head.Id, newHead.Id);
            Assert.Null(JsonSerializer.Deserialize<TargetImage>(newHead.Payload)!.CaptureStartedAtUtc);
        });
    }

    [Fact]
    public async Task Equal_time_ids_break_anchor_ties_and_even_an_older_state_head_is_preserved()
    {
        using var factory = new JanitorialApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var head = Revision(1, Lower.AddDays(-5));
            var later = Revision(2, Lower.AddMinutes(1));
            var sameTime = Revision(3, Lower);
            var anchor = Revision(10, Lower);
            var earlier = Revision(11, Lower.AddMinutes(-1));
            await SeedRevisions(db, head, later, sameTime, anchor, earlier);

            Assert.Equal(2, await OverviewTargetRetention.PruneAsync(db, Now, Settings, default));
            Assert.Equal(new long[] { head.Id, later.Id, anchor.Id }, await RetainedIds(db));
            Assert.Equal(head.Id, (await db.OverviewTargetStates.SingleAsync()).LatestRevisionId);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unreadable_revision_does_not_turn_into_a_zero_target_image(bool unknownSchema)
    {
        using var factory = new JanitorialApiFactory();
        var slot = new ShiftSlot(new(2026, 11, 3), Shift.Day);
        var start = ShiftCalendar.StartUtc(slot);
        var previous = Revision(1, start.AddDays(-1));
        var superseded = Revision(2, start.AddHours(1));
        var invalid = Revision(3, start.AddHours(1));
        if (unknownSchema)
            invalid.SchemaVersion++;
        else
        {
            invalid.Payload = """{"SyntheticMarker":"sensitive-fixture"}""";
            invalid.PayloadHash = Hash(invalid.Payload);
        }
        var restored = Revision(4, start.AddHours(3));
        // MySQL's native JSON format may change the property order and spacing.
        using (var document = JsonDocument.Parse(restored.Payload))
            restored.Payload = "{ " + string.Join(", ", document.RootElement.EnumerateObject().Reverse()) + " }";

        var (revisions, gaps, structuralGaps) = OverviewTargetRecorder.DecodeForRead([restored, invalid, superseded, previous],
            factory.Services.GetRequiredService<ILogger<OverviewRetentionTests>>());
        Assert.Equal(new long[] { previous.Id, restored.Id }, revisions.Select(r => r.Id));
        Assert.Equal((invalid.EffectiveAt, restored.EffectiveAt), Assert.Single(gaps));
        Assert.Empty(structuralGaps);
        var warning = Assert.Single(factory.Logs.Lines, line => line.Message.Contains("Could not read overview target revision"));
        Assert.Equal("Could not read overview target revision 3", warning.Message);

        var coverage = DetailCoverage.FromRetention(start.AddHours(4), Settings, CaptureStarted, true, [])
            with { UnreadableTargetIntervals = gaps };
        var during = OverviewTime.Evaluate(slot, start.AddHours(2), revisions);
        var uncertain = OverviewAvailability.Calculate(slot, during, OverviewTimeline.Build(slot, during, revisions), coverage);
        Assert.Equal("partial", uncertain.Work.State);
        Assert.Contains("targetRevisionUnreadable", uncertain.Work.Reasons);

        var after = OverviewTime.Evaluate(slot, restored.EffectiveAt, revisions);
        var resumed = OverviewAvailability.Calculate(slot, after, OverviewTimeline.Build(slot, after, revisions), coverage);
        Assert.Equal("complete", resumed.Work.State);
        Assert.Equal("partial", resumed.History.State);
    }

    [Fact]
    public void Superseded_structural_gap_evidence_survives_an_unreadable_equal_time_winner()
    {
        using var factory = new JanitorialApiFactory();
        var slot = new ShiftSlot(new(2026, 11, 3), Shift.Day);
        var start = ShiftCalendar.StartUtc(slot);
        var end = ShiftCalendar.EndUtc(slot);
        var baseline = new TargetImage(1, new(60, 0), [], [], [], [], CaptureStarted);
        var early = Revision(1, start.AddDays(-1), baseline);
        var gapFrom = end.AddHours(-1);
        var changedAt = end.AddHours(1);
        var gapMarker = Revision(2, changedAt, baseline with { CoverageGapFromUtc = gapFrom });
        var unreadableWinner = Revision(3, changedAt, baseline);
        unreadableWinner.SchemaVersion++;

        var decoded = OverviewTargetRecorder.DecodeForRead([unreadableWinner, gapMarker, early],
            factory.Services.GetRequiredService<ILogger<OverviewRetentionTests>>());
        Assert.Equal(early.Id, Assert.Single(decoded.Revisions).Id);
        Assert.Equal((gapFrom, changedAt), Assert.Single(decoded.StructuralGaps));
        Assert.Equal((changedAt, DateTime.MaxValue), Assert.Single(decoded.UnreadableIntervals));

        var readAt = changedAt.AddHours(1);
        var coverage = DetailCoverage.FromRetention(readAt, Settings, CaptureStarted, true, decoded.StructuralGaps)
            with { UnreadableTargetIntervals = decoded.UnreadableIntervals };
        var moment = OverviewTime.Evaluate(slot, readAt, decoded.Revisions);
        Assert.Equal("closed", moment.Phase);
        Assert.Equal(end, moment.EvaluationUtc);

        var availability = OverviewAvailability.Calculate(slot, moment, OverviewTimeline.Build(slot, moment, decoded.Revisions), coverage);

        Assert.Equal("partial", availability.History.State);
        Assert.Contains("targetCoverageGap", availability.History.Reasons);
        Assert.Equal("partial", availability.Work.State);
    }

    [Theory]
    [InlineData("windowShift")]
    [InlineData("areaShiftPattern")]
    [InlineData("missingBuilding")]
    [InlineData("missingArea")]
    [InlineData("missingPoint")]
    [InlineData("duplicateBuilding")]
    [InlineData("duplicateArea")]
    [InlineData("duplicatePoint")]
    [InlineData("duplicateWindow")]
    public void Hash_valid_inconsistent_target_images_are_unreadable(string defect)
    {
        using var factory = new JanitorialApiFactory();
        var building = new TargetBuilding(1, "B1", "Synthetic Building", true, CaptureStarted);
        var area = new TargetArea(2, building.Id, "A2", "Synthetic Area", true, ShiftPattern.DayAndNight, CaptureStarted);
        var point = new TargetPoint(3, area.Id, null, "Synthetic Point", 0, true, CaptureStarted);
        var window = new TargetWindow(4, point.Id, Shift.Day, new(7, 0), new(8, 0), CaptureStarted);
        var image = new TargetImage(1, new(60, 60), [building], [area], [point], [window], CaptureStarted);
        var corrupted = defect switch
        {
            "windowShift" => image with { Windows = [window with { Shift = (Shift)99 }] },
            "areaShiftPattern" => image with { Areas = [area with { ShiftPattern = (ShiftPattern)99 }] },
            "missingBuilding" => image with { Areas = [area with { BuildingId = 99 }] },
            "missingArea" => image with { Points = [point with { AreaId = 99 }] },
            "missingPoint" => image with { Windows = [window with { PointId = 99 }] },
            "duplicateBuilding" => image with { Buildings = [building, building] },
            "duplicateArea" => image with { Areas = [area, area] },
            "duplicatePoint" => image with { Points = [point, point] },
            "duplicateWindow" => image with { Windows = [window, window] },
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };
        var bad = Revision(1, Now, corrupted);
        var restored = Revision(2, Now.AddHours(1), image);

        var decoded = OverviewTargetRecorder.DecodeForRead([bad, restored],
            factory.Services.GetRequiredService<ILogger<OverviewRetentionTests>>());

        Assert.Equal(restored.Id, Assert.Single(decoded.Revisions).Id);
        Assert.Equal((bad.EffectiveAt, restored.EffectiveAt), Assert.Single(decoded.UnreadableIntervals));
        var warning = Assert.Single(factory.Logs.Lines, line => line.Message.Contains("Could not read overview target revision"));
        Assert.Equal("Could not read overview target revision 1", warning.Message);
    }

    private static OverviewTargetRevision Revision(long id, DateTime effectiveAt, bool legacy = false) =>
        Revision(id, effectiveAt, new TargetImage(1, new(60, 60), [], [], [], [], legacy ? null : CaptureStarted));

    private static OverviewTargetRevision Revision(long id, DateTime effectiveAt, TargetImage image)
    {
        var payload = JsonSerializer.Serialize(image);
        return new() { Id = id, EffectiveAt = effectiveAt, Payload = payload, PayloadHash = Hash(payload) };
    }

    private static string Hash(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    private static async Task SeedRevisions(AppDbContext db, OverviewTargetRevision head, params OverviewTargetRevision[] others)
    {
        db.OverviewTargetRevisions.AddRange(others);
        db.OverviewTargetRevisions.Add(head);
        (await db.OverviewTargetStates.SingleAsync()).LatestRevisionId = head.Id;
        await db.SaveChangesAsync();
    }

    private static Task<long[]> RetainedIds(AppDbContext db) =>
        db.OverviewTargetRevisions.OrderBy(r => r.Id).Select(r => r.Id).ToArrayAsync();
}
