import { focusRing } from '../../../shared/components/styles';
import type { BuildingComparisonProps } from './type';

const fillToneClass: Record<'passed' | 'pending', string> = {
  passed: 'bg-passed',
  pending: 'bg-pending',
};

/** Per-building Entry/Submission/Inspection comparison using each metric's own denominator. */
export function BuildingComparison({ title, subtitle, rows, emptyText, onSelect }: BuildingComparisonProps) {
  return (
    <section
      aria-labelledby="overview-buildings-heading"
      className="flex min-w-0 flex-col gap-3"
    >
      <div className="flex flex-col gap-0.5">
        <h2 id="overview-buildings-heading" className="text-lg font-bold">
          {title}
        </h2>
        <span className="text-xs text-text-2">{subtitle}</span>
      </div>
      {rows.length === 0 ? (
        <p className="rounded-lg border border-border bg-surface p-3 text-text-2">
          {emptyText ?? 'ไม่มีข้อมูลเปรียบเทียบรายตึกในบริบทนี้'}
        </p>
      ) : (
        <ul className="list-none divide-y divide-border rounded-lg border border-border bg-surface px-3">
          {rows.map((row) => (
            <li key={row.id} className="flex min-w-0 flex-col gap-1 py-2">
              <strong className="font-semibold wrap-break-word text-text">{row.label}</strong>
              <div className="grid grid-cols-3 gap-1">
                {row.cells.map((cell) => (
                  <button
                    key={cell.group}
                    type="button"
                    data-tap=""
                    aria-pressed={cell.selected}
                    aria-label={cell.ariaLabel}
                    onClick={() => onSelect(row.id, cell.group)}
                    className={`flex min-h-12 min-w-0 cursor-pointer flex-col justify-center gap-1 rounded-md px-1 py-1 text-left transition-colors hover:bg-surface-2 aria-pressed:bg-accent-subtle aria-pressed:inset-ring-2 aria-pressed:inset-ring-accent sm:px-2 lg:min-h-9 ${focusRing}`}
                  >
                    <span className="text-xs font-medium text-text-3">{cell.label}</span>
                    <span className="font-semibold wrap-break-word text-accent tabular-nums">{cell.text}</span>
                    {cell.percent !== null && (
                      <span aria-hidden="true" className="block h-1 w-full overflow-hidden rounded-sm bg-skeleton">
                        <span
                          className={`block h-full rounded-sm ${fillToneClass[cell.tone]}`}
                          style={{ width: `${cell.percent}%` }}
                        />
                      </span>
                    )}
                  </button>
                ))}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
