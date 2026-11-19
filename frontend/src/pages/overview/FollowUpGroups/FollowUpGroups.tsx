import { Button } from '../../../shared/components/Button';
import type { FollowUpGroupsProps } from './type';

const dotToneClass: Record<FollowUpGroupsProps['items'][number]['tone'], string> = {
  overdue: 'bg-overdue',
  pending: 'bg-pending',
  rework: 'bg-rework',
};

/** Separate clickable buttons for overlapping follow-up groups so shared Areas are never merged into one pie. */
export function FollowUpGroups({ title, subtitle, items, onGroup }: FollowUpGroupsProps) {
  return (
    <section
      aria-labelledby="overview-followup-heading"
      className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="overview-followup-heading" className="text-lg font-semibold">
          {title}
        </h2>
        <span className="text-xs text-text-2">{subtitle}</span>
      </div>
      <div className="flex flex-wrap gap-2">
        {items.map((item) => (
          <Button
            key={item.key}
            size="desk"
            variant={item.selected ? 'primary' : 'secondary'}
            aria-pressed={item.selected}
            onClick={() => onGroup(item.key)}
          >
            <span
              aria-hidden="true"
              className={`size-2.5 shrink-0 rounded-full border border-surface ${dotToneClass[item.tone]}`}
            />
            <span>
              {item.label} {item.text} พื้นที่
            </span>
          </Button>
        ))}
      </div>
    </section>
  );
}
