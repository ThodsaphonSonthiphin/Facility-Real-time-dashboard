import { focusRing } from '../../../shared/components/styles';
import type { CoveragePanelProps, SwatchTone } from './type';

const swatchToneClass: Record<SwatchTone, string> = {
  passed: 'bg-passed',
  pending: 'bg-pending',
  overdue: 'bg-overdue',
  rework: 'bg-rework',
  idle: 'bg-idle',
};

const ringColor = { passed: 'var(--color-passed)', pending: 'var(--color-pending)' };

/** A coverage metric and its drill-through groups within the shared shift summary. */
export function CoveragePanel({ title, metric, groups, onGroup }: CoveragePanelProps) {
  return (
    <section className="flex min-w-0 flex-col gap-3 p-4">
      <h3 className="text-center text-base font-semibold text-text">{title}</h3>
      <div className="flex flex-col items-center gap-2 text-center">
        <div className="relative grid size-36 place-items-center">
          {metric.percent !== null && (
            <span
              aria-hidden="true"
              className="absolute inset-0 rounded-full bg-skeleton"
              style={{
                backgroundImage: `conic-gradient(${ringColor[metric.tone]} ${Math.max(0, Math.min(100, metric.percent))}%, transparent 0%)`,
              }}
            />
          )}
          <p className="relative grid size-28 place-items-center rounded-full bg-surface p-2 text-lg font-extrabold tabular-nums wrap-anywhere">
            <span>{metric.text}</span>
          </p>
        </div>
        {metric.denominatorText && <p className="text-sm text-text-2">{metric.denominatorText}</p>}
      </div>
      {metric.explanation && <p className="text-sm text-text-2">{metric.explanation}</p>}
      <div className="flex flex-wrap gap-1">
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
