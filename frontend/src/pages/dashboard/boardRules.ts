import type { PointCard, PointStatus } from '../../shared/api/types';
import type { DashboardState } from '../../store/slices/dashboardSlice';

/** A pushed card replaces the same point in place. False when the list does not have that point (#26). */
export function replacePoint(points: PointCard[], card: PointCard): boolean {
  const index = points.findIndex((point) => point.id === card.id);
  if (index < 0) {
    return false;
  }
  points[index] = card;
  return true;
}

/** One Area's section of wireframe D1. */
export interface AreaGroup {
  areaId: number;
  areaCode: string;
  areaName: string;
  buildingCode: string;
  points: PointCard[];
}

/** The server sends points by Area code, then by the Area's own order (PointBoardQuery); each Area becomes one section. */
export function groupByArea(points: readonly PointCard[]): AreaGroup[] {
  const groups: AreaGroup[] = [];
  for (const point of points) {
    const last = groups.at(-1);
    if (last && last.areaId === point.areaId) {
      last.points.push(point);
    } else {
      groups.push({
        areaId: point.areaId,
        areaCode: point.areaCode,
        areaName: point.areaName,
        buildingCode: point.buildingCode,
        points: [point],
      });
    }
  }
  return groups;
}

/** Wireframe D1's legend order: not started, to do, waiting, done, then the ones that need someone, then off hours. */
export const legendOrder: readonly PointStatus[] = [
  'BeforeFirstRound',
  'NotYetDone',
  'PendingInspection',
  'Passed',
  'Rework',
  'Overdue',
  'OffHours',
];

/** ต้องแก้ไข and เลยเวลา need someone, so their tile is framed in the status colour as well as the chip (#77). */
export const tileFrame: Partial<Record<PointStatus, string>> = {
  Rework: 'border-2 border-status-rework',
  Overdue: 'border-2 border-status-overdue',
};

/** The cards of one building; '' is every building. */
export function inBuilding(points: readonly PointCard[], buildingCode: string): PointCard[] {
  return buildingCode === '' ? [...points] : points.filter((point) => point.buildingCode === buildingCode);
}

export function statusCounts(points: readonly PointCard[]): Record<PointStatus, number> {
  const counts = Object.fromEntries(legendOrder.map((status) => [status, 0])) as Record<PointStatus, number>;
  for (const point of points) {
    counts[point.status] += 1;
  }
  return counts;
}

/** The cards to show: the building, then the status and the issue tag (the legend counts inside the building). */
export function filterPoints(points: readonly PointCard[], filters: DashboardState): PointCard[] {
  return inBuilding(points, filters.buildingCode).filter(
    (point) => (filters.status === 'all' || point.status === filters.status) && (!filters.issueOnly || Boolean(point.issue)),
  );
}
