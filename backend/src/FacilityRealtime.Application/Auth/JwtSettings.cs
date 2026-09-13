namespace FacilityRealtime.Application.Auth;

/// <summary>The "Jwt" config section. SigningKey is never committed: set it with dotnet user-secrets (ADR facility-0011).</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "facility-realtime";
    public string Audience { get; set; } = "facility-realtime-web";
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>ADR facility-0012.</summary>
    public int AccessTokenMinutes { get; set; } = 5;

    /// <summary>ADR facility-0013: false in the POC because phones use plain HTTP; true on any HTTPS deployment.</summary>
    public bool RefreshCookieSecure { get; set; }
}
