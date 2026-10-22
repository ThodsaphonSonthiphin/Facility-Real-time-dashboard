import type { PointStatus } from '../api/types';
import { pointStatusLabel } from '../utils/labels';

// facility-0087 rule 3: the three statuses with nothing to do share one grey
const colours: Record<PointStatus, string> = {
  Rework: 'bg-status-rework',
  Overdue: 'bg-status-overdue',
  PendingInspection: 'bg-status-pending',
  Passed: 'bg-status-passed',
  NotYetDone: 'bg-status-idle',
  BeforeFirstRound: 'bg-status-idle',
  OffHours: 'bg-status-idle',
};

const sizes = {
  md: 'px-2.5 py-0.5 text-sm',
  lg: 'px-3.5 py-1 text-base',
} as const;

/** A point's status (facility-0047 rule 4) as the same chip on every page, always with its words. */
export function StatusChip({ status, size = 'md' }: { status: PointStatus; size?: keyof typeof sizes }) {
  return (
    <span
      className={`inline-flex items-center rounded-full font-semibold whitespace-nowrap text-on-primary ${colours[status]} ${sizes[size]}`}
    >
      {pointStatusLabel[status]}
    </span>
  );
}
