import { useId } from 'react';
import { StatusChip } from '../../shared/components/StatusChip';
import { detailOf, issueText, whereText, type WorkItem } from './workRules';

/**
 * One of variant B's groups (ต้องทำต่อ / ส่งแล้ว / ยังไม่ต้องทำ) as short rows: name, where and round, a ต้องแก้ไข
 * point's defect, any reported issue, the chip. No row links to the scan page (facility-0059); names and notes are
 * React text only (facility-0066).
 */
export function WorkGroup({ title, items }: { title: string; items: readonly WorkItem[] }) {
  const headingId = useId();
  if (items.length === 0) {
    return null;
  }
  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-2">
      <h2 id={headingId} className="text-md font-bold tracking-wide text-muted">
        {title}
      </h2>
      <ul className="m-0 list-none divide-y divide-border rounded border border-border bg-surface p-0">
        {items.map((item) => {
          const issue = issueText(item.point);
          return (
            <li key={item.point.id} className="flex min-h-14 items-center justify-between gap-3 px-3.5 py-3">
              <div className="flex min-w-0 flex-col">
                <h3 className="font-semibold">{item.point.name}</h3>
                <span className="text-md text-muted">{whereText(item)}</span>
                {/* A ต้องแก้ไข row also says what to fix (owner, 2026-10-22), as the large card does */}
                {item.point.status === 'Rework' && (
                  <span className="text-md font-semibold text-status-rework">{detailOf(item.point)}</span>
                )}
                {issue && <span className="text-md font-semibold text-error-text">{issue}</span>}
              </div>
              <StatusChip status={item.point.status} />
            </li>
          );
        })}
      </ul>
    </section>
  );
}
