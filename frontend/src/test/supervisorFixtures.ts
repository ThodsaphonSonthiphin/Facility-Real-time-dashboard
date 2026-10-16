import type { MyBuilding, MyBuildingSection } from '../pages/my-building/myBuildingApi';
import type { LastScan, PointCard } from '../shared/api/types';
import { point } from './cleanerFixtures';
import { authAnswer } from './fakeServer';

/**
 * Made-up answers for the Supervisor pages (public repo: no real names). The API sends UTC; the Thai time is in the
 * comments: 03:05Z is 10:05. Fields the server sends as null are left out, as it does.
 */
export const supervisorSession = { 'POST /api/auth/refresh': () => authAnswer('supervisor') };

export function lastScanAt(submittedAt: string, overrides: Partial<LastScan> = {}): LastScan {
  return { submittedAt, cleanerName: 'แม่บ้านทดสอบ', placement: 'OnTime', status: 'Normal', ...overrides };
}

export function section(points: PointCard[] = [point()], overrides: Partial<MyBuildingSection> = {}): MyBuildingSection {
  return { buildingId: 1, buildingCode: 'A', buildingName: 'ตึกทดลอง', isCover: false, canInspect: true, points, ...overrides };
}

export function myBuilding(buildings: MyBuildingSection[] = [section()]): MyBuilding {
  return { shiftDate: '2026-10-15', shift: 'Day', buildings };
}
