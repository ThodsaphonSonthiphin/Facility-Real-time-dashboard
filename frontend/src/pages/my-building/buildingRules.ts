import type { PointCard, PointStatus } from '../../shared/api/types';
import type { MyBuilding } from './myBuildingApi';

/** A pushed card replaces the same point where it is; a point that is not on this page is ignored (plan 9 decision 8). */
export function patchPoint(building: MyBuilding, card: PointCard): void {
  for (const section of building.buildings) {
    const index = section.points.findIndex((p) => p.id === card.id);
    if (index >= 0) {
      section.points[index] = card;
      return;
    }
  }
}

function groupOf(status: PointStatus): number {
  if (status === 'PendingInspection') {
    return 0;
  }
  return status === 'Rework' ? 1 : 2;
}

/** When the point started waiting for the Supervisor; one without a time goes last in its group, as on the server. */
function waitingSince(point: PointCard): string {
  const since = point.status === 'PendingInspection' ? point.lastScan?.submittedAt : point.rework?.requestedAt;
  return since ?? '9999';
}

function ordinal(a: string, b: string): number {
  if (a === b) {
    return 0;
  }
  return a < b ? -1 : 1;
}

/**
 * facility-0057 item 1, the server's order (MyBuildingEndpoints.Sorted): รอตรวจ by submit time, then ต้องแก้ไข by when
 * it was asked, then the rest by Area code. Sorted again on the phone because a pushed card can change group. The sort
 * is stable, so within an Area the rest keep the order they came in.
 */
export function inspectionOrder(points: readonly PointCard[]): PointCard[] {
  return [...points].sort((a, b) => {
    const group = groupOf(a.status) - groupOf(b.status);
    if (group !== 0) {
      return group;
    }
    return groupOf(a.status) === 2 ? ordinal(a.areaCode, b.areaCode) : ordinal(waitingSince(a), waitingSince(b));
  });
}

/** What waits for the Supervisor (รอตรวจ) or for the Cleaner (ต้องแก้ไข) is framed in its status colour (#76). */
export const statusFrame: Partial<Record<PointStatus, string>> = {
  PendingInspection: 'border border-pending-border',
  Rework: 'border border-rework-border',
};
