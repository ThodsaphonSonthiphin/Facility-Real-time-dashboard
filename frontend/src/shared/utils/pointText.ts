import type { PointCard } from '../api/types';
import { formatRound } from './thaiTime';

/** "รอบ 10:00–12:00", or "รอบถัดไป …" between rounds; null when the point has no round left. */
export function roundText(point: PointCard): string | null {
  if (point.currentRound) {
    return `รอบ ${formatRound(point.currentRound)}`;
  }
  return point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : null;
}

/** "แจ้งปัญหา: ถังขยะเต็ม · …", the issue a Cleaner reported with the last send; null when none. */
export function issueText(point: PointCard): string | null {
  return point.issue ? `แจ้งปัญหา: ${[...point.issue.tags, point.issue.note].filter(Boolean).join(' · ')}` : null;
}
