import type { AdminSign, AreaDetail, AreaSummary } from '../../shared/api/adminSetupApi';
import { buildingText, type BuildingNames } from '../../shared/api/buildingNames';
import { signPlaceLabel } from '../../shared/utils/labels';

/** Wireframe D4's line under each Area in the list. */
export function areaMeta(area: AreaSummary, names?: BuildingNames): string {
  const parts = [
    `ตึก ${buildingText(area.buildingCode, names)}`,
    area.shiftPattern === 'DayOnly' ? 'เฉพาะกะเช้า' : '2 กะ',
    `${area.activePointCount} จุด`,
  ];
  if (area.signsNotConfirmedOnSite > 0) {
    parts.push(`ป้ายยังไม่ยืนยัน ${area.signsNotConfirmedOnSite}`);
  }
  if (!area.isActive) {
    parts.push('ปิดใช้งาน');
  }
  return parts.join(' · ');
}

/** Wireframe D4's line under the chosen Area's name: shifts, Cleaners, and signs (1 check-in + active points). */
export function areaLine(detail: AreaDetail): string {
  const { area } = detail;
  const day = `เช้า ${area.dayCleaner?.displayName ?? 'ยังไม่มี'}`;
  const cleaners =
    area.shiftPattern === 'DayOnly' ? day : `${day} · ดึก ${area.nightCleaner?.displayName ?? 'ยังไม่มี'}`;
  const pattern = area.shiftPattern === 'DayOnly' ? 'ทำเฉพาะกะเช้า' : 'ทำ 2 กะ (เช้า + ดึก)';
  const activePoints = detail.points.filter((point) => point.isActive).length;
  return `${pattern} · แม่บ้านประจำ: ${cleaners} · ป้าย 1 + ${activePoints}`;
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
