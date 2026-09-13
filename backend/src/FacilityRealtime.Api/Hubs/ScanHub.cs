using System.Threading.Tasks;
using FacilityRealtime.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FacilityRealtime.Api.Hubs;

/// <summary>ADR facility-0018: only logged-in accounts receive the live feed, so Clients.All needs no groups.</summary>
[Authorize]
public class ScanHub : Hub
{
    // Clients connect to /hubs/scan and listen for "ScanRecorded" events
    public async Task NotifyPointUpdated(ServicePointStatusDto point)
    {
        await Clients.All.SendAsync("ScanRecorded", point);
    }
}
