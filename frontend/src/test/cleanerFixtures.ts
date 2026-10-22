import type {
  AttendanceEntry,
  AttendanceEvent,
  AttendanceState,
  BlockedScan,
  MyWork,
  MyWorkArea,
  OwnShift,
  PointCard,
} from '../shared/api/types';
import { authAnswer } from './fakeServer';

/**
 * Made-up answers for the Cleaner pages (public repo: no real names). The API sends UTC; the Thai time is in the
 * comments: 23:52Z the day before is 06:52, 03:05Z is 10:05.
 */
export const cleanerSession = { 'POST /api/auth/refresh': () => authAnswer('cleaner') };

export function point(overrides: Partial<PointCard> = {}): PointCard {
  return {
    id: 11,
    name: 'ห้องน้ำชาย',
    areaId: 1,
    areaCode: 'AR01',
    areaName: 'Area ทดลอง',
    buildingCode: 'A',
    status: 'NotYetDone',
    currentRound: { id: 101, start: '10:00:00', end: '12:00:00' },
    nextRound: { id: 102, start: '13:00:00', end: '15:00:00' },
    ...overrides,
  };
}

export function entry(eventType: AttendanceEvent, occurredAt: string, withinRadius: boolean | null = true): AttendanceEntry {
  return { eventType, occurredAt, source: 'Scan', withinRadius };
}

export function attendance(
  events: AttendanceEntry[],
  nextEvents: AttendanceEvent[],
  outcome: AttendanceState['outcome'] = null,
): AttendanceState {
  return { shiftDate: '2026-10-15', shift: 'Day', events, nextEvents, outcome };
}

export const notCheckedIn = attendance([], ['ShiftIn']);
export const onDuty = attendance([entry('ShiftIn', '2026-10-14T23:52:00Z')], ['BreakOut', 'ShiftOut']); // in 06:52
export const checkedOut = attendance(
  [entry('ShiftIn', '2026-10-14T23:52:00Z'), entry('ShiftOut', '2026-10-15T11:55:00Z')], // 06:52, 18:55
  [],
);
/** Outside the attendance window, as the server sends it: it leaves out the null shiftDate, shift and outcome. */
export const outsideShift: AttendanceState = { events: [], nextEvents: [] };

export function area(points: PointCard[] = [point()], overrides: Partial<MyWorkArea> = {}): MyWorkArea {
  return { areaId: 1, areaCode: 'AR01', areaName: 'Area ทดลอง', isCover: false, points, ...overrides };
}

/** The own shift as the server sends it with the default settings: attendance from 60 minutes before to 60 after. */
export const dayShift: OwnShift = { shift: 'Day', opensAt: '06:00:00', closesAt: '20:00:00' };
export const nightShift: OwnShift = { shift: 'Night', opensAt: '18:00:00', closesAt: '08:00:00' };

export function myWork(
  attendanceState: AttendanceState = onDuty,
  areas: MyWorkArea[] = [area()],
  ownShift: OwnShift = dayShift,
): MyWork {
  return { attendance: attendanceState, areas, ownShift };
}

export const otherAreaBlocked: BlockedScan = {
  message: 'ป้ายนี้ไม่ใช่พื้นที่ของคุณ',
  areaCode: 'AR02',
  areaName: 'Area ถัดไป',
  reason: 'OtherArea',
  buildingCode: 'B',
};
