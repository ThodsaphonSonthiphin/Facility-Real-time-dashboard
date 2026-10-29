import type { PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatClock, formatRound } from '../../shared/utils/thaiTime';

/**
 * The title block of M4 and M12 inside a `PhoneHeader` (the design file's Submit): where the point is, its name, its
 * round and its status.
 */
export function PointHeading({ point }: { point: PointCard }) {
  return (
    <div className="flex flex-col gap-1">
      <span className="text-sm text-text-2">{`${point.areaName} · ตึก ${point.buildingCode}`}</span>
      <h1 className="text-2xl font-bold">{point.name}</h1>
      <p className="flex flex-wrap items-center gap-2">
        <span className="tabular-nums">{roundLine(point)}</span>
        <StatusChip status={point.status} />
      </p>
    </div>
  );
}

/** A hint only; the server decides the placement and the result page shows it (facility-0047 rule 3). */
function roundLine(point: PointCard): string {
  if (!point.currentRound) {
    return point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : 'ตอนนี้จุดนี้ไม่มีรอบ';
  }
  const round = `รอบ ${formatRound(point.currentRound)}`;
  if (point.status === 'NotYetDone') {
    return `${round} · ส่งหลัง ${formatClock(point.currentRound.end)} นับเป็นส่งช้า`;
  }
  if (point.status === 'Overdue') {
    return `${round} · เลยเวลารอบแล้ว ส่งตอนนี้นับเป็นส่งช้า`;
  }
  return round;
}
