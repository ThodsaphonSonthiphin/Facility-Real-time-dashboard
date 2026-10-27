namespace FacilityRealtime.Application.Areas;

/// <summary>The answers when something is in the way of an Area change (facility-0091).</summary>
public static class AreaRules
{
    /// <summary>Decision 2: null when the Area may be turned off, else the active Cleaners who still hold it.</summary>
    public static string? DeactivateProblem(string code, IReadOnlyList<string> cleanerNames) =>
        cleanerNames.Count == 0
            ? null
            : $"ปิดพื้นที่ {code} ไม่ได้ ยังมีแม่บ้านประจำ {string.Join(", ", cleanerNames)} ย้ายแม่บ้านออกก่อน";

    /// <summary>Decision 2, the other way in: an Area that is off takes no new Cleaner; turn it on first.</summary>
    public const string OffAreaTakesNoNewCleaner = "เปิดใช้งานพื้นที่ก่อน แล้วค่อยใส่แม่บ้าน";
}
