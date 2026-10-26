using System.Text.RegularExpressions;

namespace FacilityRealtime.Application.Accounts;

/// <summary>What the Admin may type on the accounts page (wireframe D6, facility-0020, 0054, 0056).</summary>
public static partial class AccountRules
{
    /// <summary>users.display_name is 150 wide.</summary>
    public const int MaxDisplayName = 150;

    /// <summary>facility-0089: 15 for a login by password alone (NIST SP 800-63B-4, 3.1.1.2).</summary>
    public const int MinPassword = 15;
    public const int MaxPassword = 100;

    public static readonly string PasswordLengthProblem = $"รหัสผ่านชั่วคราวต้องยาว {MinPassword}–{MaxPassword} ตัวอักษร";
    public const string CommonPasswordProblem = "รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น";

    /// <summary>Trimmed; 1–20 English letters, digits or "-" (users.employee_id is 20 wide). Null when not allowed.</summary>
    public static string? NormalizeEmployeeId(string? typed)
    {
        var trimmed = typed?.Trim() ?? string.Empty;
        return EmployeeIdPattern().IsMatch(trimmed) ? trimmed : null;
    }

    /// <summary>The Admin's login: trimmed; 3–100 English letters, digits, ".", "_" or "-". Null when not allowed.</summary>
    public static string? NormalizeUsername(string? typed)
    {
        var trimmed = typed?.Trim() ?? string.Empty;
        return UsernamePattern().IsMatch(trimmed) ? trimmed : null;
    }

    public static string? NormalizeDisplayName(string? typed)
    {
        var trimmed = typed?.Trim() ?? string.Empty;
        return trimmed.Length is 0 or > MaxDisplayName ? null : trimmed;
    }

    /// <summary>The temporary password is kept exactly as typed, so spaces count, but it cannot be only spaces.</summary>
    public static bool IsAllowedPassword(string? password) =>
        password is { Length: >= MinPassword and <= MaxPassword } && !string.IsNullOrWhiteSpace(password);

    /// <summary>
    /// facility-0089: what is wrong with a new Admin password, or null. Checked when an Admin is created and when a
    /// password is reset, never at login, so a shorter password set before keeps working until it is reset.
    /// </summary>
    public static string? PasswordProblem(string? password) =>
        !IsAllowedPassword(password) ? PasswordLengthProblem
        : CommonPasswords.Contains(password!) ? CommonPasswordProblem
        : null;

    [GeneratedRegex(@"^[A-Za-z0-9-]{1,20}\z")]
    private static partial Regex EmployeeIdPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{3,100}\z")]
    private static partial Regex UsernamePattern();
}
