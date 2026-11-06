using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace JanitorialManagementDashboard.Api.Hubs;

/// <summary>
/// Server-to-client only. "ScanRecorded", "InspectionRecorded" and "AttendanceRecorded" go to Admin Accounts (facility-0058,
/// 0027); the first two also go to the Supervisors of the point's building, addressed by account id (facility-0057,
/// PointNotifications); since 2026-10-26 they also go to the Cleaners of the point's Area, so My Work updates by itself.
/// Every push is addressed to the active accounts read at send time, never to a group joined at connect: the token is
/// checked only when the connection starts, so a group would keep a deactivated account's open page fed (facility-0105).
/// </summary>
[Authorize]
public class ScanHub : Hub;
