using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace JanitorialManagementDashboard.Api.Hubs;

/// <summary>
/// Server-to-client only. "ScanRecorded", "InspectionRecorded" and "AttendanceRecorded" go to Admin Accounts (facility-0058,
/// 0027); the first two also go to the Supervisors of the point's building, addressed by account id (facility-0057,
/// PointNotifications); since 2026-10-26 they also go to the Cleaners of the point's Area, so My Work updates by itself.
/// "OverviewChanged" carries the committed target revision ID and goes only to active Admins.
/// Every push is addressed to the active accounts read at send time; connect/negotiate also checks active account and
/// session in <see cref="AuthSetup"/>, and open connections are aborted immediately via <see cref="ScanHubConnections"/>
/// when a session is logged out or an account is deactivated or has its secret changed.
/// </summary>
[Authorize]
public class ScanHub(ScanHubConnections connections) : Hub
{
    public override Task OnConnectedAsync()
    {
        if (!int.TryParse(Context.User?.FindFirstValue(AuthClaims.UserId), out var userId))
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        Guid? sessionId = Guid.TryParse(Context.User?.FindFirstValue(AuthClaims.SessionId), out var parsed)
            ? parsed
            : null;
        connections.Register(userId, sessionId, Context);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Unregister(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
