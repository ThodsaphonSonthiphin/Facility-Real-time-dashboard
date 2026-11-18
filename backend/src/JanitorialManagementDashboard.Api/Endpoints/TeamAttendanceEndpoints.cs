using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Rounds;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>
/// Spec §8 and §11: <c>POST /api/team-attendance/missing</c> — the scoped way a Supervisor fills a missing actual time that
/// was confirmed as a fact, with a reason. It is deliberately not a second Admin correction route: the Admin keeps its
/// existing wider rights at <c>PUT /api/admin/attendance</c>, and this route never overwrites a scan, never invents a time
/// for a skip marker, and only answers for a person whose slot's effective duty Area falls inside the actor's dated scope.
/// <para>
/// The group carries <see cref="AuthSetup.ScheduleManager"/>, so an anonymous caller is a 401 and a Cleaner a 403 before the
/// body is bound; who may be corrected is decided per slot afterwards by <see cref="ScheduleAuthority.CanManage"/>.
/// </para>
/// </summary>
public static class TeamAttendanceEndpoints
{
    /// <summary>Spec §11: the scoped missing-time route.</summary>
    public const string MissingPath = "/api/team-attendance/missing";

    private const string NotAuthorised = "ไม่มีสิทธิ์บันทึกเวลาของแม่บ้านคนนี้";

    /// <summary>One answer for every reason the actor may not touch this person: it never reveals their schedule.</summary>
    private const string AlreadyRecorded = "มีการลงเวลารายการนี้แล้ว ไม่เขียนทับเวลาที่สแกนไว้";

