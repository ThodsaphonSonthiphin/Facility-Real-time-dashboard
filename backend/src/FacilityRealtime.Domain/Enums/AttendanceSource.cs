namespace FacilityRealtime.Domain.Enums;

/// <summary>facility-0051: Admin additions and edits carry a badge; the original values live in audit_log.</summary>
public enum AttendanceSource
{
    Scan,
    AdminAdd,
    AdminEdit,
}
