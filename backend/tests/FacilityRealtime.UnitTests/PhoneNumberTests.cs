using FacilityRealtime.Application.Auth;

namespace FacilityRealtime.UnitTests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("0810000001", "0810000001")]
    [InlineData("081-000-0001", "0810000001")]
    [InlineData(" 081 000 0001 ", "0810000001")]
    [InlineData("+66 81 000 0001", "0810000001")]
    [InlineData("66810000001", "0810000001")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_keeps_the_digits_people_type_in_Thailand(string? typed, string expected)
    {
        Assert.Equal(expected, PhoneNumber.Normalize(typed));
    }
}
