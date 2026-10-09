using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>database.html: a removed Round Window leaves its Scan Records with round_window_id NULL and their copied round times.</summary>
public static class RoundWindowRemoval
{
    /// <summary>
    /// Marks the windows deleted; the caller saves. Their Scan Records are loaded first so EF sets the link to NULL itself,
    /// which holds even where the database does not enforce the foreign key (SQLite in the API tests).
    /// </summary>
    public static async Task RemoveAsync(AppDbContext db, IReadOnlyCollection<PointRoundWindow> windows)
    {
        if (windows.Count == 0)
        {
            return;
        }

        var ids = windows.Select(w => w.Id).ToList();
        await db.ScanRecords.Where(s => s.RoundWindowId != null && ids.Contains(s.RoundWindowId.Value)).LoadAsync();
        db.PointRoundWindows.RemoveRange(windows);
    }
}
