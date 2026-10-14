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

    [Theory]
    [InlineData("0000000012", true)]
    [InlineData("0000000011", true)]
    [InlineData("000000001", false)]
    [InlineData("00000000111", false)]
    [InlineData("1000000011", false)]
    [InlineData("", false)]
    public void A_login_phone_is_ten_digits_starting_with_zero(string normalized, bool complete) =>
        Assert.Equal(complete, PhoneNumber.IsComplete(normalized));

    [Fact]
    public void Thai_digits_never_make_a_complete_phone() =>
        Assert.False(PhoneNumber.IsComplete(PhoneNumber.Normalize("๐๐๐๐๐๐๐๐๑๑")));

    [Fact]
    public void A_typed_phone_with_dashes_or_country_code_is_complete_once_normalized()
    {
        Assert.True(PhoneNumber.IsComplete(PhoneNumber.Normalize("000-000-0011")));
        Assert.True(PhoneNumber.IsComplete(PhoneNumber.Normalize("+66 00 000 0011")));
    }
}
