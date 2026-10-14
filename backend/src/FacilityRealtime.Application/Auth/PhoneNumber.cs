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

    /// <summary>facility-0056: a login phone is 10 digits starting with 0. Pass the result of <see cref="Normalize"/>.</summary>
    public static bool IsComplete(string normalized) =>
        normalized.Length == 10 && normalized[0] == '0' && normalized.All(char.IsAsciiDigit);
}
