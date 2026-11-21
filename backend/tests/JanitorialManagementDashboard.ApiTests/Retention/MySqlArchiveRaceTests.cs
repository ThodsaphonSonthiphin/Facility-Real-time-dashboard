using System.Data.Common;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySql.Data.MySqlClient;

namespace JanitorialManagementDashboard.ApiTests.Retention;

[Collection("MySQL")]
public class MySqlArchiveRaceTests
{
    [MySqlFact]
    public Task Inspection_attempted_after_archive_reads_cannot_commit_between_summary_and_delete() =>
        WithFixtureAsync(async (options, source, supervisorId, clock) =>
        {
            await using var writer = new AppDbContext(options);
            await writer.Database.OpenConnectionAsync();
            await writer.Database.ExecuteSqlRawAsync("SET SESSION innodb_lock_wait_timeout = 1");
            var inspection = FreshInspection(source, supervisorId, clock);
            var inserted = false;
            var deferred = false;
            var beforeInsert = new BeforeSummaryInsert(async ct =>
            {
                writer.InspectionRecords.Add(inspection);
                try
                {
                    await writer.SaveChangesAsync(ct);
                    inserted = true;
                }
                catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1205 })
                {
                    deferred = true;
                }
            });
            await using var archive = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(options)
                .AddInterceptors(beforeInsert).Options);
            try
            {
                await new RetentionRunner(archive, clock, new()).RunAsync(default);
            }
            finally
            {
                await writer.Database.ExecuteSqlRawAsync("SET SESSION innodb_lock_wait_timeout = DEFAULT");
            }

            // On the unsafe isolation-only variant the writer returns successfully, then its row disappears.
            if (inserted)
                Assert.True(await archive.InspectionRecords.AnyAsync(i => i.Id == inspection.Id),
                    "An ineligible inspection committed during archive was deleted.");
            Assert.True(deferred, "An inspection attempted after the archive reads should wait for the archive transaction.");
            Assert.Single(await archive.PointMonthlySummaries.ToListAsync());
            Assert.Empty(await archive.ScanRecords.ToListAsync());
        });

    [MySqlFact]
    public Task Inspection_committed_after_archive_transaction_starts_defers_the_whole_month() =>
        WithFixtureAsync(async (options, source, supervisorId, clock) =>
        {
            var inspection = FreshInspection(source, supervisorId, clock);
            var afterBegin = new AfterFirstTransactionStarted(async ct =>
            {
                await using var writer = new AppDbContext(options);
                writer.InspectionRecords.Add(inspection);
                await writer.SaveChangesAsync(ct);
            });
            await using var archive = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(options)
                .AddInterceptors(afterBegin).Options);
            var runner = new RetentionRunner(archive, clock, new());
            Assert.Equal(0, (await runner.RunAsync(default)).SummariesCreated);
            Assert.Equal(inspection.Id, (await archive.InspectionRecords.SingleAsync()).Id);
            Assert.Equal(source.Id, (await archive.ScanRecords.SingleAsync()).Id);
            Assert.Single(await archive.AuditLog.ToListAsync());

            clock.Advance(TimeSpan.FromDays(2));
            Assert.Equal(1, (await runner.RunAsync(default)).SummariesCreated);
            Assert.Empty(await archive.InspectionRecords.ToListAsync());
            Assert.Empty(await archive.ScanRecords.ToListAsync());
            Assert.Empty(await archive.AuditLog.ToListAsync());
            Assert.Equal(1, (await archive.PointMonthlySummaries.SingleAsync()).InspectedPass);
            Assert.Equal(0, (await runner.RunAsync(default)).SummariesCreated);
        });

    [MySqlFact]
    public Task Archive_commit_failure_restores_summary_inputs_and_audit_and_a_new_run_archives_once() =>
        WithFixtureAsync(async (options, source, supervisorId, clock) =>
        {
            await using var writer = new AppDbContext(options);
            var inspection = FreshInspection(source, supervisorId, clock);
            inspection.InspectedAt = source.SubmittedAt.AddMinutes(1);
            writer.InspectionRecords.Add(inspection);
            await writer.SaveChangesAsync();

            var failCommit = new FailBeforeArchiveCommit();
            await using (var archive = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(options)
                .AddInterceptors(failCommit).Options))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    new RetentionRunner(archive, clock, new()).RunAsync(default));
            }

            await using var retry = new AppDbContext(options);
            Assert.Empty(await retry.PointMonthlySummaries.ToListAsync());
            Assert.Equal(source.Id, (await retry.ScanRecords.SingleAsync()).Id);
            Assert.Equal(inspection.Id, (await retry.InspectionRecords.SingleAsync()).Id);
            Assert.Single(await retry.AuditLog.ToListAsync());

            var runner = new RetentionRunner(retry, clock, new());
            Assert.Equal(1, (await runner.RunAsync(default)).SummariesCreated);
            Assert.Equal(0, (await runner.RunAsync(default)).SummariesCreated);
            Assert.Empty(await retry.ScanRecords.ToListAsync());
            Assert.Empty(await retry.InspectionRecords.ToListAsync());
            Assert.Empty(await retry.AuditLog.ToListAsync());
            Assert.Equal(1, (await retry.PointMonthlySummaries.SingleAsync()).InspectedPass);
        });

    private static InspectionRecord FreshInspection(ScanRecord source, int supervisorId, TestTimeProvider clock) =>
        new()
        {
            ScanRecordId = source.Id, ServicePointId = source.ServicePointId, SupervisorId = supervisorId,
            Result = InspectionResult.Passed, InspectedAt = clock.GetUtcNow().UtcDateTime.AddYears(-1).AddDays(1),
        };

    private static async Task WithFixtureAsync(
        Func<DbContextOptions<AppDbContext>, ScanRecord, int, TestTimeProvider, Task> test)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.MigrateAsync();
            await DbInitializer.SeedAsync(db, new Pbkdf2PasswordHasher(iterations: 1_000));
            var sign = await db.Signs.FirstAsync(s => s.ServicePointId != null);
            var cleaner = await db.Users.FirstAsync(u => u.Role == UserRole.Cleaner);
            var supervisor = await db.Users.FirstAsync(u => u.Role == UserRole.Supervisor);
            var source = new ScanRecord
            {
                ServicePointId = sign.ServicePointId!.Value, SignId = sign.Id, UserId = cleaner.Id,
                ShiftDate = new(2025, 10, 1), SubmittedAt = new(2025, 10, 1, 1, 0, 0),
            };
            db.ScanRecords.Add(source);
            await db.SaveChangesAsync();
            db.AuditLog.Add(new() { EntityType = "scan_records", EntityId = source.Id, Action = "TEST", Summary = "synthetic" });
            await db.SaveChangesAsync();

            var clock = new TestTimeProvider(new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero));
            await test(options, source, supervisor.Id, clock);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class BeforeSummaryInsert(Func<CancellationToken, Task> beforeInsert) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<PointMonthlySummary>().Any(entry => entry.State == EntityState.Added))
                await beforeInsert(cancellationToken);
            return result;
        }
    }

    private sealed class FailBeforeArchiveCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<PointMonthlySummary>().Any())
                throw new InvalidOperationException("Synthetic archive commit failure");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AfterFirstTransactionStarted(Func<CancellationToken, Task> afterBegin) : DbTransactionInterceptor
    {
        private bool started;

        public override async ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection,
            TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
        {
            if (!started)
            {
                started = true;
                await afterBegin(cancellationToken);
            }
            return result;
        }
    }
}
