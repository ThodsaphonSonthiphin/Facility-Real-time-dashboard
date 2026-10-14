using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FacilityRealtime.Api.Hubs;

/// <summary>
/// Server-to-client only. "ScanRecorded", "InspectionRecorded" and "AttendanceRecorded" go to Admin Accounts (facility-0058,
/// 0027); the first two also go to the Supervisors of the point's building, addressed by account id (facility-0057,
/// PointNotifications). Cleaners get nothing live.
/// </summary>
[Authorize]
public class ScanHub : Hub
{
    public const string AdminGroup = "admins";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        }

        await base.OnConnectedAsync();
    }
}
