using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>facility-0053: the rows the Excel export reads, by shift date and building, with people and places joined.</summary>
public class ExportQueryTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);
    private static readonly DateOnly Oct9 = new(2026, 10, 9);

    private static async Task<T> QueryAsync<T>(FacilityApiFactory factory, Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await factory.WithDbAsync(async db => result = await query(db));
        return result;
    }

    [Fact]
    public async Task Attendance_rows_carry_the_person_and_place_inside_the_date_range_only()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftOut, ThaiClock.At(8, 19, 2), AttendanceSource.AdminAdd);
        await TestData.AddAttendanceAsync(factory, "E1001", new DateOnly(2026, 10, 10), Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(10, 6, 59));

        var rows = await QueryAsync(factory, db => ExportQuery.LoadAttendanceAsync(db, Oct8, Oct9, buildingId: null));

        Assert.Equal(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut }, rows.Select(r => r.EventType));
        var shiftIn = rows[0];
        Assert.Equal(("E1001", "สมชาย ใจดี", "A", "AR01", Oct8, Shift.Day), (shiftIn.EmployeeId, shiftIn.DisplayName, shiftIn.BuildingCode, shiftIn.AreaCode, shiftIn.ShiftDate, shiftIn.Shift));
        Assert.Equal((AttendanceSource.Scan, (short?)12, (bool?)true), (shiftIn.Source, shiftIn.DistanceM, shiftIn.WithinRadius));
        Assert.Equal(ThaiClock.At(8, 6, 58).UtcDateTime, DateTime.SpecifyKind(shiftIn.OccurredAtUtc, DateTimeKind.Utc));
        Assert.Equal((AttendanceSource.AdminAdd, (bool?)null), (rows[1].Source, rows[1].WithinRadius));
    }

    [Fact]
    public async Task The_building_filter_leaves_out_other_buildings()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var buildingA = await QueryAsync(factory, db => db.Buildings.Where(b => b.Code == "A").Select(b => b.Id).SingleAsync());

        Assert.Empty(await QueryAsync(factory, db => ExportQuery.LoadAttendanceAsync(db, Oct8, Oct8, buildingB)));
        Assert.Empty(await QueryAsync(factory, db => ExportQuery.LoadSubmissionsAsync(db, Oct8, Oct8, buildingB)));
        Assert.Empty(await QueryAsync(factory, db => ExportQuery.LoadPointsAsync(db, buildingB)));
        Assert.Single(await QueryAsync(factory, db => ExportQuery.LoadAttendanceAsync(db, Oct8, Oct8, buildingA)));
        Assert.Single(await QueryAsync(factory, db => ExportQuery.LoadSubmissionsAsync(db, Oct8, Oct8, buildingA)));
    }

    [Fact]
    public async Task Submission_rows_carry_point_sign_round_and_person()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10), CleaningStatus.Issue, issueTags: "wet_floor");

        var row = Assert.Single(await QueryAsync(factory, db => ExportQuery.LoadSubmissionsAsync(db, Oct8, Oct8, null)));

        Assert.Equal(("A", "AR01", "AR01-01", Oct8, Shift.Day), (row.BuildingCode, row.AreaCode, row.SignCode, row.ShiftDate, row.Shift));
        Assert.Equal((new TimeOnly(7, 0), new TimeOnly(9, 0), Placement.OnTime), (row.RoundStart!.Value, row.RoundEnd!.Value, row.Placement));
        Assert.NotNull(row.RoundWindowId);
        Assert.Equal((CleaningStatus.Issue, "wet_floor", false), (row.Status, row.IssueTags, row.IsCover));
        Assert.Equal(("E1001", "สมชาย ใจดี"), (row.EmployeeId, row.DisplayName));
        Assert.False(string.IsNullOrEmpty(row.PointName));
    }

    [Fact]
    public async Task Points_come_with_their_windows_and_skip_deactivated_points_and_areas()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var womenRestroom = await db.Signs.Where(s => s.Code == "AR01-02").Select(s => s.ServicePoint!).SingleAsync();
            womenRestroom.IsActive = false;
            await db.SaveChangesAsync();
        });

        var points = await QueryAsync(factory, db => ExportQuery.LoadPointsAsync(db, null));

        Assert.Equal(new[] { "AR01-01", "AR02-01" }, points.Select(p => p.SignCode));
        Assert.Equal(4, points[0].Windows.Count);
        Assert.Equal((ShiftPattern.DayOnly, 1), (points[1].ShiftPattern, points[1].Windows.Count));

        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Code == "AR02")).IsActive = false;
            await db.SaveChangesAsync();
        });
        Assert.Equal(new[] { "AR01-01" }, (await QueryAsync(factory, db => ExportQuery.LoadPointsAsync(db, null))).Select(p => p.SignCode));
    }
}
