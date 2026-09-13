using System.Text;
using FacilityRealtime.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace FacilityRealtime.Api.Auth;

public static class JwtKeys
{
    public static SymmetricSecurityKey SigningKey(JwtSettings settings) =>
        new(Encoding.UTF8.GetBytes(settings.SigningKey));
}
