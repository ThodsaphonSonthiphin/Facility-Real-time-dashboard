using FacilityRealtime.Application.Accounts;

namespace FacilityRealtime.UnitTests;

public class CommonPasswordsTests
{
    [Fact]
    public void The_whole_list_is_read() => Assert.Equal(10898, CommonPasswords.Count);

    [Theory]
    [InlineData("1qaz2wsx3edc4rfv", true)]
    [InlineData("1QAZ2WSX3EDC4RFV", true)]
    [InlineData("123456789qwerty", true)]
    [InlineData("temporary-pass-1", false)]
    [InlineData("ห้องน้ำชั้นสามสะอาดเสมอ", false)]
    public void A_password_on_the_list_is_found_whatever_the_letter_case(string password, bool common) =>
        Assert.Equal(common, CommonPasswords.Contains(password));
}
