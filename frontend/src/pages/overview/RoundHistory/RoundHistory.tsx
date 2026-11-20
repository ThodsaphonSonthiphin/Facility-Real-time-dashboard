import { Button } from '../../../shared/components/Button';
import { Note } from '../../../shared/components/Note';
import type { RoundHistoryProps } from './type';

/** Finalized round summary for missed cleaning and inspection. */
export function RoundHistory({ title, subtitle, availabilityNote, groups, onGroup }: RoundHistoryProps) {
  return (
    <section
      aria-labelledby="overview-history-heading"
      className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex flex-col gap-1">
        <h2 id="overview-history-heading" className="text-lg font-semibold">
          {title}
        </h2>
        {subtitle && <p className="text-xs text-text-2">{subtitle}</p>}
      </div>
      {availabilityNote && (
        <Note tone="warn" alert={false}>
          {availabilityNote}
        </Note>
      )}
      <div className="flex flex-wrap gap-2">
        {groups.map((group) => (
          <Button
            key={group.key}
            size="desk"
            variant={group.selected ? 'primary' : 'secondary'}
            aria-pressed={group.selected}
            onClick={() => onGroup(group.key)}
          >
            {group.label} {group.text} พื้นที่
          </Button>
        ))}
      </div>
    </section>
  );
}
