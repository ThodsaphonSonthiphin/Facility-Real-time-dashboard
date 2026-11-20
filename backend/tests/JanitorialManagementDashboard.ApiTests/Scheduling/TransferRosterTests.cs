using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

public class TransferRosterTests
{
    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task A_destination_supervisor_can_discover_and_open_an_inbound_regular_cleaners_slot()
    {
        using var factory = TransferApi.NewFactory();
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var destination = await TestData.AddAreaAsync(factory, "AR03", "B");
        var unrelated = await TestData.AddAreaAsync(factory, "AR04", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        var (otherCleaner, otherAuth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000003", "0810000003");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var otherOrigin = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        await TransferApi.CheckInAsync(factory, otherCleaner, TransferApi.Thai(7, 10), "token-checkin-ar02");
        await TransferApi.InstructForIdAsync(factory, admin, otherAuth.User.Id, otherOrigin, unrelated, TransferApi.Thai(12, 55));
        var id = await TransferApi.InstructForIdAsync(factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        const string week = "/api/schedules?weekStart=2026-10-05";
        var pending = (await supervisor.GetFromJsonAsync<ScheduleWeekDto>($"{week}&buildingId={buildingB}", ResponseJson))!;
        Assert.Empty(pending.Rows);
        Assert.Equal("available", pending.Availability);
        var incoming = Assert.Single(pending.TransferSlots!);
        Assert.Equal(auth.User.Id, incoming.UserId);
        Assert.Equal(id, incoming.Id);
        Assert.Equal("AR01", incoming.FromAreaCode);
        Assert.Equal("AR03", incoming.ToAreaCode);
        Assert.Equal(new DateOnly(2026, 10, 8), incoming.ShiftDate);
        Assert.Equal(Shift.Day, incoming.Shift);
        Assert.Null(incoming.ConfirmedAt);

        await TransferApi.ReadConfirmAsync(await TransferApi.ConfirmAsync(factory, cleaner, id, incoming.Revision,
            at: TransferApi.Thai(13, 5)));
        var confirmed = (await supervisor.GetFromJsonAsync<ScheduleWeekDto>($"{week}&buildingId={buildingB}", ResponseJson))!;
        Assert.Empty(confirmed.Rows);
        Assert.NotEqual(pending.Revision, confirmed.Revision);
        Assert.Equal(TransferApi.Thai(13, 5), Assert.Single(confirmed.TransferSlots!).ConfirmedAt);

        var slotPath = $"/api/team-attendance/{incoming.UserId}?shiftDate=2026-10-08&shift=Day";
        var slot = (await supervisor.GetFromJsonAsync<AttendanceStateDto>(slotPath, ResponseJson))!;
        Assert.Equal(destination, slot.DutyAreaId);
        Assert.Equal(id, slot.Transfer!.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.GetAsync(
            $"/api/team-attendance/{otherAuth.User.Id}?shiftDate=2026-10-08&shift=Day")).StatusCode);

        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var wrongBuilding = (await supervisor.GetFromJsonAsync<ScheduleWeekDto>($"{week}&buildingId={buildingA}", ResponseJson))!;
        var wrongShift = (await supervisor.GetFromJsonAsync<ScheduleWeekDto>($"{week}&shift=Night", ResponseJson))!;
        Assert.True(wrongBuilding.TransferSlots is null or { Count: 0 });
        Assert.True(wrongShift.TransferSlots is null or { Count: 0 });
    }
}
