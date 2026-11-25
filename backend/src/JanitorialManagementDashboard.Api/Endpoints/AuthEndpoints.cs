using System.Data;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.Application.Auth;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JanitorialManagementDashboard.Api.Endpoints;

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
        IOptions<JwtSettings> jwt,
        SecurityLog securityLog)
    {
        // facility-0054: Cleaners and Supervisors use employee ID + phone; the Admin keeps username + password
        var byEmployeeId = !string.IsNullOrWhiteSpace(request.EmployeeId);
        var loginName = (byEmployeeId ? request.EmployeeId : request.Username)?.Trim() ?? string.Empty;
        var secret = byEmployeeId ? PhoneNumber.Normalize(request.Phone) : request.Password ?? string.Empty;
        var place = LoginPlace.Of(http.Connection.RemoteIpAddress);
        if (loginName.Length > MaxLoginNameLength)
        {
            // Longer than any login column, so no account can match and no secret is checked; nothing is counted or
            // kept in memory, not even toward the place's cap
            securityLog.LoginFailedUnknownAccount(byEmployeeId, place);
            return Results.Unauthorized();
        }

        var user = byEmployeeId
            ? await db.Users.FirstOrDefaultAsync(u => u.EmployeeId == loginName && u.Role != UserRole.Admin)
            : await db.Users.FirstOrDefaultAsync(u => u.Username == loginName && u.Role == UserRole.Admin);

        // Count per account when the name matches one, so spellings the database collation folds together
        // (case, accents) share one counter; per typed name otherwise, so unknown names lock the same way.
        // facility-0104: the Login Lock is per login name and place, so nobody can lock someone else out.
        var throttleKey = user is not null
            ? $"user:{user.Id}"
            : (byEmployeeId ? "employee:" : "admin:") + loginName;
        var gate = throttle.TryBeginAttempt(throttleKey, place);
        if (gate != LoginGate.Allowed)
        {
            // facility-0104 decision 8: the same 429 whichever lock it is. The journal writes one line when a place
            // reaches its cap; each later refusal there is written like a locked name, naming whoever tried
            if (gate == LoginGate.PlaceLockedNow)
            {
                securityLog.LoginPlaceLocked(place);
            }
            else
            {
                securityLog.LoginRefusedWhileLocked(user, byEmployeeId, place);
            }

            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var secretMatches = hasher.Verify(secret, user?.SecretHash ?? DummyPasswordHash);
        if (user is null)
        {
            throttle.Failed(place);
            securityLog.LoginFailedUnknownAccount(byEmployeeId, place);
            return Results.Unauthorized();
        }

        if (!user.IsActive || !secretMatches)
        {
            // facility-0097: the journal tells the two apart; the phone still gets the same 401 for both
            throttle.Failed(place);
            securityLog.LoginFailed(user, secretMatches, place);
            return Results.Unauthorized();
        }

        throttle.Succeeded(throttleKey, place);

        var now = clock.GetUtcNow().UtcDateTime;
        var sessionId = Guid.NewGuid();
        var refresh = AddRefreshToken(db, user.Id, sessionId, now);
        await db.SaveChangesAsync();

        WriteRefreshCookie(http, refresh.Token, refresh.ExpiresAt, jwt.Value);
        return Results.Ok(ToResponse(user, issuer.Issue(user, sessionId)));
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext http,
        AppDbContext db,
        IAccessTokenIssuer issuer,
        TimeProvider clock,
        IOptions<JwtSettings> jwt,
        SecurityLog securityLog,
        ScanHubConnections hubConnections,
        CancellationToken ct)
    {
        var presented = http.Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(presented))
        {
            return Unauthorized(http, jwt.Value);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var hash = RefreshTokenRules.Hash(presented);
        var target = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash)
            .Select(t => new { t.Id, t.UserId, t.SessionId })
            .FirstOrDefaultAsync(ct);
        if (target is null)
        {
            return Unauthorized(http, jwt.Value);
        }

        var isSqlite = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        await using var transaction = await db.Database.BeginTransactionAsync(
            isSqlite ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct);

        // Lock users first, then refresh_tokens — matching SetActiveAsync, ReplaceSecretAsync, and LogoutAsync so
        // concurrent revocation can never deadlock or interleave with token rotation.
        User? user;
        RefreshToken? stored;
        if (isSqlite)
        {
            user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == target.UserId, ct);
            stored = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == target.Id, ct);
        }
        else
        {
            user = (await db.Users
                .FromSqlInterpolated($"SELECT * FROM users WHERE id = {target.UserId} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(ct)).FirstOrDefault();
            stored = (await db.RefreshTokens
                .FromSqlInterpolated($"SELECT * FROM refresh_tokens WHERE id = {target.Id} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(ct)).FirstOrDefault();
        }

        if (user is null || stored is null)
        {
            await transaction.RollbackAsync(ct);
            return Unauthorized(http, jwt.Value);
        }

        switch (RefreshTokenRules.Decide(stored, now))
        {
            case RefreshDecision.Reject:
                await transaction.RollbackAsync(ct);
                return Unauthorized(http, jwt.Value);

            case RefreshDecision.ReuseDetected:
                // ADR facility-0015: two parties hold this token and the server cannot tell which is the owner
                // written first, so the event is on record even if ending the session fails
                securityLog.RefreshTokenReused(user);
                await db.RevokeSessionAsync(stored.SessionId, now);
                await transaction.CommitAsync(ct);
                hubConnections.DisconnectSession(stored.SessionId);
                return Unauthorized(http, jwt.Value);
        }

        // ADR facility-0012: the account is re-read on every refresh, so deactivation and role changes land within 5 minutes
        if (!user.IsActive)
        {
            await db.RevokeUserSessionsAsync(stored.UserId, now);
            await transaction.CommitAsync(ct);
            hubConnections.DisconnectUser(stored.UserId);
            return Unauthorized(http, jwt.Value);
        }

        // Rotate (facility-0014) only while the token is still unrevoked. Within the grace window the first rotation time
        // is kept, so the window never extends itself.
        var rotatedAt = stored.RotatedAt ?? now;
        var updated = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RotatedAt, (DateTime?)rotatedAt), ct);
        if (updated == 0)
        {
            await transaction.RollbackAsync(ct);
            return Unauthorized(http, jwt.Value);
        }

        var next = AddRefreshToken(db, stored.UserId, stored.SessionId, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        WriteRefreshCookie(http, next.Token, next.ExpiresAt, jwt.Value);
        return Results.Ok(ToResponse(user, issuer.Issue(user, stored.SessionId)));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext http,
        AppDbContext db,
        TimeProvider clock,
        IOptions<JwtSettings> jwt,
        ScanHubConnections hubConnections,
        CancellationToken ct)
    {
        var presented = http.Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(presented))
        {
            var hash = RefreshTokenRules.Hash(presented);
            var tokenInfo = await db.RefreshTokens.AsNoTracking()
                .Where(t => t.TokenHash == hash)
                .Select(t => new { t.UserId, t.SessionId })
                .FirstOrDefaultAsync(ct);

            if (tokenInfo is not null)
            {
                var isSqlite = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
                await using var transaction = await db.Database.BeginTransactionAsync(
                    isSqlite ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct);
                if (!isSqlite)
                {
                    await db.Database.SqlQuery<int>(
                        $"SELECT id AS Value FROM users WHERE id = {tokenInfo.UserId} FOR UPDATE").ToListAsync(ct);
                }

                await db.RevokeSessionAsync(tokenInfo.SessionId, clock.GetUtcNow().UtcDateTime);
                await transaction.CommitAsync(ct);
                hubConnections.DisconnectSession(tokenInfo.SessionId);
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
