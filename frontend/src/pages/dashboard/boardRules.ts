import type { PointCard } from '../../shared/api/types';

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
