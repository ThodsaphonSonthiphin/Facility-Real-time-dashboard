using System;

namespace FacilityRealtime.Domain.Entities;

/// <summary>
/// One row per refresh token ever issued (ADR facility-0014). Only the SHA-256 hash of the token is stored.
/// Every token rotated out of one login shares that login's SessionId (facility-0015).
/// </summary>
public class RefreshToken
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public Guid SessionId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>CreatedAt + 30 days; each rotation issues a new row, which is what makes the lifetime sliding (facility-0016).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When this token was first exchanged for a successor. Null while unused.</summary>
    public DateTime? RotatedAt { get; set; }

    /// <summary>Set by logout, reuse detection, deactivation or password reset.</summary>
    public DateTime? RevokedAt { get; set; }

    public User? User { get; set; }
}
