using System.Globalization;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>Words and times in an audit row's summary, which the Admin history page shows as is (facility-0057).</summary>
internal static class ThaiLabels
{
    public static string ShiftName(Shift shift) => shift == Shift.Day ? "เช้า" : "ดึก";

    public static string EventName(AttendanceEvent eventType) => eventType switch
    {
        AttendanceEvent.ShiftIn => "เข้างาน",
        AttendanceEvent.BreakOut => "ออกพัก",
        AttendanceEvent.BreakIn => "กลับจากพัก",
        _ => "เลิกงาน",
    };

    /// <summary>A stored UTC time as Thai wall-clock "HH:mm".</summary>
    public static string Clock(DateTime utc) => ThaiTime.FromUtc(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Gregorian yyyy-MM-dd whatever the server's culture; th-TH would print the Buddhist year.</summary>
    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string AlertName(AlertKind kind) => kind switch
    {
        AlertKind.BlockedScan => "สแกนนอก Area",
        AlertKind.AttendanceGps => "ลงเวลาไกลจากป้าย",
        AlertKind.SubmissionGps => "ส่งงานไกลจากป้าย",
        _ => "ไม่ได้ลงเวลาเลิกงาน",
    };
}
