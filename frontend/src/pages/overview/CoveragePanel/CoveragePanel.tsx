import { focusRing } from '../../../shared/components/styles';
import type { CoveragePanelProps, SwatchTone } from './type';

const swatchToneClass: Record<SwatchTone, string> = {
  passed: 'bg-passed',
  pending: 'bg-pending',
  overdue: 'bg-overdue',
  rework: 'bg-rework',
  idle: 'bg-idle',
};

/** A coverage metric and its drill-through groups within the shared shift summary. */
export function CoveragePanel({ title, metric, groups, onGroup }: CoveragePanelProps) {
  return (
    <section className="flex min-w-0 flex-col gap-2 p-4">
      <h3 className="text-base font-semibold text-text-2">{title}</h3>
      <div className="flex min-w-0 flex-col gap-1">
        <p className="text-3xl font-extrabold tabular-nums">{metric.text}</p>
        {metric.denominatorText && <p className="text-xs text-text-2">{metric.denominatorText}</p>}
      </div>
      {metric.percent !== null && (
        <span aria-hidden="true" className="block h-1.5 overflow-hidden rounded-sm bg-skeleton">
          <span
            className={`block h-full rounded-sm ${swatchToneClass[metric.tone]}`}
            style={{ width: `${metric.percent}%` }}
          />
        </span>
      )}
      <p className="text-xs text-text-2">{metric.explanation}</p>
      <div className="mt-auto flex flex-wrap gap-1">
        {groups.map((group) => (
          <button
            key={group.key}
            type="button"
            data-tap=""
            aria-pressed={group.selected}
            onClick={() => onGroup(group.key)}
            className={`inline-flex min-h-12 max-w-full cursor-pointer items-center gap-1.5 rounded-md px-2 text-left text-sm font-semibold text-accent transition-colors hover:bg-surface-2 aria-pressed:bg-accent-subtle aria-pressed:inset-ring-2 aria-pressed:inset-ring-accent lg:min-h-9 ${focusRing}`}
          >
            {group.tone && (
              <span
                aria-hidden="true"
                className={`size-2 shrink-0 rounded-full ${swatchToneClass[group.tone]}`}
              />
            )}
            <span>
              {group.label} {group.text} พื้นที่
            </span>
          </button>
        ))}
      </div>
    </section>
  );
}
