using FacilityRealtime.Api.Auth;
using Microsoft.AspNetCore.SignalR;

namespace FacilityRealtime.Api.Hubs;

/// <summary>Clients.User(id) addresses the account id in the access token's "sub" claim (inbound claim mapping is off).</summary>
public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst(AuthClaims.UserId)?.Value;
}
