using System.Text.RegularExpressions;

namespace FacilityRealtime.Application.Buildings;

/// <summary>What the Admin may type on the buildings page, and the answers when a building is in the way (facility-0090).</summary>
public static partial class BuildingRules
{
    /// <summary>buildings.name is 100 wide.</summary>
    public const int MaxName = 100;

    public const string CodeProblem = "รหัสตึกต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2";
    public const string NameProblem = "ต้องใส่ชื่อตึก ไม่เกิน 100 ตัวอักษร";
    public const string CodeTaken = "รหัสตึกนี้มีแล้ว";

    /// <summary>Decision 2: trimmed and upper-cased; null unless it is 1–20 letters A–Z or digits. It is set once.</summary>
    public static string? NormalizeCode(string? typed)
    {
        var normalized = typed?.Trim().ToUpperInvariant();
        return normalized is not null && CodePattern().IsMatch(normalized) ? normalized : null;
    }

    /// <summary>Decision 3: trimmed; null when empty or longer than <see cref="MaxName"/>. Another building may share it.</summary>
    public static string? NormalizeName(string? typed)
    {
        var trimmed = typed?.Trim() ?? string.Empty;
        return trimmed.Length is 0 or > MaxName ? null : trimmed;
    }

    /// <summary>Decision 4: null when the building may be turned off, else what is still active in it.</summary>
    public static string? DeactivateProblem(string code, int activeAreas, int activeSupervisors)
    {
        var left = new List<string>();
        if (activeAreas > 0)
        {
            left.Add($"พื้นที่ที่เปิดอยู่ {activeAreas} แห่ง");
        }

        if (activeSupervisors > 0)
        {
            left.Add($"หัวหน้าประจำตึก {activeSupervisors} คน");
        }

        return left.Count == 0 ? null : $"ปิดตึก {code} ไม่ได้ ยังมี{string.Join(" และ", left)} ปิดหรือย้ายออกก่อน";
    }

    /// <summary>Decision 4: an Area of a building that is off stays off.</summary>
    public static string OffForArea(string code) => $"ตึก {code} ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายพื้นที่ไปตึกอื่น";

    /// <summary>Decision 4: a Supervisor of a building that is off stays off.</summary>
    public static string OffForSupervisor(string code) => $"ตึก {code} ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายหัวหน้าไปตึกอื่น";

    [GeneratedRegex("^[A-Z0-9]{1,20}$")]
    private static partial Regex CodePattern();
}
