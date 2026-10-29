import type { PointStatus } from '../api/types';
import { pointStatusLabel } from '../utils/labels';
import { statusIcon } from './styles';

/** The light chip: the status's words in its colour on its pale background, with its border. */
const softTone: Record<PointStatus, string> = {
  Rework: 'border-rework-border bg-rework-bg text-rework',
  Overdue: 'border-overdue-border bg-overdue-bg text-overdue',
  PendingInspection: 'border-pending-border bg-pending-bg text-pending',
  Passed: 'border-passed-border bg-passed-bg text-passed',
  NotYetDone: 'border-idle-border bg-idle-bg text-idle',
  BeforeFirstRound: 'border-idle-border bg-idle-bg text-idle',
  OffHours: 'border-idle-border bg-idle-bg text-idle',
};

/** The filled chip: white words on the status colour. */
const solidTone: Record<PointStatus, string> = {
  Rework: 'border-rework bg-rework text-on-accent',
  Overdue: 'border-overdue bg-overdue text-on-accent',
  PendingInspection: 'border-pending bg-pending text-on-accent',
  Passed: 'border-passed bg-passed text-on-accent',
  NotYetDone: 'border-idle bg-idle text-on-accent',
  BeforeFirstRound: 'border-idle bg-idle text-on-accent',
  OffHours: 'border-idle bg-idle text-on-accent',
};

// md: 28 px, 14/600 (every list and table); lg: 32 px, 16/600 (a phone page's one point, and the filled chip)
const sizes = {
  md: 'min-h-7 px-2.5 text-sm',
  lg: 'min-h-8 px-3 text-base',
} as const;

interface StatusChipProps {
  status: PointStatus;
  size?: keyof typeof sizes;
  /** Filled with the status colour: the phone's "ทำอันนี้ก่อน" (DESIGN_SPEC §5), with `size="lg"`. */
  solid?: boolean;
  /** 124 px wide with its words in the middle, so a table's column of chips lines up (DESIGN_SPEC §5 Table). */
  fixed?: boolean;
}

/**
 * A point's status (facility-0047 rule 4) as the same chip on every page, always with its words and its icon; the
 * icon is decoration (`aria-hidden`), so the chip's text is its words alone. `min-h` rather than a fixed height and
 * `whitespace-nowrap`, so the words never spill out of the box (DESIGN_SPEC §5).
 */
export function StatusChip({ status, size = 'md', solid = false, fixed = false }: StatusChipProps) {
  const Icon = statusIcon[status];
  const tone = solid ? solidTone[status] : softTone[status];
  // w-31 is the design file's 124 px table chip; no token describes it
  const width = fixed ? 'w-31 justify-center' : '';
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-md border font-semibold whitespace-nowrap ${tone} ${sizes[size]} ${width}`}
    >
      <Icon aria-hidden="true" className="size-4 shrink-0" />
      {pointStatusLabel[status]}
    </span>
  );
}
