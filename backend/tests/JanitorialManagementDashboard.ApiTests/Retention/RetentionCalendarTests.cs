using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Retention;

public class RetentionCalendarTests
{
    [Theory]
    [InlineData("2025-03-01T00:00:00Z", "2024-02-01", 29)]
    [InlineData("2027-01-01T00:00:00Z", "2025-12-01", 31)]
    public async Task Missing_rounds_cover_complete_leap_and_year_end_months(string now, string monthText, int days)
    {
        using var factory = new JanitorialApiFactory();
        var ids = await AnnualRetentionTests.PrepareAsync(factory);
        var month = DateOnly.Parse(monthText, System.Globalization.CultureInfo.InvariantCulture);
        factory.Clock.SetUtcNow(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture));
        await factory.WithDbAsync(async db =>
        {
            var point = await db.ServicePoints.SingleAsync(p => p.Id == ids.Point);
            var window = await db.PointRoundWindows.SingleAsync(w => w.ServicePointId == ids.Point);
            point.CreatedAt = month.ToDateTime(TimeOnly.MinValue);
            window.CreatedAt = point.CreatedAt;
            await db.SaveChangesAsync();
            await new RetentionRunner(db, factory.Clock, new()).RunAsync(default);
            var summary = Assert.Single(await db.PointMonthlySummaries.Where(s => s.ServicePointId == ids.Point).ToListAsync());
            Assert.Equal(month, summary.Month);
            Assert.Equal((days, days), (summary.RoundsTotal, summary.NotDone));
        });
    }

    [Fact]
    public async Task Thai_month_boundary_and_CreatedAt_guard_prevent_early_archive_and_invented_rounds()
    {
        using var factory = new JanitorialApiFactory();
        var ids = await AnnualRetentionTests.PrepareAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var window = await db.PointRoundWindows.SingleAsync(w => w.ServicePointId == ids.Point);
            window.CreatedAt = new(2025, 10, 16, 0, 0, 1, DateTimeKind.Utc);
            await db.SaveChangesAsync();
            factory.Clock.SetUtcNow(DateTimeOffset.Parse("2026-10-31T16:59:59Z"));
            await new RetentionRunner(db, factory.Clock, new()).RunAsync(default);
            Assert.False(await db.PointMonthlySummaries.AnyAsync(s => s.ServicePointId == ids.Point));
            factory.Clock.SetUtcNow(DateTimeOffset.Parse("2026-10-31T17:00:00Z"));
            await new RetentionRunner(db, factory.Clock, new()).RunAsync(default);
            var summary = await db.PointMonthlySummaries.SingleAsync(s => s.ServicePointId == ids.Point);
            Assert.Equal(15, summary.RoundsTotal); // Oct 17-31: Oct 16 window was created one second after its start.
        });
    }

    [Fact]
    public async Task Disabled_runner_and_cancellation_do_not_mutate_data()
    {
        using var factory = new JanitorialApiFactory();
        var ids = await AnnualRetentionTests.PrepareAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            db.ScanRecords.Add(AnnualRetentionTests.Scan(ids, 1)); await db.SaveChangesAsync();
            var result = await new RetentionRunner(db, factory.Clock, new() { Enabled = false }).RunAsync(default);
            Assert.Equal(0, result.RecordsDeleted);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RetentionRunner(db, factory.Clock, new()).RunAsync(cancelled.Token));
            Assert.Single(await db.ScanRecords.ToListAsync()); Assert.Empty(await db.PointMonthlySummaries.ToListAsync());
        });
    }
}
