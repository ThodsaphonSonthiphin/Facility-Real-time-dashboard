using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using Microsoft.AspNetCore.SignalR;

namespace JanitorialManagementDashboard.Api.Endpoints;

internal sealed class OverviewNotifications(IHubContext<ScanHub> hub, AppDbContext db, ILogger<OverviewNotifications> logger)
{
    public async Task CommitAsync(OverviewMutationLease change, CancellationToken ct)
    {
        await change.CommitAsync(ct);
        await change.DisposeAsync();
        if (change.CommittedTargetChangeId is { } revisionId)
            await SendAsync(hub, db, logger, revisionId);
    }

    public static async Task SendAsync(IHubContext<ScanHub> hub, AppDbContext db, ILogger logger, long revisionId)
    {
        try
        {
            await hub.Clients.Users(await ActiveAdmins.IdsAsync(db))
                .SendAsync("OverviewChanged", new { RevisionId = revisionId });
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Overview revision {RevisionId} is saved; notification failed", revisionId);
        }
    }
}
