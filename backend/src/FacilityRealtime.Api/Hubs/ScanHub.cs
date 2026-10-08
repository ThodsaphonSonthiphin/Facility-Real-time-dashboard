using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FacilityRealtime.Api.Hubs;

/// <summary>Server-to-client only. The old NotifyPointUpdated method let any logged-in client broadcast to everyone, so it is gone.</summary>
[Authorize]
public class ScanHub : Hub
{
}
