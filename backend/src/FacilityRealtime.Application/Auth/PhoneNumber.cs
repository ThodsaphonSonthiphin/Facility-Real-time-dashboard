namespace FacilityRealtime.Application.Auth;

/// <summary>facility-0054: the phone number is the Cleaner's and Supervisor's secret, so it must hash the same however it is typed.</summary>
public static class PhoneNumber
{
    /// <summary>Digits only; a +66 country code becomes the leading 0 people type in Thailand.</summary>
    public static string Normalize(string? typed)
    {
        var digits = new string((typed ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 11 && digits.StartsWith("66") ? "0" + digits[2..] : digits;
    }
}
