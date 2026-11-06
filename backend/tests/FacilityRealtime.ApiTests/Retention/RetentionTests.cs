using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Retention;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Retention;

public class RetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Gps_expiry_clears_all_four_tables_in_small_batches_preserving_boundary_newer_and_derived_data()
    {
        using var factory = new FacilityApiFactory();
        factory.Clock.SetUtcNow(Now);
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.FirstAsync(s => s.ServicePointId != null);
            var user = await db.Users.FirstAsync(u => u.Role == UserRole.Cleaner);
            var supervisor = await db.Users.FirstAsync(u => u.Role == UserRole.Supervisor);
            for (var n = 0; n < 3; n++)
            {
                var at = Now.UtcDateTime.AddDays(-90).AddTicks(n - 1);
                var scan = new ScanRecord { ServicePointId = sign.ServicePointId!.Value, SignId = sign.Id, UserId = user.Id,
                    ShiftDate = new(2026, 8, 5 + n), SubmittedAt = at, Latitude = 1, AccuracyM = 2, DistanceM = 3, WithinRadius = false };
                db.ScanRecords.Add(scan);
                db.ShiftAttendances.Add(new() { UserId = user.Id, AreaId = sign.AreaId, ShiftDate = scan.ShiftDate,
                    OccurredAt = at, Longitude = 1, DistanceM = 3, WithinRadius = false });
                db.BlockedScans.Add(new() { UserId = user.Id, SignId = sign.Id, ScannedAt = at, AccuracyM = 2, DistanceM = 3, WithinRadius = false });
                db.InspectionRecords.Add(new() { ScanRecord = scan, ServicePointId = scan.ServicePointId, SupervisorId = supervisor.Id,
                    InspectedAt = at, Result = InspectionResult.Passed, Latitude = 1, Longitude = 1, AccuracyM = 2, DistanceM = 3, WithinRadius = false });
            }
            await db.SaveChangesAsync();
            var result = await new RetentionRunner(db, factory.Clock, new() { BatchSize = 1 }).RunAsync(default);
            Assert.Equal(8, result.GpsCleared);
            db.ChangeTracker.Clear();
            var tables = new IGpsStamped[][] { await db.ScanRecords.OrderBy(x => x.SubmittedAt).ToArrayAsync(),
                await db.ShiftAttendances.OrderBy(x => x.OccurredAt).ToArrayAsync(), await db.BlockedScans.OrderBy(x => x.ScannedAt).ToArrayAsync(),
                await db.InspectionRecords.OrderBy(x => x.InspectedAt).ToArrayAsync() };
            foreach (var rows in tables)
            {
                Assert.All(rows.Take(2), row => Assert.Equal((null, null, null), (row.Latitude, row.Longitude, row.AccuracyM)));
                Assert.True(rows[2].Latitude != null || rows[2].Longitude != null || rows[2].AccuracyM != null);
                Assert.All(rows, row => Assert.Equal(((short?)3, (bool?)false), (row.DistanceM, row.WithinRadius)));
            }
            Assert.NotNull((await db.Signs.FindAsync(sign.Id))!.Latitude);
            Assert.Equal(0, (await new RetentionRunner(db, factory.Clock, new()).RunAsync(default)).GpsCleared);
        });
    }
}

