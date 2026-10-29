using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.Auth;

/// <summary>
/// facility-0095..0097: the Server Log's security lines, one Warning each, all in one category so
/// `journalctl --namespace=facility -u facility | grep -A1 FacilityRealtime.Security` finds them. An account is named by employee ID, or
/// by username for an Admin. A login name that matches no account is never written (a phone typed into the wrong
/// field would land in the journal), and neither is any phone, password, token or GPS value.
/// </summary>
public sealed class SecurityLog(ILoggerFactory loggerFactory)
{
    public const string Category = "FacilityRealtime.Security";

    private readonly ILogger _logger = loggerFactory.CreateLogger(Category);

    /// <summary>The account exists: either the secret was wrong, or it was right and the account is turned off.</summary>
    public void LoginFailed(User user, bool secretMatched) =>
        _logger.LogWarning("Login failed: {Account}, {Reason}", Account(user), secretMatched ? "account turned off" : "wrong secret");

    public void LoginFailedUnknownAccount(bool byEmployeeId) =>
        _logger.LogWarning("Login failed: unknown account ({Kind} login)", Kind(byEmployeeId));

    /// <summary>One line per refused attempt: repeated lines show the guessing goes on.</summary>
    public void LoginRefusedWhileLocked(User? user, bool byEmployeeId)
    {
        if (user is null)
        {
            _logger.LogWarning("Login refused while locked: unknown account ({Kind} login)", Kind(byEmployeeId));
        }
        else
        {
            _logger.LogWarning("Login refused while locked: {Account}", Account(user));
        }
    }

    public void RefreshTokenReused(User user) =>
        _logger.LogWarning("Refresh token reused, session ended: {Account}", Account(user));

    private static string Account(User user) =>
        user.Role == UserRole.Admin ? $"admin {user.Username}" : $"employee {user.EmployeeId}";

    private static string Kind(bool byEmployeeId) => byEmployeeId ? "employee" : "admin";
}
