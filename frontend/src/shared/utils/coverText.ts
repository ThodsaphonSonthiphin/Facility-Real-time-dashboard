import type { CoverAssignment, CoverCandidate } from '../api/alertsApi';
import { buildingText, type BuildingNames } from '../api/buildingNames';
import { formatTime } from './thaiTime';

/**
 * One choice of "ให้ใครทำแทน": who, their own Area, their Shift-In, and whether they already scanned this Area's signs.
 * For a shift not started nobody has checked in or scanned, so only who and their Area are shown.
 */
export function candidateText(candidate: CoverCandidate, ahead = false): string {
  if (ahead) {
    return `${candidate.displayName} · ${candidate.areaCode}`;
  }
  return [
    candidate.displayName,
    candidate.areaCode,
    candidate.shiftInAt ? `เข้างานแล้ว ${formatTime(candidate.shiftInAt)}` : 'ยังไม่ลงเวลา',
    candidate.scannedThisAreaAt ? `สแกนป้ายพื้นที่นี้เมื่อ ${formatTime(candidate.scannedThisAreaAt)}` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}

/** What the page says once a cover is made: an Area for a Cleaner, a building for a Supervisor. */
export function coverNotice(cover: CoverAssignment, names?: BuildingNames): string {
  return cover.areaCode
    ? `มอบหมาย ${cover.personName} ทำแทน ${cover.areaCode} แล้ว`
    : `มอบหมาย ${cover.personName} ตรวจแทนตึก ${cover.buildingCode ? buildingText(cover.buildingCode, names) : ''} แล้ว`;
}
