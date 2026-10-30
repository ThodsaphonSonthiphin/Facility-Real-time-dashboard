import type { AdminSign, AreaDetail, AreaSummary } from '../../shared/api/adminSetupApi';
import { buildingText, type BuildingNames } from '../../shared/api/buildingNames';
import { signPlaceLabel } from '../../shared/utils/labels';

/** Wireframe D4's line under each Area in the list (the "ปิดใช้งาน" tag sits beside the name, not in this line). */
export function areaMeta(area: AreaSummary, names?: BuildingNames): string {
  const parts = [
    `ตึก ${buildingText(area.buildingCode, names)}`,
    area.shiftPattern === 'DayOnly' ? 'เฉพาะกะเช้า' : '2 กะ',
    `${area.activePointCount} จุด`,
  ];
  if (area.signsNotConfirmedOnSite > 0) {
    parts.push(`ป้ายยังไม่ยืนยัน ${area.signsNotConfirmedOnSite}`);
  }
  return parts.join(' · ');
}

/** Wireframe D4's page header: how much there is to set up, in one line. */
export function areaTotals(areas: readonly AreaSummary[]): string {
  const points = areas.reduce((total, area) => total + area.activePointCount, 0);
  const buildings = new Set(areas.map((area) => area.buildingCode)).size;
  return `${areas.length} พื้นที่ · ${points} จุด · ${buildings} ตึก`;
}

/** Wireframe D4's line under the chosen Area's name: its building, shifts and active points. */
export function areaHeadline(detail: AreaDetail, names?: BuildingNames): string {
  const { area } = detail;
  const activePoints = detail.points.filter((point) => point.isActive).length;
  const parts = [
    `ตึก ${buildingText(area.buildingCode, names)}`,
    area.shiftPattern === 'DayOnly' ? 'เฉพาะกะเช้า' : '2 กะ',
    `${activePoints} จุด`,
  ];
  return parts.join(' · ');
}

/** A sign's place, in one line: how it was captured (or that it has none yet) and its radius. */
export function signPlaceText(sign: AdminSign): string {
  return `${signPlaceLabel(sign)} · รัศมี ${sign.radiusM} ม.`;
}

/** facility-0091 decision 2, as the server's AreaRules.DeactivateProblem names them; null when no Cleaner holds the Area. */
export function cleanersLeft(area: AreaSummary): string | null {
  const names = [area.dayCleaner, area.nightCleaner].flatMap((cleaner) => (cleaner ? [cleaner.displayName] : []));
  return names.length === 0 ? null : `ยังมีแม่บ้านประจำ ${names.join(', ')}`;
}
