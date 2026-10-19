import { waitFor } from '@testing-library/react';
import { expect } from 'vitest';
import type {
  AreaAttendanceState,
  AttendanceBoard,
  BoardEvent,
  BoardRow,
} from '../shared/api/attendanceBoardApi';
import type { Alert } from '../shared/api/alertListApi';
import type { AttendanceEvent, PointCard } from '../shared/api/types';
import type { AppStore } from '../store';
import { point } from './cleanerFixtures';
import { lastScanAt } from './supervisorFixtures';

/**
 * Made-up answers for the Admin live board (public repo: no real names). The API sends UTC; the Thai time is in the
 * comments: 03:05Z is 10:05. Fields the server sends as null are left out, as it does.
 */

/** Waits until no request is on its way (no pending entry in the RTK Query cache), so a late answer cannot land after a check. */
export async function apiIdle(store: AppStore): Promise<void> {
  await waitFor(() =>
    expect(Object.values(store.getState().api.queries).some((query) => query?.status === 'pending')).toBe(false),
  );
}

/** Wireframe D1's points in the server's order: AR01 in building A, then AR02 in building B. */
export const boardPoints: PointCard[] = [
  point({ id: 1, name: 'ห้องน้ำชาย', status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:05:00Z') }), // 10:05
  point({
    id: 2,
    name: 'ห้องน้ำหญิง',
    status: 'Passed',
    lastScan: lastScanAt('2026-10-15T03:00:00Z'), // 10:00
    issue: { tags: ['ก๊อกน้ำรั่ว'], note: 'อ่างที่ 2', reportedAt: '2026-10-15T03:00:00Z' },
  }),
  point({
    id: 3,
    name: 'จุดทิ้งขยะ',
    status: 'Rework',
    currentRound: { id: 31, start: '08:00:00', end: '10:00:00' },
    rework: { defect: 'รอบถังขยะยังมีคราบ', requestedAt: '2026-10-15T02:40:00Z' }, // 09:40
  }),
  point({ id: 4, name: 'โถงทางเดิน', status: 'Overdue', currentRound: { id: 41, start: '07:30:00', end: '09:00:00' } }),
  point({
    id: 5,
    name: 'ห้องทำงาน',
    areaId: 2,
    areaCode: 'AR02',
    areaName: 'Office ทดลอง',
    buildingCode: 'B',
    status: 'BeforeFirstRound',
    currentRound: undefined,
    nextRound: { id: 51, start: '11:00:00', end: '12:00:00' },
  }),
  point({
    id: 6,
    name: 'ห้องน้ำ Office',
    areaId: 2,
    areaCode: 'AR02',
    areaName: 'Office ทดลอง',
    buildingCode: 'B',
    status: 'PendingInspection',
    currentRound: { id: 61, start: '08:00:00', end: '09:00:00' },
    lastScan: lastScanAt('2026-10-15T02:20:00Z', { placement: 'Late' }), // 09:20
  }),
  point({
    id: 7,
    name: 'ห้องเก็บของ',
    areaId: 2,
    areaCode: 'AR02',
    areaName: 'Office ทดลอง',
    buildingCode: 'B',
    status: 'OffHours',
    currentRound: undefined,
    nextRound: undefined,
  }),
];

/** One attendance scan (BoardEventDto): by default scanned within 6 m of the sign. */
export function boardEvent(
  id: number,
  eventType: AttendanceEvent,
  occurredAt: string,
  overrides: Partial<BoardEvent> = {},
): BoardEvent {
  return { id, eventType, occurredAt, source: 'Scan', withinRadius: true, distanceM: 6, ...overrides };
}

/** AR09 has no regular Cleaner for the shift (the server leaves `cleaner` out). */
export const noCleanerRow: BoardRow = {
  areaId: 9,
  areaCode: 'AR09',
  areaName: 'Area เก้า',
  buildingCode: 'A',
  state: 'Empty',
  events: [],
  covers: [],
};

/** AR12: its Cleaner has not come and nobody covers (wireframe D2's first row). */
export const emptyRow: BoardRow = {
  areaId: 12,
  areaCode: 'AR12',
  areaName: 'Area สิบสอง',
  buildingCode: 'B',
  cleaner: { id: 26, employeeId: '650006', displayName: 'แม่บ้านลา' },
  state: 'Empty',
  events: [],
  covers: [],
};

/** AR01: checked in 06:52, 85 m from the sign (a flag, facility-0035). */
export const workingRow: BoardRow = {
  areaId: 1,
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  buildingCode: 'A',
  cleaner: { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ' },
  state: 'Working',
  events: [boardEvent(1001, 'ShiftIn', '2026-10-14T23:52:00Z', { withinRadius: false, distanceM: 85 })],
  covers: [],
};

/** AR02: its Cleaner is away; แม่บ้านที่อื่น (AR03) covers since 07:10 and checked in at 06:50. */
export const coveredRow: BoardRow = {
  areaId: 2,
  areaCode: 'AR02',
  areaName: 'Office ทดลอง',
  buildingCode: 'B',
  cleaner: { id: 27, employeeId: '650007', displayName: 'แม่บ้านป่วย' },
  state: 'Covered',
  events: [],
  covers: [
    {
      id: 71,
      userId: 25,
      employeeId: '650005',
      displayName: 'แม่บ้านที่อื่น',
      ownAreaCode: 'AR03',
      assignedAt: '2026-10-15T00:10:00Z',
      shiftInAt: '2026-10-14T23:50:00Z',
    },
  ],
};

/** AR05: Shift-In 07:02 added by an Admin (facility-0051), so it has no GPS verdict. */
export const correctedRow: BoardRow = {
  areaId: 5,
  areaCode: 'AR05',
  areaName: 'Area ห้า',
  buildingCode: 'A',
  cleaner: { id: 29, employeeId: '650009', displayName: 'แม่บ้านแบตหมด' },
  state: 'Working',
  events: [boardEvent(1004, 'ShiftIn', '2026-10-15T00:02:00Z', { source: 'AdminAdd', withinRadius: undefined, distanceM: undefined })],
  covers: [],
};

/** AR07: in 06:55, on a break since 09:30. */
export const breakRow: BoardRow = {
  areaId: 7,
  areaCode: 'AR07',
  areaName: 'Area เจ็ด',
  buildingCode: 'A',
  cleaner: { id: 28, employeeId: '650008', displayName: 'แม่บ้านพัก' },
  state: 'OnBreak',
  events: [boardEvent(1002, 'ShiftIn', '2026-10-14T23:55:00Z'), boardEvent(1003, 'BreakOut', '2026-10-15T02:30:00Z')],
  covers: [],
};

/** In the server's order: Areas with nobody first, then by Area code. */
export const boardRows: BoardRow[] = [noCleanerRow, emptyRow, workingRow, coveredRow, correctedRow, breakRow];

/** The Day shift of 15/10/2026 as the clock sees it, with the summary the server would count from these rows. */
export function attendanceBoard(rows: BoardRow[] = boardRows, overrides: Partial<AttendanceBoard> = {}): AttendanceBoard {
  const count = (...states: AreaAttendanceState[]) => rows.filter((row) => states.includes(row.state)).length;
  return {
    shiftDate: '2026-10-15',
    shift: 'Day',
    shiftEnded: false,
    summary: {
      required: rows.length,
      present: count('Working', 'OnBreak', 'Done'),
      empty: count('Empty'),
      covered: count('Covered'),
    },
    rows,
    ...overrides,
  };
}

/** 09:10: แม่บ้านที่อื่น (AR03) scanned a point of AR12 and was refused; a cover can still be given (facility-0041). */
export const blockedAlert: Alert = {
  kind: 'BlockedScan',
  recordId: 301,
  at: '2026-10-15T02:10:00Z',
  person: { id: 25, employeeId: '650005', displayName: 'แม่บ้านที่อื่น', role: 'Cleaner', areaCode: 'AR03' },
  areaCode: 'AR12',
  areaName: 'Area สิบสอง',
  signCode: 'AR12-02',
  pointName: 'ห้องน้ำหญิง',
  missingEvents: [],
  reviewed: false,
  cover: { userId: 25, areaId: 12, shiftDate: '2026-10-15', shift: 'Day' },
  blockReason: 'OtherArea',
};

/** 07:00 today, the end of last night's shift (14/10): แม่บ้านกะดึก checked in and has no Shift-Out (facility-0051). */
export const missingAlert: Alert = {
  kind: 'MissingAttendance',
  recordId: 401,
  at: '2026-10-15T00:00:00Z',
  person: { id: 22, employeeId: '650002', displayName: 'แม่บ้านกะดึก', role: 'Cleaner', areaCode: 'AR01' },
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  missingEvents: ['ShiftOut'],
  reviewed: false,
};

/** 06:52: checked in 85 m from AR01's check-in sign. Same record id as missingAlert: an alert is known by kind and id. */
export const attendanceGpsAlert: Alert = {
  kind: 'AttendanceGps',
  recordId: 401,
  at: '2026-10-14T23:52:00Z',
  person: { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ', role: 'Cleaner', areaCode: 'AR01' },
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  signCode: 'AR01-IN',
  distanceM: 85,
  radiusM: 50,
  accuracyM: 40,
  missingEvents: [],
  reviewed: false,
};

/** 10:15: the Supervisor inspected 62 m from the sign (facility-0037). */
export const inspectionGpsAlert: Alert = {
  kind: 'InspectionGps',
  recordId: 601,
  at: '2026-10-15T03:15:00Z',
  person: { id: 31, employeeId: '660001', displayName: 'หัวหน้าทดสอบ', role: 'Supervisor' },
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  signCode: 'AR01-03',
  pointName: 'จุดทิ้งขยะ',
  distanceM: 62,
  radiusM: 50,
  accuracyM: 25,
  missingEvents: [],
  reviewed: false,
};

/** Yesterday 15:00, a submission 70 m away; an Admin closed it at 16:30. */
export const reviewedAlert: Alert = {
  kind: 'SubmissionGps',
  recordId: 701,
  at: '2026-10-14T08:00:00Z',
  person: { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ', role: 'Cleaner', areaCode: 'AR01' },
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  signCode: 'AR01-01',
  pointName: 'ห้องน้ำชาย',
  distanceM: 70,
  radiusM: 50,
  accuracyM: 30,
  missingEvents: [],
  reviewed: true,
  reviewedAt: '2026-10-14T09:30:00Z',
};

/** Newest first, as the server sends them. */
export const alerts: Alert[] = [inspectionGpsAlert, blockedAlert, missingAlert, attendanceGpsAlert, reviewedAlert];
