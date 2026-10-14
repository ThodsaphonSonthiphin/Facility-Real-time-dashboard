using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests.Infrastructure;

public class MySqlTestDatabaseTests
{
    [Theory]
    [InlineData("facility_x_test", true)]
    [InlineData("facility_attendance_test", true)]
    [InlineData("facility_dashboard", false)]
    [InlineData("x_test", false)]
    [InlineData("facility_test", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_facility_underscore_test_names_are_throwaway(string? name, bool expected) =>
        Assert.Equal(expected, MySqlTestDatabase.IsThrowawayName(name));
}
