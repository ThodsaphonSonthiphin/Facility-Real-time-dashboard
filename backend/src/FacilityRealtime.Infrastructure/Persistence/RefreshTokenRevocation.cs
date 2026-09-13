using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

public static class RefreshTokenRevocation
{
    /// <summary>Revokes every still-valid token of one login: logout and reuse detection (ADR facility-0014, facility-0015).</summary>
    public static Task<int> RevokeSessionAsync(this AppDbContext db, Guid sessionId, DateTime nowUtc) =>
        db.RefreshTokens
            .Where(t => t.SessionId == sessionId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, (DateTime?)nowUtc));

    /// <summary>Revokes every still-valid token of a user: deactivation and password reset (ADR facility-0014).</summary>
    public static Task<int> RevokeUserSessionsAsync(this AppDbContext db, int userId, DateTime nowUtc) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, (DateTime?)nowUtc));
}
