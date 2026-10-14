using System.Globalization;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>Query-string values of the Admin pages. Dates are Thai calendar dates written yyyy-MM-dd.</summary>
internal static class AdminQuery
{
    public static bool TryReadDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>An enum by its name in any letter case, e.g. "day" or "Night". Numbers are refused.</summary>
    public static bool TryReadEnum<T>(string? text, out T value)
        where T : struct, Enum
    {
        var name = Enum.GetNames<T>().FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        value = name is null ? default : Enum.Parse<T>(name);
        return name is not null;
    }

    /// <summary>Both values, or neither: then the shift on the clock, as the status page shows it. Returns the error to show, or null.</summary>
    public static string? ReadSlot(string? shiftDate, string? shift, DateTime nowUtc, out ShiftSlot slot)
    {
        slot = ShiftCalendar.SlotAt(nowUtc);
        if (shiftDate is null && shift is null)
        {
            return null;
        }

        if (!TryReadDate(shiftDate, out var date) || !TryReadEnum<Shift>(shift, out var parsed))
        {
            return "ต้องระบุวันที่ (yyyy-MM-dd) และกะ (Day หรือ Night) คู่กัน";
        }

        slot = new ShiftSlot(date, parsed);
        return null;
    }
}
