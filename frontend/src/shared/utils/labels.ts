import type { AdminRoundWindow, AdminSign, ShiftPattern } from '../api/adminSetupApi';
import type { AttendanceEvent, PointStatus, Shift } from '../api/types';

/** facility-0040. */
export const shiftLabel: Record<Shift, string> = {
  Day: 'กะเช้า 07:00–19:00',
  Night: 'กะดึก 19:00–07:00',
};

/** facility-0031, wireframes M1 and M10. */
export const eventLabel: Record<AttendanceEvent, string> = {
  ShiftIn: 'เข้างาน',
  BreakOut: 'ออกพัก',
  BreakIn: 'กลับจากพัก',
  ShiftOut: 'เลิกงาน',
};

/** The order the four events are shown in. */
export const attendanceEvents: readonly AttendanceEvent[] = ['ShiftIn', 'BreakOut', 'BreakIn', 'ShiftOut'];

/** facility-0047 rule 4: the card statuses as everyone sees them. */
export const pointStatusLabel: Record<PointStatus, string> = {
  Rework: 'ต้องแก้ไข',
  Overdue: 'เลยเวลา',
  PendingInspection: 'รอตรวจ',
  Passed: 'ตรวจผ่าน',
  NotYetDone: 'ยังไม่ทำ',
  BeforeFirstRound: 'ยังไม่ถึงรอบ',
  OffHours: 'นอกเวลา',
};

/** Short shift names for tables and lists. */
export const shiftShortLabel: Record<Shift, string> = {
  Day: 'กะเช้า',
  Night: 'กะดึก',
};

/** facility-0040 rule 4, wireframe D10. */
export const shiftPatternLabel: Record<ShiftPattern, string> = {
  DayAndNight: '2 กะ (เช้า + ดึก)',
  DayOnly: 'เฉพาะกะเช้า',
};

/** facility-0045: where a sign's coordinates came from, as the Admin pages show it (D4, M17). */
export function signPlaceLabel(sign: Pick<AdminSign, 'locationSource' | 'locationAccuracyM'>): string {
  if (sign.locationSource === 'Site') {
    return `หน้างาน ±${sign.locationAccuracyM ?? 0} ม.`;
  }
  return sign.locationSource === 'Map' ? 'ยังไม่ยืนยันหน้างาน (จากแผนที่)' : 'ยังไม่มีพิกัด';
}

/** A point's Round Windows grouped by shift, in the order given: "เช้า 07:00–09:00, 16:00–18:00 · ดึก 20:00–22:00". */
export function roundsText(windows: readonly Pick<AdminRoundWindow, 'shift' | 'start' | 'end'>[]): string {
  const parts = (['Day', 'Night'] as const)
    .map((shift) => ({
      name: shift === 'Day' ? 'เช้า' : 'ดึก',
      times: windows.filter((window) => window.shift === shift).map((window) => `${window.start}–${window.end}`),
    }))
    .filter((part) => part.times.length > 0)
    .map((part) => `${part.name} ${part.times.join(', ')}`);
  return parts.length === 0 ? 'ยังไม่มีรอบ' : parts.join(' · ');
}

/** Coordinates with 6 decimals, as the server stores them (about 0.1 m). */
export function formatCoordinates(latitude: number, longitude: number): string {
  return `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`;
}
