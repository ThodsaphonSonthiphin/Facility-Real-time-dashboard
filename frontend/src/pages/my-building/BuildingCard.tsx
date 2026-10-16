import type { PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatClock, formatRound, formatTime } from '../../shared/utils/thaiTime';

interface BuildingCardProps {
  point: PointCard;
  /** Own building in own shift, or a cover (facility-0048): only then does a รอตรวจ card send the Supervisor to scan. */
  canInspect: boolean;
}

/** One point on My Building. Names, defects and notes are rendered as React text only (facility-0066). */
export function BuildingCard({ point, canInspect }: BuildingCardProps) {
  const round = point.currentRound
    ? `รอบ ${formatRound(point.currentRound)}`
    : point.nextRound
      ? `รอบถัดไป ${formatRound(point.nextRound)}`
      : null;

  return (
    <li className={`building-card building-card--${point.status}`}>
      <div className="building-card__top">
        <h3 className="building-card__name">{point.name}</h3>
        <StatusChip status={point.status} />
      </div>
      <span className="building-card__where">{[point.areaName, round].filter(Boolean).join(' · ')}</span>
      <span className="building-card__detail">{detailOf(point)}</span>
      {canInspect && point.status === 'PendingInspection' && (
        <span className="building-card__todo">ไปที่จุดนี้แล้วสแกน QR เพื่อตรวจ</span>
      )}
      {point.issue && (
        <span className="building-card__issue">
          {`แจ้งปัญหา: ${[...point.issue.tags, point.issue.note].filter(Boolean).join(' · ')}`}
        </span>
      )}
    </li>
  );
}

/** Who asked for a rework is not in ReworkDto, so M15's "คุณให้แก้" is "ให้แก้" (plan 9 decision 7). */
function detailOf(point: PointCard): string {
  const sent = point.lastScan ? `ส่ง ${formatTime(point.lastScan.submittedAt)} · ${point.lastScan.cleanerName}` : null;
  switch (point.status) {
    case 'PendingInspection':
      return sent ?? 'ส่งแล้ว';
    case 'Rework':
      return point.rework
        ? `ให้แก้ ${formatTime(point.rework.requestedAt)}: “${point.rework.defect}” · รอแม่บ้านส่งใหม่`
        : 'รอแม่บ้านส่งใหม่';
    case 'Passed':
      return ['ตรวจผ่าน', sent].filter(Boolean).join(' · ');
    case 'Overdue':
      return 'ยังไม่ส่ง · เลยเวลารอบแล้ว';
    case 'NotYetDone':
      return point.currentRound ? `ยังไม่ส่ง · ส่งได้ถึง ${formatClock(point.currentRound.end)}` : 'ยังไม่ส่ง';
    case 'BeforeFirstRound':
      return 'รอบแรกของกะยังไม่เริ่ม';
    case 'OffHours':
      return 'จุดนี้ไม่มีรอบในกะนี้';
  }
}
