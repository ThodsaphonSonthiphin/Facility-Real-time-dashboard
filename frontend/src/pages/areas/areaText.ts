import type { AreaDetail, AreaSummary } from '../../shared/api/adminSetupApi';

/** Wireframe D4's line under each Area in the list. */
export function areaMeta(area: AreaSummary): string {
  const parts = [
    `ตึก ${area.buildingCode}`,
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
