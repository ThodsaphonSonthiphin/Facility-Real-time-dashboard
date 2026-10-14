using System.Globalization;
using System.Text.RegularExpressions;

namespace FacilityRealtime.Application.Signs;

/// <summary>The codes printed on the signs, e.g. AR01-IN and AR01-03 (facility-0040, 0045), and their QR Tokens (facility-0006).</summary>
public static partial class SignCodes
{
    /// <summary>Trimmed and upper-cased; null unless it is 2–20 letters A–Z or digits. It prefixes every sign code of the Area.</summary>
    public static string? NormalizeAreaCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        return normalized is not null && AreaCodePattern().IsMatch(normalized) ? normalized : null;
    }

    public static string CheckIn(string areaCode) => $"{areaCode}-IN";

    /// <summary>One past the highest number already used in the Area, so a deactivated point's number is never reused.</summary>
    public static string NextPointCode(string areaCode, IEnumerable<string> existingPointCodes)
    {
        var prefix = areaCode + "-";
        var highest = existingPointCodes
            .Where(code => code.StartsWith(prefix, StringComparison.Ordinal))
            .Select(code => int.TryParse(code[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{highest + 1:00}";
    }

    /// <summary>facility-0006: a random UUID v4. A new one makes the printed sign stop working.</summary>
    public static string NewQrToken() => Guid.NewGuid().ToString();

    [GeneratedRegex("^[A-Z0-9]{2,20}$")]
    private static partial Regex AreaCodePattern();
}
