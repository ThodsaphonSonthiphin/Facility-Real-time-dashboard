using FacilityRealtime.Api.Endpoints;
using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Commands;

/// <summary>
/// facility-0092 decision 5: `reset-admin &lt;username&gt;`, run on the server with the API's own settings, recovers
/// Admin access when no Admin can log in. The password is asked twice with the typing hidden, never taken as an
/// argument, because a shell keeps its history. Exit code 0 done, 1 refused, 2 wrong usage.
/// </summary>
public static class ResetAdminCommand
{
    public const string Name = "reset-admin";

    public const string Usage = $"วิธีใช้: {Name} <ชื่อผู้ใช้แอดมิน>";

    /// <summary>
    /// facility-0093 decision 2: names the database before anything touches it, the migration included, and goes on
    /// only after "yes". Shows server, port and database, never the database's user name or password.
    /// </summary>
    public static bool ConfirmDatabase(string connectionString, IAdminConsole console)
    {
        console.WriteLine($"คำสั่งนี้จะเปลี่ยนข้อมูลในฐานข้อมูล {DatabaseSettings.Describe(connectionString)}");
        if (string.Equals(console.ReadLine("ถ้าถูกต้อง พิมพ์ yes แล้วกด Enter: ")?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        console.WriteLine("ไม่ได้พิมพ์ yes ไม่ได้เปลี่ยนอะไร");
        return false;
    }

    /// <param name="args">What follows "reset-admin" on the command line.</param>
    public static async Task<int> RunAsync(string[] args, AppDbContext db, IPasswordHasher hasher, TimeProvider clock, IAdminConsole console)
    {
        if (args.Length != 1)
        {
            console.WriteLine(Usage);
            return 2;
        }

        if (AccountRules.NormalizeUsername(args[0]) is not { } username)
        {
            console.WriteLine("ชื่อผู้ใช้ต้องเป็นตัวอักษรอังกฤษ ตัวเลข . _ หรือ - ยาว 3–100 ตัว ไม่ได้เปลี่ยนอะไร");
            return 1;
        }

        var target = await ServerAdminAccounts.FindAsync(db, username);
        if (target is RecoveryTarget.Staff staff)
        {
            console.WriteLine($"{username} เป็นรหัสพนักงานของ{ThaiLabels.RoleName(staff.Role)} ไม่ใช่แอดมิน ไม่ได้เปลี่ยนอะไร");
            return 1;
        }

        string? displayName = null;
        if (target is RecoveryTarget.Nobody)
        {
            console.WriteLine($"ยังไม่มีแอดมินชื่อ {username} จะสร้างบัญชีใหม่");
            displayName = AccountRules.NormalizeDisplayName(console.ReadLine("ชื่อที่แสดง: "));
            if (displayName is null)
            {
                console.WriteLine($"ต้องใส่ชื่อที่แสดง ไม่เกิน {AccountRules.MaxDisplayName} ตัวอักษร ไม่ได้เปลี่ยนอะไร");
                return 1;
            }
        }

        var password = console.ReadSecret("รหัสผ่านใหม่ (ไม่แสดงบนจอ): ");
        if (console.ReadSecret("พิมพ์รหัสผ่านใหม่อีกครั้ง: ") != password)
        {
            console.WriteLine("รหัสผ่านสองครั้งไม่ตรงกัน ไม่ได้เปลี่ยนอะไร");
            return 1;
        }

        if (AccountRules.PasswordProblem(password) is { } problem)
        {
            console.WriteLine($"{problem} ไม่ได้เปลี่ยนอะไร");
            return 1;
        }

        if (target is RecoveryTarget.Admin existing)
        {
            await ServerAdminAccounts.ResetAsync(db, hasher, clock, existing.Account, password!);
            console.WriteLine($"ตั้งรหัสผ่านใหม่ให้ {existing.Account.Username} แล้ว บัญชีเปิดใช้งาน และทุกเครื่องที่เคยเข้าระบบด้วยบัญชีนี้ต้องเข้าใหม่");
        }
        else
        {
            await ServerAdminAccounts.CreateAsync(
                db, hasher, clock, username, displayName!, password!, ServerAdminAccounts.RecoverCreateAction,
                $"สร้างแอดมิน {username} {displayName} ด้วยคำสั่ง reset-admin บนเซิร์ฟเวอร์");
            console.WriteLine($"สร้างแอดมิน {username} แล้ว");
        }

        // LoginThrottle lives in the running API's memory; this process cannot clear it (facility-0092 decision 5)
        console.WriteLine("ถ้าบัญชีเพิ่งถูกล็อกเพราะใส่รหัสผิด 5 ครั้ง ให้รอ 15 นาที หรือรีสตาร์ตระบบ");
        console.WriteLine("เข้าระบบแล้วเปลี่ยนรหัสผ่านของตัวเองที่หน้าบัญชีผู้ใช้ทันที");
        return 0;
    }
}
