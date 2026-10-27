using FacilityRealtime.Application.Buildings;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0090: what the Admin may type on the buildings page, and the answers when a building is in the way.</summary>
public class BuildingRulesTests
{
    [Theory]
    [InlineData(" b2 ", "B2")]
    [InlineData("A", "A")]
    [InlineData("ABCDEFGHIJ0123456789", "ABCDEFGHIJ0123456789")]
    public void Codes_are_trimmed_and_upper_cased(string typed, string expected) =>
        Assert.Equal(expected, BuildingRules.NormalizeCode(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A-1")]
    [InlineData("BPI_B2")]
    [InlineData("อาคาร1")]
    [InlineData("ABCDEFGHIJ01234567890")]
    public void Other_codes_are_refused(string? typed) => Assert.Null(BuildingRules.NormalizeCode(typed));

    [Fact]
    public void Names_are_trimmed_and_100_characters_at_most()
    {
        Assert.Equal("ตึกผลิต", BuildingRules.NormalizeName("  ตึกผลิต "));
        Assert.Equal(new string('ก', 100), BuildingRules.NormalizeName(new string('ก', 100)));
        Assert.Null(BuildingRules.NormalizeName(new string('ก', 101)));
        Assert.Null(BuildingRules.NormalizeName("   "));
        Assert.Null(BuildingRules.NormalizeName(null));
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(2, 0, "ปิดตึก A ไม่ได้ ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง ปิดหรือย้ายออกก่อน")]
    [InlineData(0, 1, "ปิดตึก A ไม่ได้ ยังมีหัวหน้าประจำตึก 1 คน ปิดหรือย้ายออกก่อน")]
    [InlineData(2, 1, "ปิดตึก A ไม่ได้ ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง และหัวหน้าประจำตึก 1 คน ปิดหรือย้ายออกก่อน")]
    public void A_building_turns_off_only_when_nothing_active_is_left(int areas, int supervisors, string? expected) =>
        Assert.Equal(expected, BuildingRules.DeactivateProblem("A", areas, supervisors));

    [Fact]
    public void A_building_that_is_off_names_itself_and_the_way_out()
    {
        Assert.Equal("ตึก B ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายพื้นที่ไปตึกอื่น", BuildingRules.OffForArea("B"));
        Assert.Equal("ตึก B ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายหัวหน้าไปตึกอื่น", BuildingRules.OffForSupervisor("B"));
    }
}
