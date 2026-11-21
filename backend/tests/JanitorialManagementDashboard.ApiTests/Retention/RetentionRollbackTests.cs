using System.Data.Common;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace JanitorialManagementDashboard.ApiTests.Retention;

public class RetentionRollbackTests
{
    [Fact]
    public async Task Failure_after_summary_insert_rolls_back_marker_dependents_and_sources_and_fresh_scope_retry_succeeds()
    {
        const string privateDetail = "Synthetic private database detail";
        var failure = new FailSourceDelete(privateDetail);
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
            var logger = factory.Services.GetRequiredService<ILogger<RetentionRunner>>();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new RetentionRunner(db, factory.Clock, new(), logger).RunAsync(default));
            var diagnostic = Assert.Single(factory.Logs.Lines, line => line.Category == typeof(RetentionRunner).FullName);
            Assert.Equal(LogEventLevel.Warning, diagnostic.Level);
            Assert.Contains("ArchiveMonthlyWork", diagnostic.Message);
            Assert.Contains(nameof(InvalidOperationException), diagnostic.Message);
            Assert.DoesNotContain(privateDetail, diagnostic.ConsoleText);
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

    private sealed class FailSourceDelete(string privateDetail) : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.StartsWith("DELETE FROM \"scan_records\"", StringComparison.Ordinal))
                throw new InvalidOperationException(privateDetail);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
