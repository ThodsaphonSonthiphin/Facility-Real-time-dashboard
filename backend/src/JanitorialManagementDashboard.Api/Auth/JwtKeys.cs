using System.Text;
using JanitorialManagementDashboard.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace JanitorialManagementDashboard.Api.Auth;

public static class JwtKeys
{
    public static SymmetricSecurityKey SigningKey(JwtSettings settings) =>
        new(Encoding.UTF8.GetBytes(settings.SigningKey));
}
