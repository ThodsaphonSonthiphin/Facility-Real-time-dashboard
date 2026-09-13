using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using FacilityRealtime.Domain.Entities;

namespace FacilityRealtime.Application.Auth;

public enum RefreshDecision
{
    /// <summary>First use: mark it rotated and issue a successor.</summary>
    Rotate,

    /// <summary>Already rotated at most 30 seconds ago: another tab refreshed at the same time, issue a successor too.</summary>
    RotateWithinGrace,

    /// <summary>Expired or revoked: refuse, change nothing.</summary>
    Reject,

    /// <summary>Already rotated more than 30 seconds ago: two parties hold it, revoke the whole login.</summary>
    ReuseDetected,
}

public static class RefreshTokenRules
{
    /// <summary>ADR facility-0016: 30 days from last use. Every rotation issues a fresh token with a fresh 30 days.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>ADR facility-0015: a rotated token replayed within this window is a concurrent refresh, not theft.</summary>
    public static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

    public static RefreshDecision Decide(RefreshToken token, DateTime nowUtc)
    {
        if (token.RevokedAt is not null || nowUtc >= token.ExpiresAt)
        {
            return RefreshDecision.Reject;
        }

        if (token.RotatedAt is null)
        {
            return RefreshDecision.Rotate;
        }

        return nowUtc - token.RotatedAt.Value <= ReuseGrace
            ? RefreshDecision.RotateWithinGrace
            : RefreshDecision.ReuseDetected;
    }

    /// <summary>256 random bits (ADR facility-0014), base64url so it needs no escaping in a cookie.</summary>
    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>What the database stores. A plain SHA-256 is enough for a 256-bit random value; no salt or stretching needed.</summary>
    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
