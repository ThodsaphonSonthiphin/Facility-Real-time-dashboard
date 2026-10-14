using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FacilityRealtime.Api.Hubs;

/// <summary>Server-to-client only. "ScanRecorded" and "AttendanceRecorded" go to Admin Accounts only (facility-0058, 0027).</summary>
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
