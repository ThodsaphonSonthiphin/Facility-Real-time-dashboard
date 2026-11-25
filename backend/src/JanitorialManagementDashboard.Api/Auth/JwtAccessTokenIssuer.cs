using System.Security.Claims;
using JanitorialManagementDashboard.Application.Auth;
using JanitorialManagementDashboard.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JanitorialManagementDashboard.Api.Auth;

public sealed class JwtAccessTokenIssuer(IOptions<JwtSettings> options, TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(User user, Guid? sessionId = null)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.AccessTokenMinutes);

        var claims = new List<Claim>(5)
        {
            new(AuthClaims.UserId, user.Id.ToString()),
            new(AuthClaims.Username, user.LoginName),
            new(AuthClaims.Name, user.DisplayName),
            new(AuthClaims.Role, user.Role.ToClaimValue()),
        };
        if (sessionId is { } sid)
        {
            claims.Add(new Claim(AuthClaims.SessionId, sid.ToString("N")));
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(JwtKeys.SigningKey(settings), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }
}
