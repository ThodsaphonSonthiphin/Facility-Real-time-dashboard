using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>database.html alert_reviews: three kinds are closed by "คุยแล้ว"; a missing Shift-Out closes when it is filled (facility-0051).</summary>
public class AlertKindsTests
{
    [Theory]
    [InlineData(AlertKind.BlockedScan, AlertType.BlockedScan)]
    [InlineData(AlertKind.AttendanceGps, AlertType.AttendanceGps)]
    [InlineData(AlertKind.SubmissionGps, AlertType.SubmissionGps)]
    public void Reviewable_kinds_map_to_their_alert_reviews_type(AlertKind kind, AlertType type) =>
        Assert.Equal((AlertType?)type, AlertKinds.ReviewType(kind));

    [Fact]
    public void A_missing_shift_out_is_filled_not_reviewed() => Assert.Null(AlertKinds.ReviewType(AlertKind.MissingAttendance));
}
