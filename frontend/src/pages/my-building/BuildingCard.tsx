import { QrCode } from 'lucide-react';
import type { PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { issueText, roundText } from '../../shared/utils/pointText';
import { formatClock, formatTime } from '../../shared/utils/thaiTime';
import { statusFrame } from './buildingRules';

interface BuildingCardProps {
  point: PointCard;
  /** Own building in own shift, or a cover (facility-0048): only then does a รอตรวจ card send the Supervisor to scan. */
  canInspect: boolean;
}

/** One point on My Building. Names, defects and notes are rendered as React text only (facility-0066). */
export function BuildingCard({ point, canInspect }: BuildingCardProps) {
  const issue = issueText(point);
  return (
    <li className={`flex flex-col gap-1 rounded-lg bg-surface px-3.5 py-3 ${statusFrame[point.status] ?? 'border border-border'}`}>
      <div className="flex items-start justify-between gap-2">
        <h3 className="text-lg font-semibold">{point.name}</h3>
        <StatusChip status={point.status} />
      </div>
      <span className="text-sm text-text-2 tabular-nums">{[point.areaName, roundText(point)].filter(Boolean).join(' · ')}</span>
      <span>{detailOf(point)}</span>
      {canInspect && point.status === 'PendingInspection' && (
        <span className="flex items-center gap-2 rounded-lg bg-bg px-3 py-2 font-semibold">
          <QrCode aria-hidden="true" className="size-5 shrink-0" />
          ไปที่จุดนี้แล้วสแกน QR เพื่อตรวจ
        </span>
      )}
      {issue && <span className="text-sm font-semibold text-rework">{issue}</span>}
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
