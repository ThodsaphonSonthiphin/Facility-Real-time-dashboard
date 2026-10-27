using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FacilityRealtime.Infrastructure.Persistence;

public enum FirstAdminOutcome
{
    Created,
    AdminExists,
    NotConfigured,
}

/// <summary>
/// facility-0092 decisions 2–4, run at startup after the migration (and the seed, on a developer machine): the server's
/// FirstAdmin settings create one Admin, only while no Admin exists at all. A bad value stops the startup and names
/// the setting; the password itself never reaches the log or the error.
/// </summary>
public static class FirstAdmin
{
    public static async Task<FirstAdminOutcome> EnsureAsync(
        AppDbContext db, IPasswordHasher hasher, TimeProvider clock, FirstAdminSettings settings, ILogger logger)
    {
        // Any Admin row, turned off or not: the settings never create another, change a password or turn anyone on
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            if (!string.IsNullOrEmpty(settings.Password))
            {
                logger.LogWarning("FirstAdmin__Password is still set, but an Admin already exists, so it is ignored. Remove it from the server.");
            }

            return FirstAdminOutcome.AdminExists;
        }

        if (string.IsNullOrWhiteSpace(settings.Username) && string.IsNullOrEmpty(settings.Password) && string.IsNullOrWhiteSpace(settings.DisplayName))
        {
            logger.LogWarning(
                "No Admin account exists. Set FirstAdmin__Username, FirstAdmin__Password and FirstAdmin__DisplayName on the server, then restart (docs/handover.md).");
            return FirstAdminOutcome.NotConfigured;
        }

        var username = AccountRules.NormalizeUsername(settings.Username)
            ?? throw new InvalidOperationException("FirstAdmin__Username: 3–100 English letters, digits, '.', '_' or '-'.");
        var displayName = AccountRules.NormalizeDisplayName(settings.DisplayName)
            ?? throw new InvalidOperationException($"FirstAdmin__DisplayName: 1–{AccountRules.MaxDisplayName} characters.");
        if (AccountRules.PasswordProblem(settings.Password) is { } problem)
        {
            // The rule's words only, never the value
            throw new InvalidOperationException($"FirstAdmin__Password: {problem}");
        }

        await ServerAdminAccounts.CreateAsync(
            db, hasher, clock, username, displayName, settings.Password!, ServerAdminAccounts.FirstCreateAction,
            $"สร้างแอดมินคนแรก {username} {displayName} จากค่าในเซิร์ฟเวอร์");
        logger.LogInformation(
            "Created the first Admin '{Username}'. Remove FirstAdmin__Password from the server, log in and change the password on the accounts page.",
            username);
        return FirstAdminOutcome.Created;
    }
}
