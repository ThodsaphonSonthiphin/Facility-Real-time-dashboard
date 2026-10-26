import type { ManagedBuilding } from '../../shared/api/adminSetupApi';

/** What the page checks before sending; the server checks the code's format and that it is free, and its message is shown. */
export function draftProblem(code: string, name: string, isNew: boolean): string | null {
  if (isNew && code.trim() === '') {
    return 'ใส่รหัสตึกก่อน';
  }
  if (name.trim() === '') {
    return 'ใส่ชื่อตึกก่อน';
  }
  return null;
}

/** facility-0090 decision 4, counted as the server's BuildingRules.DeactivateProblem counts it; null when nothing is left. */
export function stillInside(building: ManagedBuilding): string | null {
  const left: string[] = [];
  if (building.activeAreaCount > 0) {
    left.push(`พื้นที่ที่เปิดอยู่ ${building.activeAreaCount} แห่ง`);
  }
  if (building.activeSupervisorCount > 0) {
    left.push(`หัวหน้าประจำตึก ${building.activeSupervisorCount} คน`);
  }
  return left.length === 0 ? null : `ยังมี${left.join(' และ')}`;
}

export function statusText(building: ManagedBuilding): string {
  return building.isActive ? 'ใช้งาน' : 'ปิดใช้งาน';
}
