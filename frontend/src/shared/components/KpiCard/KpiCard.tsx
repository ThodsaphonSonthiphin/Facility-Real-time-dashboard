import type { KpiCardProps, KpiCardTone } from './type';

const iconCircleClass: Record<KpiCardTone, string> = {
  blue: 'bg-accent-subtle text-accent',
  green: 'bg-passed-bg text-passed',
  amber: 'bg-overdue-bg text-overdue',
  red: 'bg-rework-bg text-rework',
  purple: 'bg-break-bg text-break',
  neutral: 'bg-surface-2 text-text-3',
};

const progressFillClass: Record<KpiCardTone, string> = {
  blue: 'bg-accent',
  green: 'bg-passed',
  amber: 'bg-overdue',
  red: 'bg-rework',
  purple: 'bg-break',
  neutral: 'bg-idle',
};

const progressTextClass: Record<KpiCardTone, string> = {
  blue: 'text-accent',
  green: 'text-passed',
  amber: 'text-overdue',
  red: 'text-rework',
  purple: 'text-break',
  neutral: 'text-text-2',
};

/**
 * Enterprise Light KPI summary card with semantic icon badge and optional accessible progressbar.
 */
export function KpiCard({
  label,
  value,
  unit,
  icon: Icon,
  tone = 'blue',
  progress,
  progressLabel,
  as = 'div',
  className,
}: KpiCardProps) {
  const Tag = as;
  const formattedValue = typeof value === 'number' ? value.toLocaleString('en-US') : value;
  const clampedProgress = progress !== undefined ? Math.max(0, Math.min(100, Math.round(progress))) : undefined;

  return (
    <Tag
      className={`flex flex-col gap-2.5 rounded-xl border border-border bg-surface p-3.5${className ? ` ${className}` : ''}`}
    >
      <div className="flex items-center gap-3">
        <div className={`flex size-10 shrink-0 items-center justify-center rounded-full ${iconCircleClass[tone]}`}>
          <Icon aria-hidden="true" className="size-6" strokeWidth={2} />
        </div>
        <div className="min-w-0">
          <div className="text-xs font-semibold text-text-2">{label}</div>
          <div className="text-2xl leading-tight font-bold text-text tabular-nums">
            {formattedValue}
            {unit ? (
              <>
                {' '}
                <small className="text-xs font-semibold text-text-2">{unit}</small>
              </>
            ) : null}
          </div>
        </div>
      </div>
      {clampedProgress !== undefined && (
        <div className="flex items-center gap-2">
          <div
            role="progressbar"
            aria-label={progressLabel ?? label}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={clampedProgress}
            className="h-2 flex-1 overflow-hidden rounded-full bg-skeleton"
          >
            <div className={`h-full rounded-full ${progressFillClass[tone]}`} style={{ width: `${clampedProgress}%` }} />
          </div>
          <span aria-hidden="true" className={`text-xs font-bold tabular-nums ${progressTextClass[tone]}`}>
            {clampedProgress}%
          </span>
        </div>
      )}
    </Tag>
  );
}
