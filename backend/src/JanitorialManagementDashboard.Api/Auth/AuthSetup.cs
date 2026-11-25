using System.Security.Claims;
using System.Text;
using JanitorialManagementDashboard.Application.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JanitorialManagementDashboard.Api.Auth;

public static class AuthSetup
{
    /// <summary>ADR facility-0010: 401 when not logged in, 403 when logged in without the admin role.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Spec §3 and §11: who may call a scheduling management route at all — an active Admin or an active Supervisor, and
    /// never a Cleaner. The policy only answers the role question; which building, shift and date a command may touch is
    /// decided per row by <c>ScheduleAuthority</c> inside the command. It is its own policy so adding scheduling does not
    /// widen <see cref="AdminOnly"/> or any legacy cover, scan or inspection group.
    /// </summary>
    public const string ScheduleManager = "ScheduleManager";

    public static IServiceCollection AddFacilityAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                s => Encoding.UTF8.GetByteCount(s.SigningKey) >= 32,
                "Jwt:SigningKey must be at least 32 bytes. Set it once per machine with: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"$(openssl rand -base64 48)\" --project backend/src/JanitorialManagementDashboard.Api")
            .Validate(s => s.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<SecurityLog>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured lazily from validated settings, so building the host (and `dotnet ef`) never needs the key
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((bearer, jwt) =>
            {
                var settings = jwt.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = JwtKeys.SigningKey(settings),
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30), // ADR facility-0012: the .NET default of 5 minutes would double the token's life
                    NameClaimType = AuthClaims.Name,
                    RoleClaimType = AuthClaims.Role,
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // ADR facility-0011: browsers cannot set headers on a WebSocket, so SignalR sends the token
                        // in the query string. Accept that only for hub paths.
                        var queryToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(queryToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = queryToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        // Validate active account and session on hub negotiation / initial connection (skipping
                        // already-negotiated LongPolling poll ticks carrying ?id=..., since active connections are
                        // aborted immediately via ScanHubConnections on logout, deactivation, or secret change).
                        if (!context.HttpContext.Request.Path.StartsWithSegments("/hubs")
                            || context.HttpContext.Request.Query.ContainsKey("id"))
                        {
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var user = context.Principal is not null
                            ? await CurrentUser.LoadAsync(context.Principal, db)
                            : null;
                        if (user is null)
                        {
                            context.Fail("Account is inactive or missing.");
                            return;
                        }

                        if (Guid.TryParse(context.Principal!.FindFirstValue(AuthClaims.SessionId), out var sid))
                        {
                            var activeSession = await db.RefreshTokens.AsNoTracking()
                                .AnyAsync(t => t.SessionId == sid && t.UserId == user.Id && t.RevokedAt == null,
                                    context.HttpContext.RequestAborted);
                            if (!activeSession)
                            {
                                context.Fail("Session has been revoked.");
                            }
                        }
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminOnly, policy => policy.RequireAuthenticatedUser().RequireRole("admin"))
            // Spec §3: the scheduling management surface is Admin or Supervisor; a Cleaner only ever reads their own week.
            .AddPolicy(ScheduleManager, policy => policy.RequireAuthenticatedUser().RequireRole("admin", "supervisor"));

        return services;
    }
}
