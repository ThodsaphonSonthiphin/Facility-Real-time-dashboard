using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Infrastructure;

internal static class OverviewTestData
{
    public static async Task<long> StartHistoryAsync(JanitorialApiFactory factory, DateTimeOffset at)
    {
        factory.Clock.SetUtcNow(at);
        long revisionId = 0;
        await factory.WithDbAsync(async db =>
        {
            if (await db.OverviewTargetRevisions.AnyAsync())
                throw new InvalidOperationException("History already started in this fixture");
            var instant = at.UtcDateTime;
            await db.Buildings.Where(x => x.CreatedAt > instant).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, instant));
            await db.Areas.Where(x => x.CreatedAt > instant).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, instant));
            await db.ServicePoints.Where(x => x.CreatedAt > instant).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, instant));
            await db.PointRoundWindows.Where(x => x.CreatedAt > instant).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, instant));
            revisionId = await OverviewHistoryBootstrap.EnsureAsync(db,
                factory.Services.GetRequiredService<AttendanceSettings>(), factory.Clock, CancellationToken.None);
        });
        return revisionId;
    }
}
