import type { PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatClock, formatRound, formatTime } from '../../shared/utils/thaiTime';

/** One point on My Work. Names, defects and notes are rendered as React text only (facility-0066). */
export function WorkCard({ point }: { point: PointCard }) {
  const round = point.currentRound
    ? `รอบ ${formatRound(point.currentRound)}`
    : point.nextRound
      ? `รอบถัดไป ${formatRound(point.nextRound)}`
      : null;

  return (
    <li className={`mywork-card mywork-card--${point.status}`}>
      <div className="mywork-card__top">
        <h3 className="mywork-card__name">{point.name}</h3>
        <StatusChip status={point.status} />
      </div>
      {round && <span className="mywork-card__round">{round}</span>}
      <span className="mywork-card__detail">{detailOf(point)}</span>
      {point.rework && <span className="mywork-card__todo">แก้แล้วสแกน QR ที่จุดนี้เพื่อส่งงานใหม่</span>}
      {point.issue && (
        <span className="mywork-card__issue">
          {`แจ้งปัญหา: ${[...point.issue.tags, point.issue.note].filter(Boolean).join(' · ')}`}
        </span>
      )}
    </li>
  );
}

function detailOf(point: PointCard): string {
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
