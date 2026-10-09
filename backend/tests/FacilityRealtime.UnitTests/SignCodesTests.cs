using FacilityRealtime.Application.Signs;

namespace FacilityRealtime.UnitTests;

/// <summary>The codes printed on the signs (facility-0040, 0045) and their QR Tokens (facility-0006).</summary>
public class SignCodesTests
{
    [Theory]
    [InlineData(" ar03 ", "AR03")]
    [InlineData("B12", "B12")]
    [InlineData("ABCDEFGHIJ0123456789", "ABCDEFGHIJ0123456789")]
    public void Area_codes_are_trimmed_and_upper_cased(string input, string expected)
    {
        Assert.Equal(expected, SignCodes.NormalizeAreaCode(input));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AR-03")]
    [InlineData("อาคาร1")]
    [InlineData("ABCDEFGHIJ01234567890")]
    [InlineData("")]
    [InlineData(null)]
    public void Area_codes_are_two_to_twenty_letters_or_digits(string? input)
    {
        Assert.Null(SignCodes.NormalizeAreaCode(input));
    }

    [Fact]
    public void The_check_in_sign_ends_in_IN()
    {
        Assert.Equal("AR01-IN", SignCodes.CheckIn("AR01"));
    }

    [Theory]
    [InlineData("", "AR01-01")]
    [InlineData("AR01-01,AR01-02", "AR01-03")]
    [InlineData("AR01-09,AR01-03", "AR01-10")]
    [InlineData("AR01-99", "AR01-100")]
    [InlineData("AR011-05,AR01-IN", "AR01-01")]
    public void A_new_point_takes_one_past_the_highest_number_ever_used(string existing, string expected)
    {
        var codes = existing.Split(',', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(expected, SignCodes.NextPointCode("AR01", codes));
    }

    [Fact]
    public void Qr_tokens_are_random_uuids()
    {
        var first = SignCodes.NewQrToken();
        var second = SignCodes.NewQrToken();

        Assert.True(Guid.TryParse(first, out _));
        Assert.NotEqual(first, second);
    }
}
