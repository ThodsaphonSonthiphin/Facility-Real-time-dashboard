import type { ChartBarProps } from './type';

const fillClass = {
  passed: 'bg-passed',
  pending: 'bg-pending',
  overdue: 'bg-overdue',
  rework: 'bg-rework',
};

/** The surrounding button supplies the category and value as text. */
export function ChartBar({ percent, tone }: ChartBarProps) {
  const width = Math.max(0, Math.min(100, percent));
  return (
    <span aria-hidden="true" className="block h-3 w-full overflow-hidden rounded-sm bg-skeleton">
      <span className={`block h-full rounded-sm ${fillClass[tone]}`} style={{ width: `${width}%` }} />
    </span>
  );
}