    public static IEndpointRouteBuilder MapTeamAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(MissingPath, AddMissingAsync).RequireAuthorization(AuthSetup.ScheduleManager);
        return app;
    }

    private static async Task<IResult> AddMissingAsync(
        AddMissingAttendanceRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        ScheduleStore store,
        ScheduleScopeReader scopes,
        TimeProvider clock,
        AttendanceSettings settings,
        IHubContext<ScanHub> hub,
        ILogger<ScanHub> logger,
        CancellationToken ct)
    {
        var actor = await CurrentUser.LoadAsync(principal, db);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (request.ShiftDate is not { } shiftDate
            || request.Shift is not { } shift || !Enum.IsDefined(shift)
            || request.EventType is not { } eventType || !Enum.IsDefined(eventType))
        {
            return Bad("ต้องระบุวันที่ กะ และรายการที่จะเพิ่ม");
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return Bad("ต้องกรอกเหตุผล");
        }

        if (reason.Length > AdminAttendanceEndpoints.MaxReasonLength)
        {
            return Bad($"เหตุผลยาวได้ไม่เกิน {AdminAttendanceEndpoints.MaxReasonLength} ตัวอักษร");
        }

        if (!RoundWindowRules.TryParseTime(request.Time, out var time))
        {
            return Bad("กรอกชั่วโมงและนาทีให้ครบ เช่น 06:58");
        }

        var expected = request.ExpectedRevision?.Trim();
        if (string.IsNullOrEmpty(expected))
        {
            return Bad("ต้องส่งรุ่นข้อมูลล่าสุดที่อ่านมากับคำขอ");
        }

        var person = await db.Users.Include(user => user.Area)
            .FirstOrDefaultAsync(user => user.Id == request.UserId && user.Role == UserRole.Cleaner && user.IsActive, ct);
        if (person is null)
        {
            return Bad("ต้องเลือกแม่บ้านที่ใช้งานอยู่");
        }

        var slot = new ShiftSlot(shiftDate, shift);

        // Spec §7 and ruling A: the slot's effective duty Area is what an actual fact belongs to, and its building is what a
        // Supervisor's dated scope is asked about. A person with no duty Area cannot be corrected into one.
        var duty = await EffectiveDutyReader.ReadAsync(db, store, person, slot, clock.GetUtcNow(), ct);
        if (duty.AreaId is not { } areaId)
        {
            // Nothing to correct into. A Supervisor is not told even that much about somebody outside their scope: the answer
            // is the same one an out-of-scope slot gets, so this route cannot be used to probe another team's people.
            return actor.Role == UserRole.Admin
                ? Bad("แม่บ้านคนนี้ยังไม่มีพื้นที่")
                : ApiResults.Message(StatusCodes.Status403Forbidden, NotAuthorised);
        }

        if (actor.Role != UserRole.Admin)
        {
            // A Supervisor only inside one of their dated periods, decided by the one authority predicate every
            // management route uses. Anyone else is refused without being told anything about the person's schedule.
            var buildingId = await db.Areas.Where(area => area.Id == areaId)
                .Select(area => (int?)area.BuildingId)
                .FirstOrDefaultAsync(ct);
            var periods = await scopes.ReadAsync(actor.Id, ct);
            if (buildingId is not { } building
                || !ScheduleAuthority.CanManage(actor.Role, actor.Id, building, shift, shiftDate, periods))
            {
                return ApiResults.Message(StatusCodes.Status403Forbidden, NotAuthorised);
            }
        }

        var facts = await AttendanceEndpoints.LoadEventsAsync(db, person.Id, slot);

        // Spec §8: an existing actual scan is never overwritten here; the Admin edit route stays the only overwrite path.
        if (facts.Any(fact => fact.EventType == eventType))
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, AlreadyRecorded);
        }

        if (AttendanceCalendar.PlaceTime(slot, time, settings) is not { } at)
        {
            return Bad($"เวลานี้อยู่นอกช่วงลงเวลาของกะนี้ ({settings.OpensMinutesBeforeShift} นาทีก่อนกะถึง {settings.ClosesMinutesAfterShift} นาทีหลังกะ)");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (at > now)
        {
            return Bad("เวลาที่ใส่ยังมาไม่ถึง");
        }

        var version = AttendanceLifecycle.VersionFor(await db.AttendanceSlotLifecycles.AsNoTracking()
            .Where(row => row.ShiftDate == slot.ShiftDate && row.Shift == slot.Shift)
            .Select(row => (AttendanceLifecycleVersion?)row.Version)
            .FirstOrDefaultAsync(ct));

        var proposed = facts.ToDictionary(fact => fact.EventType, fact => fact.OccurredAt);
        proposed[eventType] = at;
        if (AttendanceRules.CheckTimes(version, proposed) is { } orderError)
        {
            return Bad(orderError);
        }

        // Spec §11: the revision the actor read has to be the slot's current one, so a correction is never written against a
        // state somebody else has already changed.
        var revision = AttendanceStateRevision.Of(person.Id, slot, facts);
        if (!string.Equals(expected, revision, StringComparison.Ordinal))
        {
            var current = await AttendanceEndpoints.StateForSlotAsync(db, store, person, slot, now, ct);
            return Results.Json(
                new AttendanceStateConflictDto(
                    "ข้อมูลการลงเวลาเปลี่ยนไประหว่างที่คุณเปิดหน้า โหลดใหม่แล้วลองอีกครั้ง",
                    AttendanceEndpoints.RefreshRequired,
                    current),
                statusCode: StatusCodes.Status409Conflict);
        }

        var areaCode = await db.Areas.Where(area => area.Id == areaId).Select(area => area.Code).FirstOrDefaultAsync(ct);
        var summary = $"เพิ่มเวลา{ThaiLabels.EventName(eventType)}ของ {person.DisplayName} {areaCode} "
            + $"กะ{ThaiLabels.ShiftName(shift)} {ThaiLabels.Date(shiftDate)}: – → {ThaiLabels.Clock(at)}";

        var row = new ShiftAttendance
        {
            UserId = person.Id,
            AreaId = areaId,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            EventType = eventType,
            OccurredAt = at,
            Source = AttendanceSource.SupervisorAdd,
            SignId = null,
            CreatedAt = now,
        };

        var written = await AttendanceWrites.RecordAsync(
            db, row, [], areaId, actor.Id, now,
            async saved =>
            {
                AuditTrail.Add(db, actor.Id, now, "ATTENDANCE_ADD", "shift_attendances", saved.Id, summary,
                    before: null, after: Snapshot(saved), reason);
                await db.SaveChangesAsync(ct);
            },
            ct);

        if (!written.Stored)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, AlreadyRecorded);
        }

        await AttendanceBoard.NotifyAsync(hub, db, logger, areaId, slot);
        return Results.Ok(await AttendanceEndpoints.StateForSlotAsync(db, store, person, slot, now, ct));
    }

    private static object Snapshot(ShiftAttendance row) => new
    {
        EventType = row.EventType.ToString(),
        OccurredAt = DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc),
        Source = row.Source.ToString(),
    };

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
