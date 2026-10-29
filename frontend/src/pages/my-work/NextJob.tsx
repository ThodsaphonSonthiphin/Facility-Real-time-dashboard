import { QrCode } from 'lucide-react';
import { useId } from 'react';
import type { PointStatus } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { issueText } from '../../shared/utils/pointText';
import { detailOf, whatToDo, whereText, type WorkItem } from './workRules';

// The card's frame, band and heading take the point's status colour; ยังไม่ทำ has nothing wrong, so it stays neutral
const tone: Partial<Record<PointStatus, { frame: string; band: string; heading: string }>> = {
  Rework: { frame: 'border-rework-border', band: 'bg-rework-bg', heading: 'text-rework-strong' },
  Overdue: { frame: 'border-overdue-border', band: 'bg-overdue-bg', heading: 'text-overdue-strong' },
};
const neutral = { frame: 'border-border', band: 'bg-idle-bg', heading: 'text-text' };

/**
 * Variant B's large card "ทำอันนี้ก่อน" (#68, `pages/phone.md`), as the design file's Main: a band in the status colour
 * with the heading and the filled chip, then the point, what the Supervisor said and the order to scan. The most urgent
 * point the Cleaner has to act on. Names, defects and notes are rendered as React text only (facility-0066). It is
 * not a link (facility-0059).
 */
export function NextJob({ item }: { item: WorkItem }) {
  const { point } = item;
  const issue = issueText(point);
  const look = tone[point.status] ?? neutral;
  const headingId = useId();
  return (
    <section aria-labelledby={headingId} className={`overflow-hidden rounded-lg border bg-surface ${look.frame}`}>
      <div className={`flex items-center justify-between gap-2 px-4 py-2 ${look.band}`}>
        <h2 id={headingId} className={`text-base font-bold ${look.heading}`}>
          ทำอันนี้ก่อน
        </h2>
        <StatusChip status={point.status} size="lg" solid />
      </div>
      <div className="flex flex-col gap-3 p-4">
        <div className="flex flex-col">
          <h3 className="text-2xl font-bold">{point.name}</h3>
          <span className="text-text-2 tabular-nums">{whereText(item)}</span>
        </div>
        <span className="flex gap-3 rounded-md bg-bg p-3 text-text">{detailOf(point)}</span>
        {issue && <span className="text-sm font-semibold text-rework">{issue}</span>}
        <span className="flex items-center gap-3 border-t border-border pt-3 text-lg font-bold text-text">
          <QrCode aria-hidden="true" className="size-6 shrink-0" />
          {whatToDo(point.status)}
        </span>
      </div>
    </section>
  );
}
