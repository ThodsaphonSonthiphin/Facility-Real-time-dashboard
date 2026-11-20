import { focusRing } from '../../../shared/components/styles';
import { ChartBar } from '../ChartBar/ChartBar';
import type { BuildingComparisonProps } from './type';

// Fixed label and value columns keep the plotting area aligned across every building.
const plotColumns = 'grid grid-cols-[4.5rem_minmax(0,1fr)_5rem] items-center gap-2 sm:grid-cols-[6rem_minmax(0,1fr)_7.5rem]';

/** Per-building Entry/Submission/Inspection comparison using each metric's own denominator. */
export function BuildingComparison({ title, rows, emptyText, onSelect }: BuildingComparisonProps) {
  return (
    <section
      aria-labelledby="overview-buildings-heading"
      className="flex min-w-0 flex-col gap-3"
    >
      <div className="flex flex-col gap-0.5">
        <h2 id="overview-buildings-heading" className="text-lg font-bold">
          {title}
        </h2>
      </div>
      {rows.length === 0 ? (
        <p className="rounded-lg border border-border bg-surface p-3 text-text-2">
          {emptyText ?? 'ไม่มีข้อมูลรายตึก'}
        </p>
      ) : (
        <ul className="list-none divide-y divide-border rounded-lg border border-border bg-surface px-3">
          {rows.map((row) => (
            <li key={row.id} className="flex min-w-0 flex-col gap-1 py-3">
              <h3 className="px-2 font-semibold wrap-break-word text-text">{row.label}</h3>
              <div>
                {row.cells.map((cell) => (
                  <button
                    key={cell.group}
                    type="button"
                    data-tap=""
                    aria-pressed={cell.selected}
                    aria-label={cell.ariaLabel}
                    onClick={() => onSelect(row.id, cell.group)}
                    className={`${plotColumns} min-h-12 w-full min-w-0 cursor-pointer rounded-md px-2 py-1 text-left transition-colors hover:bg-surface-2 aria-pressed:bg-accent-subtle aria-pressed:inset-ring-2 aria-pressed:inset-ring-accent lg:min-h-9 ${focusRing}`}
                  >
                    <span className="font-medium wrap-break-word text-text-3">{cell.label}</span>{' '}
                    {cell.percent !== null && <ChartBar percent={cell.percent} tone={cell.tone} />}
                    <span className="col-start-3 text-right font-semibold wrap-anywhere text-accent tabular-nums">{cell.text}</span>
                  </button>
                ))}
                {row.cells.some((cell) => cell.percent !== null) && (
                  <div aria-hidden="true" className={`${plotColumns} px-2 pt-1 text-sm text-text-2 tabular-nums`}>
                    <span className="col-start-2 flex justify-between gap-1 border-t border-border pt-1">
                      <span>0%</span>
                      <span>100%</span>
                    </span>
                  </div>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
