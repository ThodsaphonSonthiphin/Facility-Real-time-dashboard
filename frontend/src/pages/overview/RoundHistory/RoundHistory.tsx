import { Button } from '../../../shared/components/Button';
import { Note } from '../../../shared/components/Note';
import type { RoundHistoryProps } from './type';

/** Finalized round summary section separating missed cleaning/inspection from notReworked and canceled reasons. */
export function RoundHistory({ title, subtitle, availabilityNote, groups, details, onGroup }: RoundHistoryProps) {
  return (
    <section
      aria-labelledby="overview-history-heading"
      className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex flex-col gap-1">
        <h2 id="overview-history-heading" className="text-lg font-semibold">
          {title}
        </h2>
        <p className="text-xs text-text-2">{subtitle}</p>
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
      <dl className="m-0 grid grid-cols-1 gap-2 border-t border-border pt-3 text-xs text-text-2 sm:grid-cols-2 sm:gap-4">
        {details.map((detail) => (
          <div key={detail.title} className="flex flex-col gap-0.5">
            <dt className="font-semibold text-text-3">{detail.title}</dt>
            <dd className="m-0">{detail.description}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
