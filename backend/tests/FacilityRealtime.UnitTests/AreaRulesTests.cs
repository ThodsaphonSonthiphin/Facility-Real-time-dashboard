using FacilityRealtime.Application.Areas;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0091 decision 2: an Area is turned off only once no active Cleaner holds it.</summary>
public class AreaRulesTests
{
    [Fact]
    public void An_area_nobody_holds_may_be_turned_off() => Assert.Null(AreaRules.DeactivateProblem("AR01", []));

    [Fact]
    public void An_area_its_cleaners_hold_names_them() =>
        Assert.Equal(
            "ปิดพื้นที่ AR01 ไม่ได้ ยังมีแม่บ้านประจำ สมชาย ใจดี, สมหญิง รักสะอาด ย้ายแม่บ้านออกก่อน",
            AreaRules.DeactivateProblem("AR01", ["สมชาย ใจดี", "สมหญิง รักสะอาด"]));
}
