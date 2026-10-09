using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
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

    private const int MaxLoginNameLength = 100; // users.username, the longest login column

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
        LoginThrottle throttle,
        TimeProvider clock,
        IOptions<JwtSettings> jwt)
    {
        // facility-0054: Cleaners and Supervisors use employee ID + phone; the Admin keeps username + password
        var byEmployeeId = !string.IsNullOrWhiteSpace(request.EmployeeId);
        var loginName = (byEmployeeId ? request.EmployeeId : request.Username)?.Trim() ?? string.Empty;
        var secret = byEmployeeId ? PhoneNumber.Normalize(request.Phone) : request.Password ?? string.Empty;
        if (loginName.Length > MaxLoginNameLength)
        {
            // Longer than any login column, so no account can match; nothing is counted or kept in memory
            return Results.Unauthorized();
        }

        var user = byEmployeeId
            ? await db.Users.FirstOrDefaultAsync(u => u.EmployeeId == loginName && u.Role != UserRole.Admin)
            : await db.Users.FirstOrDefaultAsync(u => u.Username == loginName && u.Role == UserRole.Admin);

        // Count per account when the name matches one, so spellings the database collation folds together
        // (case, accents) share one counter; per typed name otherwise, so unknown names lock the same way
        var throttleKey = user is not null
            ? $"user:{user.Id}"
            : (byEmployeeId ? "employee:" : "admin:") + loginName;
        if (!throttle.TryBeginAttempt(throttleKey))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var secretMatches = hasher.Verify(secret, user?.SecretHash ?? DummyPasswordHash);
        if (user is null || !user.IsActive || !secretMatches)
        {
            return Results.Unauthorized();
        }

        throttle.Reset(throttleKey);

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
        new(access.Token, access.ExpiresAtUtc, ToUserDto(user));

    internal static AuthUserDto ToUserDto(User user) => new(user.Id, user.LoginName, user.DisplayName, user.Role.ToClaimValue());
}
