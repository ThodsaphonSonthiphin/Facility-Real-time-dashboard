import type { Placement, PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatClock, formatRound, formatTime } from '../../shared/utils/thaiTime';

/**
 * One point on wireframe D1. It is not a link: only the sign's QR opens a point (facility-0059). Names, defects and
 * notes are rendered as React text only (facility-0066).
 */
export function PointTile({ point }: { point: PointCard }) {
  const round = roundText(point);
  return (
    <li className="dashboard-tile">
      <div className="dashboard-tile__top">
        <h3 className="dashboard-tile__name">{point.name}</h3>
        <StatusChip status={point.status} />
      </div>
      {round && <span className="dashboard-tile__round">{round}</span>}
      <span className="dashboard-tile__detail">{detailText(point)}</span>
      {point.issue && (
        <span className="dashboard-tile__issue">
          {`แจ้งปัญหา: ${[...point.issue.tags, point.issue.note].filter(Boolean).join(' · ')}`}
        </span>
      )}
    </li>
  );
}

function roundText(point: PointCard): string | null {
  if (point.currentRound) {
    return `รอบ ${formatRound(point.currentRound)}`;
  }
  return point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : null;
}

const placementNote: Record<Placement, string> = { OnTime: '', Late: ' · ส่งช้า', Rework: ' · งานแก้', OffRound: ' · ส่งเพิ่ม (ไม่นับรอบ)' };

/** What the card says under its round, by status. The browser computes no time-based status (facility-0061). */
function detailText(point: PointCard): string {
  const sent = point.lastScan
    ? `ส่งงาน ${formatTime(point.lastScan.submittedAt)} · ${point.lastScan.cleanerName}${placementNote[point.lastScan.placement]}`
    : null;
  switch (point.status) {
    case 'PendingInspection':
      return sent ?? 'ส่งงานแล้ว';
    case 'Passed':
      return ['ตรวจผ่าน', sent].filter(Boolean).join(' · ');
    case 'Rework':
      return point.rework
        ? `ไม่สะอาด ${formatTime(point.rework.requestedAt)}: “${point.rework.defect}” · รอแม่บ้านแก้`
        : 'รอแม่บ้านแก้';
    case 'Overdue':
      return 'ยังไม่ส่งงาน · เลยเวลารอบแล้ว';
    case 'NotYetDone':
      return point.currentRound ? `ยังไม่ส่งงาน · ส่งได้ถึง ${formatClock(point.currentRound.end)}` : 'ยังไม่ส่งงาน';
    case 'BeforeFirstRound':
      return point.nextRound ? `รอบแรกของกะเริ่ม ${formatClock(point.nextRound.start)}` : 'รอบแรกของกะยังไม่เริ่ม';
    case 'OffHours':
      return 'จุดนี้ไม่มีรอบในกะนี้';
  }
}
