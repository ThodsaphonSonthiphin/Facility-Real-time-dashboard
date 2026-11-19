import { Button } from '../../../shared/components/Button';
import type { CoveragePanelProps, RingTone, SwatchTone } from './type';

const ringStroke: Record<RingTone, string> = {
  passed: 'var(--color-passed)',
  pending: 'var(--color-pending)',
  overdue: 'var(--color-overdue)',
};

const swatchToneClass: Record<SwatchTone, string> = {
  passed: 'bg-passed',
  pending: 'bg-pending',
  overdue: 'bg-overdue',
  rework: 'bg-rework',
  idle: 'bg-idle',
};

/** One summary progress panel with an accessible number, decorative ring, explanation, and category buttons. */
export function CoveragePanel({ title, metric, groups, onGroup }: CoveragePanelProps) {
  const segments = metric.segments ?? [];
  return (
    <section className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4">
      <h2 className="text-lg font-semibold">{title}</h2>
      <div className="flex flex-wrap items-center gap-4">
        <div className="relative flex size-24 shrink-0 items-center justify-center">
          <svg aria-hidden="true" viewBox="0 0 120 120" className="size-24 -rotate-90">
            <circle cx="60" cy="60" r="48" fill="none" stroke="var(--color-skeleton)" strokeWidth="12" />
            {segments.map((segment) => (
              <circle
                key={segment.key}
                cx="60"
                cy="60"
                r="48"
                fill="none"
                stroke={ringStroke[segment.tone]}
                strokeWidth="12"
                strokeDasharray={segment.dashArray}
                strokeDashoffset={segment.dashOffset}
              />
            ))}
          </svg>
          {metric.ringValueText !== undefined && (
            <span aria-hidden="true" className="absolute text-base font-bold tabular-nums text-text">
              {metric.ringValueText}
            </span>
          )}
        </div>
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <p className="text-3xl font-bold tabular-nums">{metric.text}</p>
          {metric.denominatorText && <p className="text-xs text-text-2">{metric.denominatorText}</p>}
        </div>
      </div>
      <p className="text-text-2">{metric.explanation}</p>
      <div className="mt-auto flex flex-wrap gap-2">
        {groups.map((group) => (
          <Button
            key={group.key}
            size="desk"
            variant={group.selected ? 'primary' : 'secondary'}
            aria-pressed={group.selected}
            onClick={() => onGroup(group.key)}
          >
            {group.tone && (
              <span
                aria-hidden="true"
                className={`size-2.5 shrink-0 rounded-full border border-surface ${swatchToneClass[group.tone]}`}
              />
            )}
            <span>
              {group.label} {group.text} พื้นที่
            </span>
          </Button>
        ))}
      </div>
    </section>
  );
}
