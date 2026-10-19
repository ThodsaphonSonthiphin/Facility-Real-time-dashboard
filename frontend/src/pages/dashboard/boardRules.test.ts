import { describe, expect, it } from 'vitest';
import { point } from '../../test/cleanerFixtures';
import { boardPoints } from '../../test/liveFixtures';
import { groupByArea, replacePoint } from './boardRules';

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
});
