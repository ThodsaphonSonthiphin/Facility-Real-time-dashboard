import { ChevronRight } from 'lucide-react';
import { focusRing } from '../../../shared/components/styles';
import { ChartBar } from '../ChartBar/ChartBar';
import type { FollowUpGroupsProps } from './type';

export function FollowUpGroups({ title, max, items, onGroup }: FollowUpGroupsProps) {
  return (
    <section
      aria-labelledby="overview-followup-heading"
      className="flex min-w-0 flex-col gap-3"
    >
      <div className="flex flex-col gap-0.5">
        <h2 id="overview-followup-heading" className="text-lg font-bold">
          {title}
        </h2>
      </div>
      <div className="rounded-lg border border-border bg-surface px-3 py-2">
        <ul className="list-none">
          {items.map((item) => (
            <li key={item.key}>
              <button
                type="button"
                data-tap=""
                aria-pressed={item.selected}
                onClick={() => onGroup(item.key)}
                className={`flex min-h-12 w-full cursor-pointer flex-col gap-1.5 rounded-md px-2 py-2 text-left transition-colors hover:bg-surface-2 aria-pressed:bg-accent-subtle aria-pressed:inset-ring-2 aria-pressed:inset-ring-accent ${focusRing}`}
              >
                <span className="flex w-full items-center gap-2">
                  <span className="min-w-0 flex-1 font-semibold wrap-break-word">{item.label}</span>{' '}
                  <span className="shrink-0 text-text-3 tabular-nums">{item.text} พื้นที่</span>
                  <ChevronRight aria-hidden="true" className="size-4 shrink-0 text-accent" />
                </span>
                {item.percent !== null && <ChartBar percent={item.percent} tone={item.tone} />}
              </button>
            </li>
          ))}
        </ul>
        {max > 0 && (
          <div aria-hidden="true" className="flex justify-between px-2 pt-1 text-sm text-text-2 tabular-nums">
            <span>0</span>
            <span>{max} พื้นที่</span>
          </div>
        )}
      </div>
    </section>
  );
}
