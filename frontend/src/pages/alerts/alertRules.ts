import type { Alert, AlertKind } from '../../shared/api/alertListApi';
import type { BoardRow } from '../../shared/api/attendanceBoardApi';
import type { Shift } from '../../shared/api/types';
import { addDays } from '../../shared/utils/dateRange';
import { eventLabel, shiftShortLabel } from '../../shared/utils/labels';
import { formatDate, formatShiftDate, formatTime, formatWeekday } from '../../shared/utils/thaiTime';
import type { CoverAssignment, CoverCandidate } from './alertsApi';

/** Wireframe D3's tabs: "ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด". */
export type ReviewTab = 'open' | 'reviewed' | 'all';

/** Wireframe D3's type picker: the three GPS flags are one choice. */
export type AlertGroup = 'all' | 'blocked' | 'far' | 'missing';

export const alertGroupLabel: Record<AlertGroup, string> = {
  all: 'ทุกประเภท',
  blocked: 'สแกนผิดพื้นที่ / ผิดตึก / นอกกะ',
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

/** What happened. A Blocked Scan says why it was refused (blockReason), so a Supervisor's is never "สแกนผิดพื้นที่". */
export function alertTitle(alert: Alert): string {
  switch (alert.kind) {
    case 'BlockedScan':
      if (alert.blockReason === 'OtherBuilding') {
        return 'สแกนผิดตึก';
      }
      return alert.blockReason === 'OutsideShift' ? 'สแกนนอกกะ' : 'สแกนผิดพื้นที่';
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
        ? `พื้นที่ของ${alert.person.displayName}คือ ${alert.person.areaCode}`
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

/**
 * One choice of "ให้ใครทำแทน": who, their own Area, their Shift-In, and whether they already scanned this Area's signs.
 * For a shift not started nobody has checked in or scanned, so only who and their Area are shown.
 */
export function candidateText(candidate: CoverCandidate, ahead = false): string {
  if (ahead) {
    return `${candidate.displayName} · ${candidate.areaCode}`;
  }
  return [
    candidate.displayName,
    candidate.areaCode,
    candidate.shiftInAt ? `เข้างานแล้ว ${formatTime(candidate.shiftInAt)}` : 'ยังไม่ลงเวลา',
    candidate.scannedThisAreaAt ? `สแกนป้ายพื้นที่นี้เมื่อ ${formatTime(candidate.scannedThisAreaAt)}` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}

/** What the page says once a cover is made: an Area for a Cleaner, a building for a Supervisor. */
export function coverNotice(cover: CoverAssignment): string {
  return cover.areaCode
    ? `มอบหมาย ${cover.personName} ทำแทน ${cover.areaCode} แล้ว`
    : `มอบหมาย ${cover.personName} ตรวจแทนตึก ${cover.buildingCode ?? ''} แล้ว`;
}

/** A shift the cover box shows (facility-0075). */
export interface CoverSlot {
  shiftDate: string;
  shift: Shift;
}

/** facility-0075: covers are assigned up to 7 days ahead, so the night and the weekend never wait for an Admin. */
export const AHEAD_DAYS = 7;

/** From the shift on the clock to the Night shift 7 days later, in the order they come. */
export function coverSlots(current: CoverSlot): CoverSlot[] {
  const slots: CoverSlot[] = [];
  for (let day = 0; day <= AHEAD_DAYS; day += 1) {
    const shiftDate = addDays(current.shiftDate, day);
    for (const shift of ['Day', 'Night'] as const) {
      // On the first date a Night shift on the clock has already passed that date's Day shift
      if (day > 0 || current.shift === 'Day' || shift === 'Night') {
        slots.push({ shiftDate, shift });
      }
    }
  }
  return slots;
}

export function sameSlot(a: CoverSlot, b: CoverSlot): boolean {
  return a.shiftDate === b.shiftDate && a.shift === b.shift;
}

/** The picker's value, e.g. "2026-10-16:Night". */
export function slotKey(slot: CoverSlot): string {
  return `${slot.shiftDate}:${slot.shift}`;
}

/** "กะดึก ศ. 16/10/2026": the weekday matters, because the Admin thinks "Saturday", not a date. */
export function slotText(slot: CoverSlot): string {
  return `${shiftShortLabel[slot.shift]} ${formatWeekday(slot.shiftDate)} ${formatShiftDate(slot.shiftDate)}`;
}

/** facility-0084: the cover box's search, by the regular Cleaner's name or the Area's code or name. */
export function matchesSearch(row: BoardRow, search: string): boolean {
  const words = search.trim().toLowerCase();
  if (words === '') {
    return true;
  }
  return [row.areaCode, row.areaName, row.cleaner?.displayName ?? ''].some((text) => text.toLowerCase().includes(words));
}
