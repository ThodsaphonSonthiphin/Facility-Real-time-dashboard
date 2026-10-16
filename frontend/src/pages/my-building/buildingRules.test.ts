import { describe, expect, it } from 'vitest';
import { point } from '../../test/cleanerFixtures';
import { lastScanAt, myBuilding, section } from '../../test/supervisorFixtures';
import { inspectionOrder, patchPoint } from './buildingRules';

describe('inspectionOrder (facility-0057 item 1, MyBuildingEndpoints.Sorted)', () => {
  it('รอตรวจ by submit time, then ต้องแก้ไข by when it was asked, then the rest by Area in the order they came', () => {
    const points = [
      point({ id: 1, areaCode: 'AR02', status: 'NotYetDone' }),
      point({ id: 2, status: 'Rework', rework: { defect: 'ก', requestedAt: '2026-10-15T02:50:00Z' } }),
      point({ id: 3, status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:12:00Z') }),
      point({ id: 4, areaCode: 'AR01', status: 'Passed' }),
      point({ id: 5, status: 'Rework', rework: { defect: 'ข', requestedAt: '2026-10-15T02:40:00Z' } }),
      point({ id: 6, status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:05:00Z') }),
      point({ id: 7, areaCode: 'AR01', status: 'OffHours' }),
    ];

    expect(inspectionOrder(points).map((p) => p.id)).toEqual([6, 3, 5, 2, 4, 7, 1]);
  });
});

describe('patchPoint', () => {
  it('a pushed card replaces the same point where it is, in any building', () => {
    const answer = myBuilding([
      section([point({ id: 1 })]),
      section([point({ id: 2 }), point({ id: 3 })], { buildingId: 2, isCover: true }),
    ]);

    patchPoint(answer, point({ id: 3, status: 'PendingInspection' }));

    expect(answer.buildings[1].points.map((p) => [p.id, p.status])).toEqual([
      [2, 'NotYetDone'],
      [3, 'PendingInspection'],
    ]);
  });

  it('a card for a point not on the page changes nothing', () => {
    const answer = myBuilding([section([point({ id: 1 })])]);

    patchPoint(answer, point({ id: 99, status: 'Passed' }));

    expect(answer).toEqual(myBuilding([section([point({ id: 1 })])]));
  });
});
