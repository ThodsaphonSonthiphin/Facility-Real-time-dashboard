import type { MyBuilding } from '../../src/pages/my-building/myBuildingApi.ts';
import type { ScheduleWeek } from '../../src/shared/api/scheduleTypes.ts';
import type { MyWork, PointCard } from '../../src/shared/api/types.ts';
import { restroomName } from './seed.ts';

// Live read models change throughout the day. Render a fixed shift for screenshots;
// the functional browser suites exercise the same screens against unmodified API reads.
export const visualTime = new Date('2026-11-19T03:30:00Z');
const shiftDate = '2026-11-19';
const area = { areaId: 1, areaCode: 'AR01', areaName: 'Area 1 ชั้น 1' };
export const visualPoints: PointCard[] = [
  {
    ...area, id: 1, name: restroomName, buildingCode: 'A', status: 'Overdue',
    currentRound: { id: 1, start: '07:00:00', end: '09:00:00' },
  },
  {
    ...area, id: 2, name: 'ห้องน้ำหญิง ชั้น 1', buildingCode: 'A', status: 'PendingInspection',
    currentRound: { id: 2, start: '10:00:00', end: '12:00:00' },
  },
];

export const visualWork: MyWork = {
  ownShift: { shift: 'Day', opensAt: '06:00:00', closesAt: '20:00:00' },
  attendance: {
    shiftDate, shift: 'Day',
    events: [{ eventType: 'ShiftIn', occurredAt: '2026-11-19T00:01:00Z', source: 'Scan', withinRadius: true }],
  },
  areas: [{ ...area, isCover: false, points: visualPoints }],
};

export const visualBuilding: MyBuilding = {
  shiftDate, shift: 'Day',
  buildings: [{
    buildingId: 1, buildingCode: 'A', buildingName: 'ตึก A',
    isCover: false, canInspect: true, points: visualPoints,
  }],
};

export const visualWeek: ScheduleWeek = {
  weekStart: '2026-11-16', revision: '1', readAt: visualTime.toISOString(),
  availability: 'available', relief: [],
  rows: [{
    userId: 2, displayName: 'แม่บ้านทดสอบ', cleanerKind: 'Regular', homeBuildingId: 1, homeBuildingCode: 'A',
    hasWeeklyChoice: true, weeklyRevision: '1', regularBaselineRevision: '1', reliefBaselineRevision: '0',
    days: Array.from({ length: 7 }, (_, day) => ({
      date: `2026-11-${16 + day}`,
      shift: 'Day',
      area: { sourceKind: 'baseline', revision: '1', value: 1 },
      breakGroup: { sourceKind: 'baseline', revision: '1', value: 1 },
      availability: 'available', isDayOff: false, isLeave: false, isUnset: false, problems: [],
    })),
  }],
};
