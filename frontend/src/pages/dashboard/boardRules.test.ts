import { describe, expect, it } from 'vitest';
import { point } from '../../test/cleanerFixtures';
import { boardPoints } from '../../test/liveFixtures';
import { filterPoints, groupByArea, inBuilding, replacePoint, statusCounts } from './boardRules';

describe('Dashboard rules (wireframe D1)', () => {
  it('groups the points by Area in the order the server sends them', () => {
    const groups = groupByArea(boardPoints);

    expect(groups.map((group) => [group.areaCode, group.areaName, group.buildingCode, group.points.length])).toEqual([
      ['AR01', 'Area ทดลอง', 'A', 4],
      ['AR02', 'Office ทดลอง', 'B', 3],
    ]);
    expect(groupByArea([])).toEqual([]);
  });

  it('a pushed card replaces its point in place; a point the list does not have is reported, not added', () => {
    const points = [point({ id: 1, status: 'NotYetDone' }), point({ id: 2 })];

    expect(replacePoint(points, point({ id: 1, status: 'PendingInspection' }))).toBe(true);
    expect(points.map((p) => [p.id, p.status])).toEqual([
      [1, 'PendingInspection'],
      [2, 'NotYetDone'],
    ]);
    expect(replacePoint(points, point({ id: 9 }))).toBe(false);
    expect(points).toHaveLength(2);
  });

  it('counts each status, inside the chosen building', () => {
    expect(statusCounts(boardPoints)).toEqual({
      BeforeFirstRound: 1,
      NotYetDone: 0,
      PendingInspection: 2,
      Passed: 1,
      Rework: 1,
      Overdue: 1,
      OffHours: 1,
    });
    expect(statusCounts(inBuilding(boardPoints, 'B')).PendingInspection).toBe(1);
    expect(inBuilding(boardPoints, '')).toHaveLength(7);
  });

  it('keeps the cards of the building, the status and the issue tag together', () => {
    const names = (filters: Parameters<typeof filterPoints>[1]) =>
      filterPoints(boardPoints, filters).map((p) => p.name);

    expect(names({ buildingCode: '', status: 'all', issueOnly: false, search: '', problemFirst: true })).toHaveLength(
      7,
    );
    expect(
      names({ buildingCode: '', status: 'PendingInspection', issueOnly: false, search: '', problemFirst: true }),
    ).toEqual(['ห้องน้ำชาย', 'ห้องน้ำ Office']);
    expect(
      names({ buildingCode: 'B', status: 'PendingInspection', issueOnly: false, search: '', problemFirst: true }),
    ).toEqual(['ห้องน้ำ Office']);
    expect(names({ buildingCode: '', status: 'all', issueOnly: true, search: '', problemFirst: true })).toEqual([
      'ห้องน้ำหญิง',
    ]);
    expect(names({ buildingCode: 'B', status: 'all', issueOnly: true, search: '', problemFirst: true })).toEqual([]);
  });

  it('the search looks in the Area, the point and the Cleaner who last sent it, ignoring case and spaces at the ends', () => {
    const names = (search: string) =>
      filterPoints(boardPoints, { buildingCode: '', status: 'all', issueOnly: false, search, problemFirst: true }).map(
        (p) => p.name,
      );

    expect(names('  ar02 ')).toEqual(['ห้องทำงาน', 'ห้องน้ำ Office', 'ห้องเก็บของ']);
    expect(names('ถังขยะ')).toEqual([]);
    expect(names('จุดทิ้ง')).toEqual(['จุดทิ้งขยะ']);
    expect(names('แม่บ้านทดสอบ')).toContain('ห้องน้ำชาย');
  });
});
