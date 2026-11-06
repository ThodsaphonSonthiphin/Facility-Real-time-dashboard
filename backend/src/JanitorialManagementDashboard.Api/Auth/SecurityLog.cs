using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Api.Auth;

/// <summary>
/// facility-0095..0097: the Server Log's security lines, one Warning each, all in one category so
/// `journalctl --namespace=facility -u facility | grep JanitorialManagementDashboard.Security` finds them (the category is on each line,
/// facility-0103). An account is named by employee ID, or by username for an Admin. A login name that matches no account is never written (a phone typed into the wrong
/// field would land in the journal), and neither is any phone, password, token or GPS value. Login lines end with the
/// place the attempt came from (facility-0104 decision 9): the address, or its /64 for IPv6, as the Login Lock counts it.
/// </summary>
public sealed class SecurityLog(ILoggerFactory loggerFactory)
{
    public const string Category = "JanitorialManagementDashboard.Security";

    private readonly ILogger _logger = loggerFactory.CreateLogger(Category);

    /// <summary>The account exists: either the secret was wrong, or it was right and the account is turned off.</summary>
    public void LoginFailed(User user, bool secretMatched, string place) =>
        _logger.LogWarning(
            "Login failed: {Account}, {Reason}, from {Place}", Account(user), secretMatched ? "account turned off" : "wrong secret", place);

    public void LoginFailedUnknownAccount(bool byEmployeeId, string place) =>
        _logger.LogWarning("Login failed: unknown account ({Kind} login), from {Place}", Kind(byEmployeeId), place);

    /// <summary>One line per refused attempt: repeated lines show the guessing goes on.</summary>
    public void LoginRefusedWhileLocked(User? user, bool byEmployeeId, string place)
    {
        if (user is null)
        {
            _logger.LogWarning("Login refused while locked: unknown account ({Kind} login), from {Place}", Kind(byEmployeeId), place);
        }
        else
        {
            _logger.LogWarning("Login refused while locked: {Account}, from {Place}", Account(user), place);
        }
    }

    /// <summary>facility-0104 decision 2: written once, when the place reaches its cap of failures across every login name.</summary>
    public void LoginPlaceLocked(string place) =>
        _logger.LogWarning("Login place locked after too many failures: {Place}", place);

    public void RefreshTokenReused(User user) =>
        _logger.LogWarning("Refresh token reused, session ended: {Account}", Account(user));

    private static string Account(User user) =>
        user.Role == UserRole.Admin ? $"admin {user.Username}" : $"employee {user.EmployeeId}";

    private static string Kind(bool byEmployeeId) => byEmployeeId ? "employee" : "admin";
}
