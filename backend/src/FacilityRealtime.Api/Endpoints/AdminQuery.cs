using System.Globalization;
using FacilityRealtime.Application.Common;
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

    /// <summary>
    /// Thai dates from and to, both included, as the UTC range [from 00:00, the day after to 00:00) Thai time.
    /// Neither given: the <paramref name="defaultDays"/> days ending today. Returns the error to show, or null.
    /// </summary>
    public static string? ReadDateRange(
        string? from, string? to, DateTime nowUtc, int defaultDays, int maxDays, out DateTime fromUtc, out DateTime toUtc)
    {
        fromUtc = toUtc = default;
        DateOnly first, last;
        if (from is null && to is null)
        {
            last = DateOnly.FromDateTime(ThaiTime.FromUtc(nowUtc));
            first = last.AddDays(1 - defaultDays);
        }
        else if (!TryReadDate(from, out first) || !TryReadDate(to, out last))
        {
            return "ต้องระบุทั้งวันเริ่ม (from) และวันสุดท้าย (to) แบบ yyyy-MM-dd";
        }

        if (last < first)
        {
            return "วันสุดท้ายต้องไม่ก่อนวันเริ่ม";
        }

        if (last.DayNumber - first.DayNumber + 1 > maxDays)
        {
            return $"เลือกได้ไม่เกิน {maxDays} วัน";
        }

        fromUtc = ThaiTime.ToUtc(first.ToDateTime(TimeOnly.MinValue));
        toUtc = ThaiTime.ToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue));
        return null;
    }
}
