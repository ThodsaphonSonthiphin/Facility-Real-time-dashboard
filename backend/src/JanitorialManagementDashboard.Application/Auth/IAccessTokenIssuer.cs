using JanitorialManagementDashboard.Domain.Entities;

namespace JanitorialManagementDashboard.Application.Auth;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
