import { waitFor } from '@testing-library/react';
import { expect } from 'vitest';
import type { PointCard } from '../shared/api/types';
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
