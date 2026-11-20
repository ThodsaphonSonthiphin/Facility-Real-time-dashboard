using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Overview;

public sealed class OverviewTargetRecorder(AppDbContext db, AttendanceSettings settings, TimeProvider clock)
{
    private const int CurrentSchemaVersion = 1;
    private const int MaxCachedImages = 128;
    private static readonly JsonSerializerOptions ReadOptions = new() { RespectRequiredConstructorParameters = true };
    private static readonly ConcurrentDictionary<(long Id, int SchemaVersion, string PayloadHash, string RawHash), TargetImage> VerifiedImageCache = new();
    private static readonly ConcurrentDictionary<(string DbKey, long Id, DateTime EffectiveAt, int SchemaVersion, string PayloadHash), DecodedRevisionCacheEntry> VerifiedDbRevisionCache = new();

    private sealed record DecodedRevisionCacheEntry(TargetRevision? Winner, DateTime? StructuralGapFromUtc);

    internal sealed record RevisionMetadata(long Id, DateTime EffectiveAt, int SchemaVersion, string PayloadHash);

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
        return new TargetImage(CurrentSchemaVersion, new(settings.OpensMinutesBeforeShift, settings.ClosesMinutesAfterShift),
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
            // A legacy head may be a retained predecessor, so its timestamp cannot establish the original start.
            CaptureStartedAtUtc = head == null ? instant : previous?.CaptureStartedAtUtc,
            CoverageGapFromUtc = coverageGapFromUtc is { } gap ? UtcMicroseconds(gap) : null,
        };
        var json = JsonSerializer.Serialize(image);
        var revision = new OverviewTargetRevision
        {
            EffectiveAt = instant,
            SchemaVersion = image.SchemaVersion,
            Payload = json,
            PayloadHash = Hash(json),
        };
        db.OverviewTargetRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        state.LatestRevisionId = revision.Id;
        await db.SaveChangesAsync(ct);
        return revision.Id;
    }

    /// <summary>Decodes effective images and independently preserves evidence of earlier structural gaps.</summary>
    public static (TargetRevision[] Revisions, (DateTime From, DateTime To)[] UnreadableIntervals,
        (DateTime From, DateTime To)[] StructuralGaps) DecodeForRead(IEnumerable<OverviewTargetRevision> rows, ILogger logger)
    {
        var groups = rows.OrderBy(row => row.EffectiveAt).ThenBy(row => row.Id)
            .GroupBy(row => DateTime.SpecifyKind(row.EffectiveAt, DateTimeKind.Utc)).ToArray();
        var revisions = new List<TargetRevision>();
        var unreadable = new List<(DateTime From, DateTime To)>();
        var structuralGaps = new List<(DateTime From, DateTime To)>();
        for (var i = 0; i < groups.Length; i++)
        {
            var effectiveAt = groups[i].Key;
            TargetRevision? winner = null;
            foreach (var row in groups[i])
            {
                var image = ReadIntegrityChecked(row);
                if (image?.CoverageGapFromUtc is { } from && from < effectiveAt)
                    structuralGaps.Add((from, effectiveAt));

                if (image == null || !IsReplayable(image, effectiveAt))
                {
                    winner = null;
                    logger.LogWarning("Could not read overview target revision {RevisionId}", row.Id);
                    continue;
                }

                winner = new(row.Id, effectiveAt, image);
            }

            if (winner != null)
                revisions.Add(winner);
            else
                unreadable.Add((effectiveAt, i + 1 < groups.Length ? groups[i + 1].Key : DateTime.MaxValue));
        }
        return (revisions.ToArray(), unreadable.ToArray(), structuralGaps.ToArray());
    }

    internal static async Task<(TargetRevision[] Revisions, (DateTime From, DateTime To)[] UnreadableIntervals,
        (DateTime From, DateTime To)[] StructuralGaps)> LoadAndDecodeForReadAsync(
        AppDbContext db,
        DateTime? predecessorAt,
        DateTime readAt,
        ILogger logger,
        CancellationToken ct)
    {
        var metas = await db.OverviewTargetRevisions.AsNoTracking()
            .Where(r => (predecessorAt == null || r.EffectiveAt >= predecessorAt.Value) && r.EffectiveAt <= readAt)
            .OrderBy(r => r.EffectiveAt).ThenBy(r => r.Id)
            .Select(r => new RevisionMetadata(r.Id, r.EffectiveAt, r.SchemaVersion, r.PayloadHash))
            .ToListAsync(ct);

        if (metas.Count == 0)
        {
            return ([], [], []);
        }

        // Only bypass re-fetching the large JSON payload on MySQL databases keyed by database name.
        var isMySql = string.Equals(db.Database.ProviderName, "MySql.EntityFrameworkCore", StringComparison.Ordinal);
        var dbKey = isMySql ? (db.Database.GetConnectionString() ?? string.Empty) : string.Empty;

        var cachedHits = new Dictionary<long, DecodedRevisionCacheEntry>();
        var missingIds = new List<long>();
        if (!isMySql)
        {
            missingIds.AddRange(metas.Select(m => m.Id));
        }
        else
        {
            foreach (var meta in metas)
            {
                var key = (dbKey, meta.Id, DateTime.SpecifyKind(meta.EffectiveAt, DateTimeKind.Utc), meta.SchemaVersion, meta.PayloadHash);
                if (VerifiedDbRevisionCache.TryGetValue(key, out var hit))
                {
                    cachedHits[meta.Id] = hit;
                }
                else
                {
                    missingIds.Add(meta.Id);
                }
            }
        }

        Dictionary<long, OverviewTargetRevision>? fetchedById = null;
        if (missingIds.Count > 0)
        {
            fetchedById = await db.OverviewTargetRevisions.AsNoTracking()
                .Where(r => missingIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, ct);
        }

        var groups = metas.OrderBy(row => row.EffectiveAt).ThenBy(row => row.Id)
            .GroupBy(row => DateTime.SpecifyKind(row.EffectiveAt, DateTimeKind.Utc)).ToArray();
        var revisions = new List<TargetRevision>();
        var unreadable = new List<(DateTime From, DateTime To)>();
        var structuralGaps = new List<(DateTime From, DateTime To)>();

        for (var i = 0; i < groups.Length; i++)
        {
            var effectiveAt = groups[i].Key;
            TargetRevision? winner = null;
            foreach (var meta in groups[i])
            {
                DecodedRevisionCacheEntry entry;
                if (cachedHits.TryGetValue(meta.Id, out var cached))
                {
                    entry = cached;
                }
                else if (fetchedById is not null && fetchedById.TryGetValue(meta.Id, out var row))
                {
                    var image = ReadIntegrityChecked(row);
                    DateTime? gapFrom = image?.CoverageGapFromUtc is { } from && from < effectiveAt ? from : null;
                    TargetRevision? decoded = null;
                    if (image == null || !IsReplayable(image, effectiveAt))
                    {
                        logger.LogWarning("Could not read overview target revision {RevisionId}", row.Id);
                    }
                    else
                    {
                        decoded = new(row.Id, effectiveAt, image);
                    }

                    entry = new DecodedRevisionCacheEntry(decoded, gapFrom);
                    if (isMySql && decoded is not null)
                    {
                        if (VerifiedDbRevisionCache.Count >= MaxCachedImages)
                        {
                            VerifiedDbRevisionCache.Clear();
                        }
                        VerifiedDbRevisionCache.TryAdd(
                            (dbKey, meta.Id, effectiveAt, meta.SchemaVersion, meta.PayloadHash),
                            entry);
                    }
                }
                else
                {
                    logger.LogWarning("Could not read overview target revision {RevisionId}", meta.Id);
                    entry = new DecodedRevisionCacheEntry(null, null);
                }

                if (entry.StructuralGapFromUtc is { } cachedGap)
                {
                    structuralGaps.Add((cachedGap, effectiveAt));
                }

                if (entry.Winner is null)
                {
                    winner = null;
                    continue;
                }

                winner = entry.Winner;
            }

            if (winner != null)
            {
                revisions.Add(winner);
            }
            else
            {
                unreadable.Add((effectiveAt, i + 1 < groups.Length ? groups[i + 1].Key : DateTime.MaxValue));
            }
        }

        return (revisions.ToArray(), unreadable.ToArray(), structuralGaps.ToArray());
    }

    private static TargetImage? ReadIntegrityChecked(OverviewTargetRevision row)
    {
        if (row.SchemaVersion != CurrentSchemaVersion)
            return null;

        var rawHash = Hash(row.Payload);
        var cacheKey = (row.Id, row.SchemaVersion, row.PayloadHash, rawHash);
        if (VerifiedImageCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var image = JsonSerializer.Deserialize<TargetImage>(row.Payload, ReadOptions);
            if (image != null && image.SchemaVersion == row.SchemaVersion
                // MySQL's JSON column may reorder properties and whitespace on storage.
                && (string.Equals(row.PayloadHash, rawHash, StringComparison.OrdinalIgnoreCase)
                    || HashMatches(row, JsonSerializer.Serialize(image))))
            {
                if (VerifiedImageCache.Count >= MaxCachedImages)
                    VerifiedImageCache.Clear();
                VerifiedImageCache.TryAdd(cacheKey, image);
                return image;
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsReplayable(TargetImage image, DateTime effectiveAt) =>
        image.Policy is
        {
            OpensMinutesBeforeShift: >= 0 and <= AttendanceSettings.MaxReceivingOffsetMinutes,
            ClosesMinutesAfterShift: >= 0 and <= AttendanceSettings.MaxReceivingOffsetMinutes,
        }
        && !(image.CaptureStartedAtUtc > effectiveAt || image.CoverageGapFromUtc > effectiveAt)
        && IsStructurallyValid(image);

    private static bool IsStructurallyValid(TargetImage image)
    {
        var buildings = new HashSet<int>();
        var areas = new HashSet<int>();
        var points = new HashSet<int>();
        var windows = new HashSet<int>();

        return image.Buildings?.All(building => building is { Id: > 0, Code: not null, Name: not null }
                && buildings.Add(building.Id)) == true
            && image.Areas?.All(area => area is { Id: > 0, Code: not null, Name: not null }
                && buildings.Contains(area.BuildingId) && Enum.IsDefined(area.ShiftPattern) && areas.Add(area.Id)) == true
            && image.Points?.All(point => point is { Id: > 0, Name: not null }
                && areas.Contains(point.AreaId) && points.Add(point.Id)) == true
            && image.Windows?.All(window => window is { Id: > 0 }
                && points.Contains(window.PointId) && Enum.IsDefined(window.Shift) && windows.Add(window.Id)) == true;
    }

    private static bool HashMatches(OverviewTargetRevision row, string json) =>
        string.Equals(row.PayloadHash, Hash(json), StringComparison.OrdinalIgnoreCase);

    private static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    internal static TargetImage Deserialize(OverviewTargetRevision revision) =>
        JsonSerializer.Deserialize<TargetImage>(revision.Payload) ?? throw new JsonException("Missing target image");

    internal static bool SameTargets(TargetImage first, TargetImage second) =>
        CanonicalTargets(first) == CanonicalTargets(second);

    private static string CanonicalTargets(TargetImage image) =>
        JsonSerializer.Serialize(image with { CaptureStartedAtUtc = null, CoverageGapFromUtc = null });

    private static DateTime UtcMicroseconds(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
}
