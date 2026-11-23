import type { KpiCardProps, KpiCardTone } from './type';

const iconToneClass: Record<KpiCardTone, string> = {
  teal: 'text-pending',
  green: 'text-passed',
  amber: 'text-overdue',
  red: 'text-rework',
  purple: 'text-break',
  neutral: 'text-text-2',
};

const progressFillClass: Record<KpiCardTone, string> = {
  teal: 'bg-pending',
  green: 'bg-passed',
  amber: 'bg-overdue',
  red: 'bg-rework',
  purple: 'bg-break',
  neutral: 'bg-idle',
};

const progressTextClass: Record<KpiCardTone, string> = {
  teal: 'text-pending',
  green: 'text-passed',
  amber: 'text-overdue',
  red: 'text-rework',
  purple: 'text-break',
  neutral: 'text-text-2',
};

/**
 * Enterprise Light KPI summary card with semantic icon and optional accessible progressbar.
 */
export function KpiCard({
  label,
  value,
  unit,
  icon: Icon,
  tone = 'neutral',
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
      className={`flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 ${className ?? ''}`}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="text-xs font-semibold text-text-2">{label}</div>
          <div className="mt-1 text-2xl/tight font-bold text-text tabular-nums">
            {formattedValue}
            {unit ? (
              <>
                {' '}
                <small className="text-xs font-semibold text-text-2">{unit}</small>
              </>
            ) : null}
          </div>
        </div>
        <Icon aria-hidden="true" className={`mt-0.5 size-4 shrink-0 ${iconToneClass[tone]}`} strokeWidth={2} />
      </div>
      {clampedProgress !== undefined && (
        <div className="flex items-center gap-2">
          <div
            role="progressbar"
            aria-label={progressLabel ?? label}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={clampedProgress}
            className="h-2 flex-1 overflow-hidden rounded-sm bg-skeleton"
          >
            <div className={`h-full rounded-sm ${progressFillClass[tone]}`} style={{ width: `${clampedProgress}%` }} />
          </div>
          <span aria-hidden="true" className={`text-xs font-bold tabular-nums ${progressTextClass[tone]}`}>
            {clampedProgress}%
          </span>
        </div>
      )}
    </Tag>
  );
}
