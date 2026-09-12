using System.Threading.Tasks;
using FacilityRealtime.Api.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace FacilityRealtime.Api.Hubs;

public class ScanHub : Hub
{
    // Clients connect to /hubs/scan and listen for "ScanRecorded" events
    public async Task NotifyPointUpdated(ServicePointStatusDto point)
    {
        await Clients.All.SendAsync("ScanRecorded", point);
    }
}
