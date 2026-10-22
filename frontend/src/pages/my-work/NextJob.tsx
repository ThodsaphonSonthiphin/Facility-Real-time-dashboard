import { QrCode } from 'lucide-react';
import { useId } from 'react';
import type { PointStatus } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { detailOf, issueText, whatToDo, whereText, type WorkItem } from './workRules';

// The card's frame and heading take the point's status colour; ยังไม่ทำ has nothing wrong, so it stays neutral
const tone: Partial<Record<PointStatus, string>> = {
  Rework: 'border-status-rework text-status-rework',
  Overdue: 'border-status-overdue text-status-overdue',
};

/**
 * Variant B's large card "ทำอันนี้ก่อน" (#68, `pages/phone.md`): the most urgent point the Cleaner has to act on.
 * Names, defects and notes are rendered as React text only (facility-0066). It is not a link (facility-0059).
 */
export function NextJob({ item }: { item: WorkItem }) {
  const { point } = item;
  const issue = issueText(point);
  const colour = tone[point.status] ?? 'border-text text-text';
  const headingId = useId();
  return (
    <section aria-labelledby={headingId} className={`flex flex-col gap-2.5 rounded border-2 bg-surface px-4 py-4.5 ${colour}`}>
      <h2 id={headingId} className="text-md font-bold tracking-wide">
        ทำอันนี้ก่อน
      </h2>
      <div className="flex items-start justify-between gap-3 text-text">
        <h3 className="text-2xl font-bold">{point.name}</h3>
        <StatusChip status={point.status} size="lg" />
      </div>
      <span className="text-muted">{whereText(item)}</span>
      <span className="text-lg text-text">{detailOf(point)}</span>
      {issue && <span className="text-md font-semibold text-error-text">{issue}</span>}
      <span className="flex items-center gap-2 rounded bg-bg px-3 py-2.5 font-semibold text-text">
        <QrCode aria-hidden="true" className="size-5 shrink-0" />
        {whatToDo(point.status)}
      </span>
    </section>
  );
}
