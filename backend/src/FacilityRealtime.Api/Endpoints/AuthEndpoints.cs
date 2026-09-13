using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FacilityRealtime.Api.Endpoints;

public static class AuthEndpoints
{
    public const string RefreshCookieName = "facility_refresh";

    /// <summary>ADR facility-0013: the browser sends the refresh cookie to these routes and nowhere else.</summary>
    private const string RefreshCookiePath = "/api/auth";

    /// <summary>Verified against a dummy hash when the account is unknown, so every failed login costs one PBKDF2 run.</summary>
    private static readonly string DummyPasswordHash =
        $"pbkdf2-sha256${Pbkdf2PasswordHasher.DefaultIterations}${Convert.ToBase64String(new byte[16])}${Convert.ToBase64String(new byte[32])}";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/refresh", RefreshAsync);
        auth.MapPost("/logout", LogoutAsync);
        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext http,
        AppDbContext db,
        IPasswordHasher hasher,
        IAccessTokenIssuer issuer,
        TimeProvider clock,
        IOptions<JwtSettings> jwt)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        var passwordMatches = hasher.Verify(request.Password, user?.PasswordHash ?? DummyPasswordHash);
        if (user is null || !user.IsActive || !passwordMatches)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var refresh = AddRefreshToken(db, user.Id, sessionId: Guid.NewGuid(), now);
        await db.SaveChangesAsync();

        WriteRefreshCookie(http, refresh.Token, refresh.ExpiresAt, jwt.Value);
        return Results.Ok(ToResponse(user, issuer.Issue(user)));
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext http,
        AppDbContext db,
        IAccessTokenIssuer issuer,
        TimeProvider clock,
        IOptions<JwtSettings> jwt)
    {
        var presented = http.Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(presented))
        {
            return Unauthorized(http, jwt.Value);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var hash = RefreshTokenRules.Hash(presented);
        var stored = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored?.User is null)
        {
            return Unauthorized(http, jwt.Value);
        }

        switch (RefreshTokenRules.Decide(stored, now))
        {
            case RefreshDecision.Reject:
                return Unauthorized(http, jwt.Value);

            case RefreshDecision.ReuseDetected:
                // ADR facility-0015: two parties hold this token and the server cannot tell which is the owner
                await db.RevokeSessionAsync(stored.SessionId, now);
                return Unauthorized(http, jwt.Value);
        }

        // ADR facility-0012: the account is re-read on every refresh, so deactivation and role changes land within 5 minutes
        if (!stored.User.IsActive)
        {
            await db.RevokeUserSessionsAsync(stored.UserId, now);
            return Unauthorized(http, jwt.Value);
        }

        // Rotate (facility-0014). Within the grace window the first rotation time is kept, so the window never extends itself.
        stored.RotatedAt ??= now;
        var next = AddRefreshToken(db, stored.UserId, stored.SessionId, now);
        await db.SaveChangesAsync();

        WriteRefreshCookie(http, next.Token, next.ExpiresAt, jwt.Value);
        return Results.Ok(ToResponse(stored.User, issuer.Issue(stored.User)));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext http,
        AppDbContext db,
        TimeProvider clock,
        IOptions<JwtSettings> jwt)
    {
        var presented = http.Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(presented))
        {
            var hash = RefreshTokenRules.Hash(presented);
            var sessionId = await db.RefreshTokens
                .Where(t => t.TokenHash == hash)
                .Select(t => (Guid?)t.SessionId)
                .FirstOrDefaultAsync();

            if (sessionId is not null)
            {
                await db.RevokeSessionAsync(sessionId.Value, clock.GetUtcNow().UtcDateTime);
            }
        }

        http.Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(jwt.Value));
        return Results.NoContent();
    }

    private static IResult Unauthorized(HttpContext http, JwtSettings settings)
    {
        http.Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(settings));
        return Results.Unauthorized();
    }

    private static (string Token, DateTime ExpiresAt) AddRefreshToken(AppDbContext db, int userId, Guid sessionId, DateTime nowUtc)
    {
        var token = RefreshTokenRules.NewToken();
        var expiresAt = nowUtc + RefreshTokenRules.Lifetime;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            SessionId = sessionId,
            TokenHash = RefreshTokenRules.Hash(token),
            CreatedAt = nowUtc,
            ExpiresAt = expiresAt,
        });

        return (token, expiresAt);
    }

    private static CookieOptions RefreshCookieOptions(JwtSettings settings) => new()
    {
        HttpOnly = true,
        Secure = settings.RefreshCookieSecure,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
    };

    private static void WriteRefreshCookie(HttpContext http, string token, DateTime expiresAtUtc, JwtSettings settings)
    {
        var options = RefreshCookieOptions(settings);
        options.Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc));
        http.Response.Cookies.Append(RefreshCookieName, token, options);
    }

    private static AuthResponse ToResponse(User user, AccessToken access) =>
        new(access.Token, access.ExpiresAtUtc, new AuthUserDto(user.Id, user.Username, user.FullName, user.Role));
}
