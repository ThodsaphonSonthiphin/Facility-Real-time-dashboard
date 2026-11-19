using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Overview;

public sealed class OverviewTargetRecorder(AppDbContext db, AttendanceSettings settings, TimeProvider clock)
{
    public async Task<TargetImage> ReadImageAsync(CancellationToken ct)
    {
        var buildings = await db.Buildings.AsNoTracking().OrderBy(b => b.Id)
            .Select(b => new TargetBuilding(b.Id, b.Code, b.Name, b.IsActive, UtcMicroseconds(b.CreatedAt))).ToListAsync(ct);
        var areas = await db.Areas.AsNoTracking().OrderBy(a => a.Id)
            .Select(a => new TargetArea(a.Id, a.BuildingId, a.Code, a.Name, a.IsActive, a.ShiftPattern,
                UtcMicroseconds(a.CreatedAt))).ToListAsync(ct);
        // The printed code belongs to the sign. Keep a point without its sign, with an unknown code.
        var points = await (from p in db.ServicePoints.AsNoTracking()
            join sign in db.Signs.AsNoTracking() on p.Id equals sign.ServicePointId into signs
            from sign in signs.DefaultIfEmpty()
            orderby p.Id
            select new TargetPoint(p.Id, p.AreaId, sign == null ? null : sign.Code, p.Name, p.SortOrder,
                p.IsActive, UtcMicroseconds(p.CreatedAt))).ToListAsync(ct);
        var windows = await db.PointRoundWindows.AsNoTracking().OrderBy(w => w.Id)
            .Select(w => new TargetWindow(w.Id, w.ServicePointId, w.Shift, w.StartTime, w.EndTime,
                UtcMicroseconds(w.CreatedAt))).ToListAsync(ct);
        return new TargetImage(1, new(settings.OpensMinutesBeforeShift, settings.ClosesMinutesAfterShift),
            buildings, areas, points, windows);
    }

    /// <summary>The caller holds the state row lock until both the setup changes and revision commit.</summary>
    public Task<long> CaptureAsync(CancellationToken ct, DateTime? coverageGapFromUtc = null) =>
        CaptureAsync(ct, coverageGapFromUtc, observedImage: null);

    internal async Task<long> CaptureAsync(CancellationToken ct, DateTime? coverageGapFromUtc, TargetImage? observedImage)
    {
        var state = db.OverviewTargetStates.Local.Single(s => s.Id == 1);
        OverviewTargetRevision? head = null;
        if (state.LatestRevisionId is { } id)
        {
            head = await db.OverviewTargetRevisions.FindAsync([id], ct)
                ?? throw new InvalidOperationException("Missing overview head revision");
        }
        var previous = head == null ? null : Deserialize(head);
        var image = observedImage ?? await ReadImageAsync(ct);
        if (head?.SchemaVersion == image.SchemaVersion && previous != null && SameTargets(image, previous))
            return head.Id;

        var instant = UtcMicroseconds(clock.GetUtcNow().UtcDateTime);
        // Replay orders by effective time, then ID. An earlier timestamp cannot supersede the head.
        if (head != null && instant < head.EffectiveAt)
            throw new InvalidOperationException("The capture clock is earlier than the latest overview revision.");

        image = image with
        {
            CaptureStartedAtUtc = previous?.CaptureStartedAtUtc ?? (head == null ? instant : UtcMicroseconds(head.EffectiveAt)),
            CoverageGapFromUtc = coverageGapFromUtc is { } gap ? UtcMicroseconds(gap) : null,
        };
        var json = JsonSerializer.Serialize(image);
        var revision = new OverviewTargetRevision
        {
            EffectiveAt = instant,
            SchemaVersion = image.SchemaVersion,
            Payload = json,
            PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
        };
        db.OverviewTargetRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        state.LatestRevisionId = revision.Id;
        await db.SaveChangesAsync(ct);
        return revision.Id;
    }

    internal static TargetImage Deserialize(OverviewTargetRevision revision) =>
        JsonSerializer.Deserialize<TargetImage>(revision.Payload) ?? throw new JsonException("Missing target image");

    internal static bool SameTargets(TargetImage first, TargetImage second) =>
        CanonicalTargets(first) == CanonicalTargets(second);

    private static string CanonicalTargets(TargetImage image) =>
        JsonSerializer.Serialize(image with { CaptureStartedAtUtc = null, CoverageGapFromUtc = null });

    private static DateTime UtcMicroseconds(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
}
