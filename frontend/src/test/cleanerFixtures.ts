import type {
  AttendanceEntry,
  AttendanceEvent,
  AttendanceState,
  BlockedScan,
  MyWork,
  MyWorkArea,
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
    lastScan: null,
    issue: null,
    rework: null,
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
export const outsideShift: AttendanceState = { shiftDate: null, shift: null, events: [], nextEvents: [], outcome: null };

export function area(points: PointCard[] = [point()], overrides: Partial<MyWorkArea> = {}): MyWorkArea {
  return { areaId: 1, areaCode: 'AR01', areaName: 'Area ทดลอง', isCover: false, points, ...overrides };
}

export function myWork(attendanceState: AttendanceState = onDuty, areas: MyWorkArea[] = [area()]): MyWork {
  return { attendance: attendanceState, areas };
}

export const otherAreaBlocked: BlockedScan = {
  message: 'ป้ายนี้ไม่ใช่ Area ของคุณ',
  areaCode: 'AR02',
  areaName: 'Area ถัดไป',
  reason: 'OtherArea',
  buildingCode: 'B',
};
