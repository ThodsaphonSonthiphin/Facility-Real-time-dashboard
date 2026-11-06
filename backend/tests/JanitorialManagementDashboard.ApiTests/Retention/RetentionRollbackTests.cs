using Microsoft.Extensions.DependencyInjection;

using System.Data.Common;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JanitorialManagementDashboard.ApiTests.Retention;

public class RetentionRollbackTests
{
    [Fact]
    public async Task Failure_after_summary_insert_rolls_back_marker_dependents_and_sources_and_fresh_scope_retry_succeeds()
    {
        var failure = new FailSourceDelete();
        using var factory = new JanitorialApiFactory
        {
            ConfigureTestServices = services => services.AddDbContext<JanitorialManagementDashboard.Infrastructure.Persistence.AppDbContext>(o => o.AddInterceptors(failure)),
        };
        var ids = await AnnualRetentionTests.PrepareAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var scan = AnnualRetentionTests.Scan(ids, 1);
            db.ScanRecords.Add(scan);
            await db.SaveChangesAsync();
            db.AuditLog.Add(new() { EntityType = "scan_records", EntityId = scan.Id, Action = "TEST", Summary = "synthetic" });
            await db.SaveChangesAsync();
            failure.Enabled = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => new RetentionRunner(db, factory.Clock, new()).RunAsync(default));
        });
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.PointMonthlySummaries.ToListAsync());
            Assert.Single(await db.ScanRecords.ToListAsync());
            Assert.Single(await db.AuditLog.ToListAsync());
            failure.Enabled = false;
            var result = await new RetentionRunner(db, factory.Clock, new()).RunAsync(default);
            Assert.Equal(1, result.SummariesCreated);
            Assert.Empty(await db.ScanRecords.ToListAsync());
            Assert.Empty(await db.AuditLog.ToListAsync());
        });
    }

    private sealed class FailSourceDelete : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.StartsWith("DELETE FROM \"scan_records\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Synthetic source deletion failure");
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
