using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace JanitorialManagementDashboard.Api.Hubs;

/// <summary>
/// Tracks live <see cref="ScanHub"/> connections by user and login session so logout, deactivation, phone change,
/// password reset, or refresh-token reuse immediately aborts affected open connections rather than waiting for the
/// client to disconnect on its own.
/// </summary>
public sealed class ScanHubConnections
{
    private sealed record TrackedConnection(int UserId, Guid? SessionId, HubCallerContext Context);

    private readonly ConcurrentDictionary<string, TrackedConnection> _connections = new(StringComparer.Ordinal);

    public void Register(int userId, Guid? sessionId, HubCallerContext context)
    {
        _connections[context.ConnectionId] = new TrackedConnection(userId, sessionId, context);
    }

    public void Unregister(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    /// <summary>Immediately aborts every live hub connection held by <paramref name="userId"/>.</summary>
    public void DisconnectUser(int userId)
    {
        foreach (var (connectionId, tracked) in _connections)
        {
            if (tracked.UserId == userId && _connections.TryRemove(connectionId, out var removed))
            {
                removed.Context.Abort();
            }
        }
    }

    /// <summary>Immediately aborts every live hub connection tied to <paramref name="sessionId"/>.</summary>
    public void DisconnectSession(Guid sessionId)
    {
        foreach (var (connectionId, tracked) in _connections)
        {
            if (tracked.SessionId == sessionId && _connections.TryRemove(connectionId, out var removed))
            {
                removed.Context.Abort();
            }
        }
    }
}
