namespace FacilityRealtime.Api.Auth;

/// <summary>Claim names inside the access token. Inbound claim mapping is off, so these are also what handlers read.</summary>
public static class AuthClaims
{
    public const string UserId = "sub";
    public const string Username = "preferred_username";
    public const string Name = "name";
    public const string Role = "role";
}
