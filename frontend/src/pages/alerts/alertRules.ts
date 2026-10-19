import type { Alert, AlertKind } from '../../shared/api/alertListApi';
import type { Shift } from '../../shared/api/types';
import { eventLabel, shiftShortLabel } from '../../shared/utils/labels';
import { formatDate, formatShiftDate, formatTime } from '../../shared/utils/thaiTime';

/** Wireframe D3's tabs: "ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด". */
export type ReviewTab = 'open' | 'reviewed' | 'all';

/** Wireframe D3's type picker: the three GPS flags are one choice. */
export type AlertGroup = 'all' | 'blocked' | 'far' | 'missing';

export const alertGroupLabel: Record<AlertGroup, string> = {
  all: 'ทุกประเภท',
  blocked: 'สแกนแล้วถูกบล็อก',
  far: 'ไกลจากป้าย',
  missing: 'ไม่ได้ลงเวลา',
};

const groupOf: Record<AlertKind, Exclude<AlertGroup, 'all'>> = {
  BlockedScan: 'blocked',
  AttendanceGps: 'far',
  SubmissionGps: 'far',
  InspectionGps: 'far',
  MissingAttendance: 'missing',
};

export function filterAlerts(alerts: readonly Alert[], tab: ReviewTab, group: AlertGroup): Alert[] {
  return alerts.filter(
    (alert) =>
      (tab === 'all' || alert.reviewed === (tab === 'reviewed')) && (group === 'all' || groupOf[alert.kind] === group),
  );
}

/** What happened. A Blocked Scan says why it was refused (blockReason), so a Supervisor's is never "สแกนนอก Area". */
export function alertTitle(alert: Alert): string {
  switch (alert.kind) {
    case 'BlockedScan':
      if (alert.blockReason === 'OtherBuilding') {
        return 'สแกนนอกตึก (ถูกบล็อก)';
      }
      return alert.blockReason === 'OutsideShift' ? 'สแกนนอกกะ (ถูกบล็อก)' : 'สแกนนอก Area (ถูกบล็อก)';
    case 'AttendanceGps':
      return 'ลงเวลาไกลจากป้าย';
    case 'SubmissionGps':
      return 'ส่งงานไกลจากป้าย';
    case 'InspectionGps':
      return 'ตรวจงานไกลจากป้าย';
    case 'MissingAttendance':
      return `ไม่ได้ลงเวลา${alert.missingEvents.map((event) => eventLabel[event]).join(' และ')}`;
  }
}

/** Who, with Supervisors marked as wireframe D3 does. */
export function alertWho(alert: Alert): string {
  return alert.person.role === 'Supervisor' ? `${alert.person.displayName} (หัวหน้า)` : alert.person.displayName;
}

/** The sign, the place and the distance, as far as the server knows them. */
export function alertDetail(alert: Alert): string {
  const place = `${alert.areaCode} ${alert.areaName}`;
  const sign = alert.signCode
    ? alert.pointName
      ? `ป้าย ${alert.signCode} ${alert.pointName}`
      : `ป้ายลงเวลา ${alert.signCode}`
    : null;
  if (alert.kind === 'BlockedScan') {
    const own =
      alert.person.role === 'Cleaner' && alert.person.areaCode
        ? `Area ของ${alert.person.displayName}คือ ${alert.person.areaCode}`
        : null;
    return [sign ? `${sign} ของ ${place}` : place, own, 'ไม่ได้บันทึกงาน'].filter(Boolean).join(' · ');
  }
  if (alert.kind === 'MissingAttendance') {
    const slot = slotOfShiftEnd(alert.at);
    return `${shiftShortLabel[slot.shift]} ${formatShiftDate(slot.shiftDate)} · ${place}`;
  }
  const distance =
    alert.distanceM == null ? null : `ห่าง ${alert.distanceM} ม.${alert.radiusM == null ? '' : ` (รัศมี ${alert.radiusM} ม.)`}`;
  const accuracy = alert.accuracyM == null ? null : `ความแม่นยำ ±${alert.accuracyM} ม.`;
  return [sign, place, distance, accuracy].filter(Boolean).join(' · ');
}

/**
 * A missing-attendance alert's time is its shift's end (AlertQuery): 19:00 ends that date's Day shift, 07:00 the Night
 * shift of the date before (facility-0040).
 */
export function slotOfShiftEnd(at: string): { shiftDate: string; shift: Shift } {
  const [day, month, year] = formatDate(at).split('/').map(Number);
  return formatTime(at) === '19:00'
    ? { shiftDate: isoDate(year, month, day), shift: 'Day' }
    : { shiftDate: isoDate(year, month, day - 1), shift: 'Night' };
}

function isoDate(year: number, month: number, day: number): string {
  return new Date(Date.UTC(year, month - 1, day)).toISOString().slice(0, 10);
}

/** D2 with that shift and the correction form open on the first missing event (plan 11 decision 9). Built from server ids and enums only. */
export function fillLink(alert: Alert): string {
  const slot = slotOfShiftEnd(alert.at);
  const params = new URLSearchParams({
    date: slot.shiftDate,
    shift: slot.shift,
    fill: String(alert.person.id),
    event: alert.missingEvents[0] ?? 'ShiftOut',
  });
  return `/admin/attendance?${params.toString()}`;
}
