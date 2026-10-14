using FacilityRealtime.Application.Accounts;

namespace FacilityRealtime.UnitTests;

/// <summary>Wireframe D6: what the Admin may type when adding an account (facility-0020, 0054, 0056).</summary>
public class AccountRulesTests
{
    [Theory]
    [InlineData(" E1001 ", "E1001")]
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
    [InlineData("E1001!")]
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
    [InlineData("12345678", true)]
    [InlineData("temp pass 1", true)]
    [InlineData("1234567", false)]
    [InlineData("        ", false)]
    [InlineData(null, false)]
    public void A_temporary_password_is_8_to_100_characters(string? password, bool allowed) =>
        Assert.Equal(allowed, AccountRules.IsAllowedPassword(password));

    [Fact]
    public void A_password_longer_than_100_characters_is_refused() =>
        Assert.False(AccountRules.IsAllowedPassword(new string('x', 101)));
}
