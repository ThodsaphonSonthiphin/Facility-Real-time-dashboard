import { buildingText, type BuildingNames } from '../../shared/api/buildingNames';
import { shiftShortLabel } from '../../shared/utils/labels';
import type { SummaryRow } from './summaryApi';

/** ExportRules.MaxDays, which the summary endpoint uses too. */
export const SUMMARY_MAX_DAYS = 31;

/** "จุดที่ตรวจแล้ว": points of the beat inspected at least once in the range, and how many were not. */
export function pointsText({ pointsInspected, pointsTotal }: SummaryRow): string {
  const missed = pointsTotal - pointsInspected;
  return `${pointsInspected} จาก ${pointsTotal} จุด${missed > 0 ? ` · ไม่ได้ตรวจ ${missed} จุด` : ''}`;
}

/** "สัดส่วนที่ตรวจ" in whole percent; null when nothing was submitted (plan 12 decision 4). */
export function inspectedShare({ submitted, submittedInspected }: SummaryRow): number | null {
  return submitted === 0 ? null : Math.round((submittedInspected / submitted) * 100);
}

export function placeText({ buildingCode, shift }: SummaryRow, names?: BuildingNames): string {
  return `${buildingText(buildingCode, names)} · ${shiftShortLabel[shift]}`;
}
