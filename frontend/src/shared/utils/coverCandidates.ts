import type { CoverCandidate } from '../api/alertsApi';
import type { BoardRow } from '../api/attendanceBoardApi';
import { formatTime } from './thaiTime';

/**
 * A choice drawn as a card (the design file's cover forms): the chosen one pale blue with a 2 px blue frame; the 1 px
 * margin of one not chosen makes up for its thinner frame, so choosing moves nothing. The radio stays a real radio
 * inside the label, so the keyboard and a screen reader use it as one.
 */
export const choiceCardClass =
  'm-px flex cursor-pointer flex-col items-start gap-0.5 rounded-md border border-border-control bg-surface px-3 py-2.5 ' +
  'text-left has-checked:m-0 has-checked:border-2 has-checked:border-accent has-checked:bg-accent-subtle ' +
  'has-focus-visible:outline-3 has-focus-visible:outline-offset-2 has-focus-visible:outline-accent ' +
  'has-disabled:cursor-not-allowed has-disabled:opacity-50';

/** What the cover forms say about one candidate, from the server's candidate and the board of that shift. */
export function candidateFacts(candidate: CoverCandidate, rows: readonly BoardRow[], area: BoardRow, ahead: boolean) {
  const own = rows.find((row) => row.areaCode === candidate.areaCode);
  const covering = rows.filter((row) => row.covers.some((cover) => cover.userId === candidate.id)).length;
  const ownText = [
    own ? `${own.areaCode} ${own.areaName}` : candidate.areaCode,
    own ? `ตึก ${own.buildingCode}` : null,
    ahead ? null : candidate.shiftInAt ? `เข้างาน ${formatTime(candidate.shiftInAt)}` : 'ยังไม่ลงเวลา',
  ]
    .filter(Boolean)
    .join(' · ');
  const why = [
    own ? (own.buildingCode === area.buildingCode ? 'ตึกเดียวกัน' : 'ต่างตึก') : null,
    candidate.scannedThisAreaAt ? `สแกนป้าย ${area.areaCode} ไปเมื่อ ${formatTime(candidate.scannedThisAreaAt)}` : null,
    covering > 0 ? `ทำแทนอยู่ ${covering} พื้นที่` : 'ยังไม่ได้ทำแทนใคร',
  ]
    .filter(Boolean)
    .join(' · ');
  return { ownText, why, covering };
}
