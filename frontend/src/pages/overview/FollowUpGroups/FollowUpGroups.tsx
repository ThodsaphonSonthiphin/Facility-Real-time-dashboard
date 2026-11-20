import { ChevronRight, ClipboardCheck, Clock3, TriangleAlert } from 'lucide-react';
import { focusRing } from '../../../shared/components/styles';
import type { FollowUpGroupsProps } from './type';

const statusLooks = {
  overdue: { Icon: Clock3, className: 'bg-overdue-bg text-overdue' },
  pending: { Icon: ClipboardCheck, className: 'bg-pending-bg text-pending' },
  rework: { Icon: TriangleAlert, className: 'bg-rework-bg text-rework' },
} as const;

/** Action rows for the follow-up groups. An Area may appear in more than one group. */
export function FollowUpGroups({ title, subtitle, items, onGroup }: FollowUpGroupsProps) {
  return (
    <section
      aria-labelledby="overview-followup-heading"
      className="flex min-w-0 flex-col gap-3"
    >
      <div className="flex flex-col gap-0.5">
        <h2 id="overview-followup-heading" className="text-lg font-bold">
          {title}
        </h2>
        <span className="text-xs text-text-2">{subtitle}</span>
      </div>
      <ul className="list-none divide-y divide-border rounded-lg border border-border bg-surface px-3">
        {items.map((item) => {
          const { Icon, className } = statusLooks[item.tone];
          return (
            <li key={item.key}>
              <button
                type="button"
                data-tap=""
                aria-label={`${item.label} ${item.text} พื้นที่`}
                aria-pressed={item.selected}
                onClick={() => onGroup(item.key)}
                className={`flex min-h-12 w-full cursor-pointer items-center gap-2 rounded-md px-1 py-2 text-left transition-colors hover:bg-surface-2 aria-pressed:bg-accent-subtle aria-pressed:inset-ring-2 aria-pressed:inset-ring-accent ${focusRing}`}
              >
                <span className={`inline-grid size-8 shrink-0 place-items-center rounded-md ${className}`}>
                  <Icon aria-hidden="true" className="size-4" />
                </span>
                <span className="min-w-0 flex-1 font-semibold wrap-break-word">{item.label}</span>
                <span className="shrink-0 text-text-3 tabular-nums">{item.text} พื้นที่</span>
                <ChevronRight aria-hidden="true" className="size-4 shrink-0 text-accent" />
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
