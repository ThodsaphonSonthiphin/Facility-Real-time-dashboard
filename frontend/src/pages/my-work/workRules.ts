import type { MyWorkArea, PointCard, PointStatus } from '../../shared/api/types';
import { formatClock, formatRound, formatTime } from '../../shared/utils/thaiTime';

/** What needs the Cleaner comes first (plan 8 decision 6), then what is done or not due yet. */
const urgency: readonly PointStatus[] = [
  'Rework',
  'Overdue',
  'NotYetDone',
  'PendingInspection',
  'Passed',
  'BeforeFirstRound',
  'OffHours',
];

/** A point with the Area it belongs to, so a row can say "(ทำแทน)" for a covered Area. */
export interface WorkItem {
  point: PointCard;
  area: MyWorkArea;
}

/** Every point of the own Area, then of the covered Areas, most urgent first; the server's order breaks ties. */
export function byUrgency(areas: readonly MyWorkArea[]): WorkItem[] {
  return areas
    .flatMap((area) => area.points.map((point) => ({ point, area })))
    .sort((a, b) => urgency.indexOf(a.point.status) - urgency.indexOf(b.point.status));
}

/** Statuses the Cleaner has to act on now: the large card and the "ต้องทำต่อ" group (#68, variant B). */
export const toDo: readonly PointStatus[] = ['Rework', 'Overdue', 'NotYetDone'];
/** Sent this round: the "ส่งแล้ว" group and the progress line. */
export const sentStatuses: readonly PointStatus[] = ['PendingInspection', 'Passed'];
/** Nothing to do yet: the "ยังไม่ต้องทำ" group (owner, 2026-10-22: ยังไม่ถึงรอบ and นอกเวลา together). */
export const laterStatuses: readonly PointStatus[] = ['BeforeFirstRound', 'OffHours'];

function roundText(point: PointCard): string | null {
  if (point.currentRound) {
    return `รอบ ${formatRound(point.currentRound)}`;
  }
  return point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : null;
}

/** "Area ทดลอง · รอบ 10:00–12:00", or for a covered Area "Area 12 · ตึก B (ทำแทน) · รอบ …". */
export function whereText({ point, area }: WorkItem): string {
  const where = area.isCover ? `${area.areaName} · ตึก ${point.buildingCode} (ทำแทน)` : area.areaName;
  const round = roundText(point);
  return round ? `${where} · ${round}` : where;
}

/** What the large card says under the point, by status. */
export function detailOf(point: PointCard): string {
  const sent = point.lastScan ? `ส่งงาน ${formatTime(point.lastScan.submittedAt)}` : null;
  const next = point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : null;
  switch (point.status) {
    case 'Rework':
      return point.rework ? `หัวหน้าให้แก้ ${formatTime(point.rework.requestedAt)}: “${point.rework.defect}”` : 'หัวหน้าให้แก้';
    case 'Overdue':
      return 'ยังไม่ส่งงาน · เลยเวลารอบแล้ว';
    case 'NotYetDone':
      return point.currentRound ? `ยังไม่ส่ง · ส่งได้ถึง ${formatClock(point.currentRound.end)}` : 'ยังไม่ส่ง';
    case 'PendingInspection':
      return [sent, next].filter(Boolean).join(' · ');
    case 'Passed':
      return ['ตรวจผ่าน', sent, next].filter(Boolean).join(' · ');
    case 'BeforeFirstRound':
      return 'รอบแรกของกะยังไม่เริ่ม';
    case 'OffHours':
      return 'จุดนี้ไม่มีรอบในกะนี้';
  }
}

/** The large card's instruction (owner, 2026-10-22). */
export function whatToDo(status: PointStatus): string {
  return status === 'Rework' ? 'แก้แล้วสแกน QR ที่จุดนี้เพื่อส่งงานใหม่' : 'สแกน QR ที่จุดนี้เพื่อส่งงาน';
}

export function issueText(point: PointCard): string | null {
  return point.issue ? `แจ้งปัญหา: ${[...point.issue.tags, point.issue.note].filter(Boolean).join(' · ')}` : null;
}
