using System.Text;
using FacilityRealtime.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FacilityRealtime.Api.Auth;

public static class AuthSetup
{
    /// <summary>ADR facility-0010: 401 when not logged in, 403 when logged in without the admin role.</summary>
    public const string AdminOnly = "AdminOnly";

    public static IServiceCollection AddFacilityAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                s => Encoding.UTF8.GetByteCount(s.SigningKey) >= 32,
                "Jwt:SigningKey must be at least 32 bytes. Set it once per machine with: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"$(openssl rand -base64 48)\" --project backend/src/FacilityRealtime.Api")
            .Validate(s => s.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

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
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminOnly, policy => policy.RequireAuthenticatedUser().RequireRole("admin"));

        return services;
    }
}
