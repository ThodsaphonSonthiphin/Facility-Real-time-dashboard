using FacilityRealtime.Application.Audit;

namespace FacilityRealtime.UnitTests;

/// <summary>Wireframe D9's "ประเภท" filter over every action the Admin endpoints write.</summary>
public class AuditCategoriesTests
{
    [Theory]
    [InlineData("ATTENDANCE_ADD", AuditCategory.Attendance)]
    [InlineData("ATTENDANCE_EDIT", AuditCategory.Attendance)]
    [InlineData("COVER_ASSIGN", AuditCategory.Cover)]
    [InlineData("COVER_CANCEL", AuditCategory.Cover)]
    [InlineData("ALERT_REVIEW", AuditCategory.AlertReview)]
    [InlineData("AREA_CREATE", AuditCategory.Setup)]
    [InlineData("AREA_UPDATE", AuditCategory.Setup)]
    [InlineData("AREA_ACTIVATE", AuditCategory.Setup)]
    [InlineData("AREA_DEACTIVATE", AuditCategory.Setup)]
    [InlineData("BUILDING_CREATE", AuditCategory.Setup)]
    [InlineData("BUILDING_UPDATE", AuditCategory.Setup)]
    [InlineData("BUILDING_ACTIVATE", AuditCategory.Setup)]
    [InlineData("BUILDING_DEACTIVATE", AuditCategory.Setup)]
    [InlineData("POINT_CREATE", AuditCategory.Setup)]
    [InlineData("POINT_UPDATE", AuditCategory.Setup)]
    [InlineData("POINT_ACTIVATE", AuditCategory.Setup)]
    [InlineData("POINT_DEACTIVATE", AuditCategory.Setup)]
    [InlineData("SIGN_LOCATION", AuditCategory.Setup)]
    [InlineData("SIGN_RADIUS", AuditCategory.Setup)]
    [InlineData("ACCOUNT_CREATE", AuditCategory.Account)]
    [InlineData("ACCOUNT_UPDATE", AuditCategory.Account)]
    [InlineData("ACCOUNT_PHONE", AuditCategory.Account)]
    [InlineData("ACCOUNT_PASSWORD", AuditCategory.Account)]
    [InlineData("ACCOUNT_ACTIVATE", AuditCategory.Account)]
    [InlineData("ACCOUNT_DEACTIVATE", AuditCategory.Account)]
    [InlineData("EXPORT_EXCEL", AuditCategory.Export)]
    [InlineData("QR_REGENERATE", AuditCategory.QrToken)]
    public void Every_action_written_so_far_has_its_category(string action, AuditCategory category) =>
        Assert.Equal((AuditCategory?)category, AuditCategories.Of(action));

    [Fact]
    public void An_unknown_action_has_no_category() => Assert.Null(AuditCategories.Of("SOMETHING_NEW"));

    [Fact]
    public void Each_action_is_in_exactly_one_category()
    {
        var categories = Enum.GetValues<AuditCategory>();
        var actions = categories.SelectMany(c => AuditCategories.ActionsOf(c)).ToList();

        Assert.Equal(actions.Count, actions.Distinct().Count());
        Assert.All(categories, c => Assert.NotEmpty(AuditCategories.ActionsOf(c)));
    }
}
