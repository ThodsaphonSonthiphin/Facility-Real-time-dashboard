import type { MyBuilding, MyBuildingSection } from '../pages/my-building/myBuildingApi';
import type { InspectionRecorded, InspectionScan, InspectionSubmission } from '../pages/inspect/inspectApi';
import type { BlockedScan, LastScan, PointCard } from '../shared/api/types';
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

/** The submission a scan offers for judging (InspectionSubmissionDto); 03:05Z is 10:05. */
export function submission(overrides: Partial<InspectionSubmission> = {}): InspectionSubmission {
  return {
    id: 501,
    submittedAt: '2026-10-15T03:05:00Z',
    cleanerName: 'แม่บ้านทดสอบ',
    placement: 'OnTime',
    status: 'Normal',
    issueTags: [],
    ...overrides,
  };
}

/** POST /api/inspections/scan with work to judge (M6): 4 m from the sign, within the radius. */
export function inspectionScan(overrides: Partial<InspectionScan> = {}): InspectionScan {
  return {
    point: point({ status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:05:00Z') }),
    submission: submission(),
    isCover: false,
    distanceM: 4,
    withinRadius: true,
    ...overrides,
  };
}

/** POST /api/inspections/scan with nothing to judge (M16): no submission key, as the server sends it. */
export function nothingToInspect(
  refusal = 'ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน',
  overrides: Partial<InspectionScan> = {},
): InspectionScan {
  return { point: point(), refusal, isCover: false, distanceM: 5, withinRadius: true, ...overrides };
}

/** A 201 answer of POST /api/inspections; 03:15Z is 10:15. */
export function recorded(overrides: Partial<InspectionRecorded> = {}): InspectionRecorded {
  return {
    inspectionId: 901,
    servicePointId: 11,
    scanRecordId: 501,
    result: 'Passed',
    newPointStatus: 'Passed',
    distanceM: 4,
    withinRadius: true,
    inspectedAt: '2026-10-15T03:15:00Z',
    ...overrides,
  };
}

/** 403 from either inspection call: the sign is in another building (facility-0048). */
export const otherBuildingBlocked: BlockedScan = {
  message: 'จุดนี้ไม่ได้อยู่ในตึกของคุณ',
  areaCode: 'BR01',
  areaName: 'Area ตึกอื่น',
  reason: 'OtherBuilding',
  buildingCode: 'B',
};

/** 403 from either inspection call: the own building, but not the own shift by the clock (facility-0048 amendment). */
export const outsideShiftBlocked: BlockedScan = {
  message: 'ตอนนี้ไม่ใช่กะของคุณ ตรวจได้เฉพาะในกะของตัวเอง',
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  reason: 'OutsideShift',
  buildingCode: 'A',
};
