using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;

namespace JanitorialManagementDashboard.Application.Attendance;

/// <summary>
/// Spec §8 and §11: the marker an attendance page sends back with its write, so a page that read an older slot state is
/// refused with "refresh-required" instead of writing over facts it never saw — <c>stale incompatible page ได้
/// refresh-required ก่อนเขียน</c>.
/// <para>
/// It is derived from the slot's actual facts alone — each fact's identity, action and instant — and never from the plan,
/// so cancelling leave, moving a break group or re-reading the same facts leaves it unchanged while every added, moved or
/// corrected scan changes it. An explicit skip marker is not an actual fact (it carries no time), so it is not part of the
/// token either; the fact recorded alongside it is.
/// </para>
/// </summary>
public static class AttendanceStateRevision
{
    /// <summary>
    /// The revision of one person's slot. The facts are read in identity order rather than arrival order, so two callers
    /// that loaded the same rows agree and the token depends on the facts, not on how a query happened to sort them.
    /// </summary>
    public static string Of(int userId, ShiftSlot slot, IEnumerable<ShiftAttendance> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var facts = events
            .OrderBy(fact => fact.Id)
            .Select(fact => string.Join(':',
                fact.Id.ToString(CultureInfo.InvariantCulture),
                fact.EventType.ToString(),
                fact.OccurredAt.Ticks.ToString(CultureInfo.InvariantCulture)));

        var text = string.Join('|',
            userId.ToString(CultureInfo.InvariantCulture),
            slot.ShiftDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            slot.Shift.ToString(),
            string.Join(';', facts));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}
