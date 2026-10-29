import type { Alert, AlertKind } from '../../shared/api/alertListApi';
import type { BoardRow } from '../../shared/api/attendanceBoardApi';
import type { Shift } from '../../shared/api/types';
import { eventLabel, shiftShortLabel } from '../../shared/utils/labels';
import { addDays } from '../../shared/utils/dateRange';
import { formatDate, formatShiftDate, formatTime, thaiToday } from '../../shared/utils/thaiTime';

/** The design file's D3 tabs, one per kind of alert: a scan refused at a sign, a scan far from its sign, a missing time. */
export type AlertTab = 'blocked' | 'far' | 'missing';

export const alertTabs: readonly AlertTab[] = ['blocked', 'far', 'missing'];

export const alertTabLabel: Record<AlertTab, string> = {
  blocked: 'สแกนผิดพื้นที่',
  far: 'ไกลป้าย',
  missing: 'ลืมลงเวลา',
};

/** The line under the tabs: what the kind means and what the Admin does about it (the design file's words). */
export const alertTabHint: Record<AlertTab, string> = {
  blocked:
    'สแกนป้ายที่ไม่ใช่ของตัวเอง ผิดตึก หรือนอกกะ ระบบไม่บันทึกงาน ถ้าพื้นที่ที่สแกนยังไม่มีคน ให้ทำแทนได้ในคลิกเดียว',
  far: 'ลงเวลา ส่งงาน หรือตรวจงานตอนพิกัดอยู่นอกรัศมีป้าย ระบบบันทึกไว้แต่ติดธง คุยเพื่อดูว่าอยู่ที่จุดจริงหรือ GPS คลาด',
  missing: 'ลงเวลาไม่ครบ ปิดเตือนด้วยมือไม่ได้ ต้องเติมเวลาที่หน้าการเข้างาน แล้วเตือนจะปิดเอง',
};

const tabOf: Record<AlertKind, AlertTab> = {
  BlockedScan: 'blocked',
  AttendanceGps: 'far',
  SubmissionGps: 'far',
  InspectionGps: 'far',
  MissingAttendance: 'missing',
};

export function alertTabOf(alert: Pick<Alert, 'kind'>): AlertTab {
  return tabOf[alert.kind];
}

/** The filter inside a tab: "ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด". */
export type ReviewTab = 'open' | 'reviewed' | 'all';

/** The server sends yesterday and today (AlertQuery); the page shows one of the two. */
export type AlertDay = 'yesterday' | 'today';

/** Which of the two Thai calendar days the alert happened on; a missing time's moment is its shift's end. */
export function alertDay(alert: Pick<Alert, 'at'>, today: string): AlertDay | null {
  const date = thaiToday(new Date(alert.at));
  if (date === today) {
    return 'today';
  }
  return date === addDays(today, -1) ? 'yesterday' : null;
}

export function filterAlerts(
  alerts: readonly Alert[],
  { tab, review, day, today }: { tab: AlertTab; review: ReviewTab; day: AlertDay; today: string },
): Alert[] {
  return alerts.filter(
    (alert) =>
      alertTabOf(alert) === tab &&
      alertDay(alert, today) === day &&
      (review === 'all' || alert.reviewed === (review === 'reviewed')),
  );
}

/** What happened, in the design file's words. A Blocked Scan says why it was refused (blockReason). */
export function alertTitle(alert: Alert): string {
  switch (alert.kind) {
    case 'BlockedScan':
      if (alert.blockReason === 'OtherBuilding') {
        return 'สแกนผิดพื้นที่ (ผิดตึก)';
      }
      return alert.blockReason === 'OutsideShift' ? 'สแกนผิดพื้นที่ (นอกกะ)' : 'สแกนผิดพื้นที่';
    case 'AttendanceGps':
      return 'ลงเวลาไกลป้าย';
    case 'SubmissionGps':
      return 'ส่งงานไกลป้าย';
    case 'InspectionGps':
      return 'ตรวจงานไกลป้าย';
    case 'MissingAttendance':
      return 'ลืมลงเวลา';
  }
}

const roleLabel: Record<Alert['person']['role'], string> = { Cleaner: 'แม่บ้าน', Supervisor: 'หัวหน้า', Admin: 'แอดมิน' };

/** The line under the person's name: their role, and a Cleaner's own Area. */
export function alertRole(alert: Alert): string {
  const { role, areaCode } = alert.person;
  return role === 'Cleaner' && areaCode ? `${roleLabel[role]} · ${areaCode}` : roleLabel[role];
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
    // A cover is suggested only while the Area has nobody (facility-0041 rule 4)
    const empty = alert.cover?.areaId != null ? `${alert.areaCode} ยังไม่มีคน` : null;
    return [`สแกน${sign ? `${sign} ของ ${place}` : place}`, empty].filter(Boolean).join(' · ');
  }
  if (alert.kind === 'MissingAttendance') {
    const slot = slotOfShiftEnd(alert.at);
    const missing = `ไม่ได้ลงเวลา${alert.missingEvents.map((event) => eventLabel[event]).join(' และ')}`;
    return `${missing} · ${shiftShortLabel[slot.shift]} ${formatShiftDate(slot.shiftDate)} · ${place}`;
  }
  const distance =
    alert.distanceM == null ? null : `ห่าง ${alert.distanceM} ม.${alert.radiusM == null ? '' : ` (รัศมี ${alert.radiusM} ม.)`}`;
  const accuracy = alert.accuracyM == null ? null : `ความแม่นยำ ±${alert.accuracyM} ม.`;
  return [sign, place, distance, accuracy].filter(Boolean).join(' · ');
}

/** The design file's "ผลของระบบ": a refused scan saved nothing; a far one was saved with a flag; a missing time waits. */
export function alertResult(alert: Alert): { text: string; refused: boolean } {
  if (alert.kind === 'BlockedScan') {
    return { text: 'ไม่บันทึก', refused: true };
  }
  return { text: alert.kind === 'MissingAttendance' ? 'รอเติมเวลา' : 'บันทึก ติดธง', refused: false };
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

export { candidateText, coverNotice } from '../../shared/utils/coverText';

export { AHEAD_DAYS, coverSlots, sameSlot, slotKey, slotText, type CoverSlot } from '../../shared/utils/coverSlots';

/** facility-0084: the cover box's search, by the regular Cleaner's name or the Area's code or name. */
export function matchesSearch(row: BoardRow, search: string): boolean {
  const words = search.trim().toLowerCase();
  if (words === '') {
    return true;
  }
  return [row.areaCode, row.areaName, row.cleaner?.displayName ?? ''].some((text) => text.toLowerCase().includes(words));
}
