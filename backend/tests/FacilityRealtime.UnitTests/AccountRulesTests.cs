using FacilityRealtime.Application.Accounts;

namespace FacilityRealtime.UnitTests;

/// <summary>Wireframe D6: what the Admin may type when adding an account (facility-0020, 0054, 0056).</summary>
public class AccountRulesTests
{
    [Theory]
    [InlineData(" 1000001 ", "1000001")]
    [InlineData("emp-0042", "emp-0042")]
    [InlineData("A", "A")]
    [InlineData("12345678901234567890", "12345678901234567890")]
    public void Employee_ids_are_trimmed(string typed, string expected) =>
        Assert.Equal(expected, AccountRules.NormalizeEmployeeId(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("E 1001")]
    [InlineData("1000001!")]
    [InlineData("รหัส1")]
    [InlineData("123456789012345678901")]
    public void Bad_employee_ids_are_refused(string? typed) => Assert.Null(AccountRules.NormalizeEmployeeId(typed));

    [Theory]
    [InlineData(" admin2 ", "admin2")]
    [InlineData("a.b_c-d", "a.b_c-d")]
    public void Usernames_are_trimmed(string typed, string expected) => Assert.Equal(expected, AccountRules.NormalizeUsername(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("ผู้ดูแล")]
    public void Bad_usernames_are_refused(string? typed) => Assert.Null(AccountRules.NormalizeUsername(typed));

    [Fact]
    public void A_username_is_at_most_100_characters()
    {
        Assert.NotNull(AccountRules.NormalizeUsername(new string('a', 100)));
        Assert.Null(AccountRules.NormalizeUsername(new string('a', 101)));
    }

    [Fact]
    public void Display_names_are_trimmed_and_at_most_150_characters()
    {
        Assert.Equal("สมใจ ทดสอบ", AccountRules.NormalizeDisplayName("  สมใจ ทดสอบ  "));
        Assert.NotNull(AccountRules.NormalizeDisplayName(new string('ก', 150)));
        Assert.Null(AccountRules.NormalizeDisplayName(new string('ก', 151)));
        Assert.Null(AccountRules.NormalizeDisplayName("   "));
        Assert.Null(AccountRules.NormalizeDisplayName(null));
    }

    [Theory]
    [InlineData("temporary-pass-1", true)]
    [InlineData("temp pass phrase", true)]
    [InlineData("temporary-pass1", true)]
    [InlineData("12345678901234", false)]
    [InlineData("               ", false)]
    [InlineData(null, false)]
    public void A_temporary_password_is_15_to_100_characters(string? password, bool allowed) =>
        Assert.Equal(allowed, AccountRules.IsAllowedPassword(password));

    [Fact]
    public void A_password_longer_than_100_characters_is_refused() =>
        Assert.False(AccountRules.IsAllowedPassword(new string('x', 101)));

    [Theory]
    [InlineData("short", "รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร")]
    [InlineData(null, "รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร")]
    [InlineData("1QAZ2WSX3EDC4RFV", "รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น")]
    [InlineData("123456789012345", "รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น")]
    [InlineData("temporary-pass-1", null)]
    public void A_password_problem_says_the_length_first_then_whether_it_is_common(string? password, string? problem) =>
        Assert.Equal(problem, AccountRules.PasswordProblem(password));
}