public class AnnualRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

    internal static async Task<(int Point, int Sign, int User, int Supervisor)> PrepareAsync(FacilityApiFactory factory)
    {
        (int, int, int, int) ids = default;
        factory.Clock.SetUtcNow(Now);
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.FirstAsync(s => s.ServicePointId != null);
            var point = await db.ServicePoints.Include(p => p.Area).SingleAsync(p => p.Id == sign.ServicePointId);
            point.CreatedAt = new(2025, 10, 1);
            point.Area!.CreatedAt = new(2020, 1, 1);
            point.Area.ShiftPattern = ShiftPattern.DayOnly;
            db.PointRoundWindows.RemoveRange(await db.PointRoundWindows.Where(w => w.ServicePointId == point.Id).ToListAsync());
            db.PointRoundWindows.Add(new() { ServicePointId = point.Id, Shift = Shift.Day, StartTime = new(7, 0), EndTime = new(9, 0), CreatedAt = point.CreatedAt });
            await db.SaveChangesAsync();
            ids = (point.Id, sign.Id, (await db.Users.FirstAsync(u => u.Role == UserRole.Cleaner)).Id,
                (await db.Users.FirstAsync(u => u.Role == UserRole.Supervisor)).Id);
        });
        return ids;
    }

    internal static ScanRecord Scan((int Point, int Sign, int User, int Supervisor) ids, int day, Placement placement = Placement.OnTime,
        int hour = 1, bool deletedWindow = false) => new()
    {
        ServicePointId = ids.Point, SignId = ids.Sign, UserId = ids.User, ShiftDate = new(2025, 10, day), Shift = Shift.Day,
        RoundStart = placement == Placement.OffRound ? null : new TimeOnly(deletedWindow ? 10 : 7, 0),
        RoundEnd = placement == Placement.OffRound ? null : new TimeOnly(deletedWindow ? 11 : 9, 0),
        Placement = placement, SubmittedAt = new(2025, 10, day, hour, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task Complete_month_summary_counts_rounds_first_placement_verdicts_deleted_windows_issues_and_missing_then_is_idempotent()
    {
        using var factory = new FacilityApiFactory();
        var ids = await PrepareAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var first = Scan(ids, 1); first.Status = CleaningStatus.Issue;
            var fixedScan = Scan(ids, 1, Placement.Rework, 2);
            var late = Scan(ids, 2, Placement.Late);
            var notReworked = Scan(ids, 3);
            var deleted = Scan(ids, 4, deletedWindow: true);
            var offRound = Scan(ids, 5, Placement.OffRound); offRound.Status = CleaningStatus.Issue;
            var currentMonth = Scan(ids, 1); currentMonth.ShiftDate = new(2025, 11, 1); currentMonth.SubmittedAt = new(2025, 11, 1);
            db.ScanRecords.AddRange(first, fixedScan, late, notReworked, deleted, offRound, currentMonth);
            db.InspectionRecords.AddRange(
                new() { ScanRecord = first, ServicePointId = ids.Point, SupervisorId = ids.Supervisor, Result = InspectionResult.Rework, Defect = "synthetic", InspectedAt = first.SubmittedAt.AddMinutes(10) },
                new() { ScanRecord = fixedScan, ServicePointId = ids.Point, SupervisorId = ids.Supervisor, Result = InspectionResult.Passed, InspectedAt = fixedScan.SubmittedAt.AddMinutes(10) },
                new() { ScanRecord = notReworked, ServicePointId = ids.Point, SupervisorId = ids.Supervisor, Result = InspectionResult.Rework, Defect = "synthetic", InspectedAt = notReworked.SubmittedAt.AddMinutes(10) });
            await db.SaveChangesAsync();
            await new RetentionRunner(db, factory.Clock, new()).RunAsync(default);
            db.ChangeTracker.Clear();
            var summary = Assert.Single(await db.PointMonthlySummaries.Where(s => s.ServicePointId == ids.Point).ToListAsync());
            Assert.Equal(new DateOnly(2025, 10, 1), summary.Month);
            Assert.Equal((32, 3, 1, 28, 1, 2, 2, 2), (summary.RoundsTotal, summary.SubmittedOnTime, summary.SubmittedLate,
                summary.NotDone, summary.InspectedPass, summary.InspectedFail, summary.NotInspected, summary.IssuesReported));
            Assert.Equal(new DateOnly(2025, 11, 1), Assert.Single(await db.ScanRecords.ToListAsync()).ShiftDate);
            Assert.Empty(await db.InspectionRecords.ToListAsync());
            Assert.Equal(0, (await new RetentionRunner(db, factory.Clock, new()).RunAsync(default)).SummariesCreated);
            Assert.Equal(32, (await db.PointMonthlySummaries.SingleAsync(s => s.ServicePointId == ids.Point)).RoundsTotal);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_newer_submission_or_inspection_on_an_old_shift_defers_the_point_month(bool newerInspection)
    {
        using var factory = new FacilityApiFactory();
        var ids = await PrepareAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var scan = Scan(ids, 1);
            if (!newerInspection) scan.SubmittedAt = Now.UtcDateTime.AddYears(-1).AddTicks(1);
            db.ScanRecords.Add(scan);
            if (newerInspection) db.InspectionRecords.Add(new() { ScanRecord = scan, ServicePointId = ids.Point, SupervisorId = ids.Supervisor,
                Result = InspectionResult.Passed, InspectedAt = Now.UtcDateTime.AddYears(-1).AddTicks(1) });
            await db.SaveChangesAsync();
            await new RetentionRunner(db, factory.Clock, new()).RunAsync(default);
            Assert.False(await db.PointMonthlySummaries.AnyAsync(s => s.ServicePointId == ids.Point));
            Assert.True(await db.ScanRecords.AnyAsync(s => s.Id == scan.Id));
            factory.Clock.Advance(TimeSpan.FromDays(1));
            await new RetentionRunner(db, factory.Clock, new()).RunAsync(default);
            Assert.True(await db.PointMonthlySummaries.AnyAsync(s => s.ServicePointId == ids.Point));
            Assert.False(await db.ScanRecords.AnyAsync(s => s.Id == scan.Id));
        });
    }

    [Fact]
    public async Task Typed_dependents_same_numeric_id_tokens_and_referenced_covers_obey_their_own_policies()
    {
        using var factory = new FacilityApiFactory();
        var ids = await PrepareAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.SingleAsync(s => s.Id == ids.Sign);
            var cutoff = Now.UtcDateTime.AddYears(-1);
            var scan = Scan(ids, 1); scan.Id = 100; scan.ShiftDate = new(2025, 11, 1); scan.SubmittedAt = cutoff.AddTicks(1);
            db.ScanRecords.Add(scan);
            db.InspectionRecords.Add(new() { Id = 100, ScanRecord = scan, ServicePointId = ids.Point, SupervisorId = ids.Supervisor,
                Result = InspectionResult.Passed, InspectedAt = Now.UtcDateTime });
            db.ShiftAttendances.Add(new() { Id = 100, AreaId = sign.AreaId, UserId = ids.User, ShiftDate = new(2025, 10, 1), OccurredAt = cutoff });
            db.BlockedScans.Add(new() { Id = 100, UserId = ids.User, SignId = ids.Sign, ScannedAt = cutoff });
            foreach (var type in Enum.GetValues<AlertType>()) db.AlertReviews.Add(new() { RecordId = 100, AlertType = type, ReviewedById = ids.Supervisor });
            foreach (var type in new[] { "shift_attendances", "blocked_scans", "scan_records", "inspections", "users", "signs" })
                db.AuditLog.Add(new() { EntityType = type, EntityId = 100, Summary = "synthetic", Action = "TEST" });
            var removable = new CoverAssignment { UserId = ids.User, AreaId = sign.AreaId, AssignedById = ids.Supervisor,
                ShiftDate = new(2025, 10, 1), AssignedAt = cutoff.AddDays(-1) };
            var referenced = new CoverAssignment { UserId = ids.User, AreaId = sign.AreaId, AssignedById = ids.Supervisor,
                ShiftDate = new(2025, 10, 2), AssignedAt = cutoff.AddDays(-1) };
            db.CoverAssignments.AddRange(removable, referenced); scan.CoverAssignment = referenced;
            var grace = Now.UtcDateTime.AddDays(-30);
            db.RefreshTokens.AddRange(new RefreshToken { UserId = ids.User, TokenHash = "expired", ExpiresAt = grace },
                new RefreshToken { UserId = ids.User, TokenHash = "retain", ExpiresAt = grace.AddTicks(1) });
            await db.SaveChangesAsync();
            db.AuditLog.Add(new() { EntityType = "cover_assignments", EntityId = removable.Id, Summary = "synthetic", Action = "TEST" });
            await db.SaveChangesAsync();
            await new RetentionRunner(db, factory.Clock, new() { BatchSize = 1 }).RunAsync(default);
            Assert.Empty(await db.ShiftAttendances.ToListAsync()); Assert.Empty(await db.BlockedScans.ToListAsync());
            Assert.Equal(new[] { AlertType.InspectionGps, AlertType.SubmissionGps }, await db.AlertReviews.OrderBy(r => r.AlertType).Select(r => r.AlertType).ToArrayAsync());
            Assert.Equal(new[] { "inspections", "scan_records", "signs", "users" }, await db.AuditLog.OrderBy(a => a.EntityType).Select(a => a.EntityType).ToArrayAsync());
            Assert.Equal(referenced.Id, Assert.Single(await db.CoverAssignments.ToListAsync()).Id);
            Assert.Equal("retain", Assert.Single(await db.RefreshTokens.ToListAsync()).TokenHash);
            Assert.True(await db.Signs.AnyAsync(s => s.Id == ids.Sign)); Assert.True(await db.Users.AnyAsync(u => u.Id == ids.User));
        });
    }
}
