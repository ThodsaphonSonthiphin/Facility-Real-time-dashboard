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
  Overdue: 'เลยรอบ',
  PendingInspection: 'รอตรวจ',
  Passed: 'ตรวจผ่าน',
  NotYetDone: 'ยังไม่ทำ',
  BeforeFirstRound: 'ยังไม่ถึงรอบ',
  OffHours: 'นอกเวลา',
};
