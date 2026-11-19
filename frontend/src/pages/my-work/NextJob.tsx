import { MapPin, QrCode } from 'lucide-react';
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
    <section aria-labelledby={headingId} className={`overflow-hidden rounded-xl border bg-surface shadow-xs ${look.frame}`}>
      <div className={`flex flex-wrap items-center justify-between gap-2 px-3.5 py-2 ${look.band}`}>
        <h2 id={headingId} className={`text-base font-bold ${look.heading}`}>
          ทำอันนี้ก่อน
        </h2>
        <StatusChip status={point.status} size="lg" solid />
      </div>
      <div className="flex flex-col gap-2.5 p-3.5">
        <div className="flex items-start gap-3">
          <span className="mt-0.5 flex size-10 shrink-0 items-center justify-center rounded-md bg-accent-subtle text-accent">
            <MapPin aria-hidden="true" className="size-5" />
          </span>
          <div className="flex min-w-0 flex-col">
            <h3 className="text-lg font-extrabold wrap-break-word">{point.name}</h3>
            <span className="text-sm wrap-break-word text-text-2 tabular-nums">{whereText(item)}</span>
          </div>
        </div>
        <span className="flex gap-3 rounded-md bg-bg p-3 text-text">{detailOf(point)}</span>
        {issue && <span className="text-sm font-semibold text-rework">{issue}</span>}
        <span className="flex items-center gap-2.5 border-t border-border pt-2.5 text-base font-bold text-text">
          <QrCode aria-hidden="true" className="size-6 shrink-0" />
          {whatToDo(point.status)}
        </span>
      </div>
    </section>
  );
}
