using System.Globalization;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>Words and times in an audit row's summary, which the Admin history page shows as is (facility-0057).</summary>
internal static class ThaiLabels
{
    public static string ShiftName(Shift shift) => shift == Shift.Day ? "เช้า" : "ดึก";

    public static string RoleName(UserRole role) => role switch
    {
        UserRole.Cleaner => "แม่บ้าน",
        UserRole.Supervisor => "หัวหน้า",
        _ => "แอดมิน",
    };

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

    /// <summary>facility-0085: a Blocked Scan named by why it was refused, as the Admin's pages say it.</summary>
    public static string BlockedName(BlockReason reason) => reason switch
    {
        BlockReason.OtherBuilding => "สแกนผิดตึก",
        BlockReason.OutsideShift => "สแกนนอกกะ",
        _ => "สแกนผิดพื้นที่",
    };

    public static string AlertName(AlertKind kind, BlockReason? reason = null) => kind switch
    {
        AlertKind.BlockedScan => BlockedName(reason ?? BlockReason.OtherArea),
        AlertKind.AttendanceGps => "ลงเวลาไกลจากป้าย",
        AlertKind.SubmissionGps => "ส่งงานไกลจากป้าย",
        AlertKind.InspectionGps => "ตรวจงานไกลจากป้าย",
        _ => "ไม่ได้ลงเวลาเลิกงาน",
    };

    /// <summary>facility-0047: ผล of a submission on the export.</summary>
    public static string SubmissionResult(Placement placement, int? lateMinutes) => placement switch
    {
        Placement.OnTime => "ตรงเวลา",
        Placement.Late => $"ส่งช้า {lateMinutes ?? 0} นาที",
        Placement.Rework => "ส่งงานแก้",
        _ => "ส่งเพิ่ม (ไม่นับรอบ)",
    };

    /// <summary>What the phone says about a Blocked Scan (facility-0041, 0048).</summary>
    public static string BlockedMessage(BlockReason reason) => reason switch
    {
        BlockReason.OtherBuilding => "จุดนี้ไม่ได้อยู่ในตึกของคุณ",
        BlockReason.OutsideShift => "ตอนนี้ไม่ใช่กะของคุณ ตรวจได้เฉพาะในกะของตัวเอง",
        _ => "ป้ายนี้ไม่ใช่พื้นที่ของคุณ",
    };

    /// <summary>facility-0029: ผล of an inspection on the export, as the Supervisor's buttons say it.</summary>
    public static string InspectionResultName(InspectionResult result) => result == InspectionResult.Passed ? "สะอาด" : "ต้องแก้ไข";

    /// <summary>facility-0047 rule 5: a finished round's line on the inspection sheet.</summary>
    public static string RoundOutcomeName(RoundOutcome outcome) => outcome switch
    {
        RoundOutcome.NotInspected => "ไม่ได้ตรวจ",
        RoundOutcome.NotReworked => "ไม่ได้แก้",
        _ => "ตรวจผ่าน",
    };

    /// <summary>facility-0053: distance and verdict only, never coordinates.</summary>
    public static string Gps(short? distanceM, bool? withinRadius) => withinRadius switch
    {
        true => $"ในรัศมี {distanceM} ม.",
        false => $"นอกรัศมี {distanceM} ม.",
        null => "-",
    };
}
