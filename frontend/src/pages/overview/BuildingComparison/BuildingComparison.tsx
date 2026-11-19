import { Button } from '../../../shared/components/Button';
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
      className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="overview-buildings-heading" className="text-lg font-semibold">
          {title}
        </h2>
        <span className="text-xs text-text-2">{subtitle}</span>
      </div>
      {rows.length === 0 ? (
        <p className="text-text-2">{emptyText ?? 'ไม่มีข้อมูลเปรียบเทียบรายตึกในบริบทนี้'}</p>
      ) : (
        <div className="flex flex-col gap-3">
          {rows.map((row) => (
            <div
              key={row.id}
              className="grid grid-cols-1 items-center gap-2 border-b border-border pb-3 last:border-b-0 last:pb-0 md:grid-cols-[minmax(160px,1.2fr)_repeat(3,minmax(0,1fr))] md:gap-3"
            >
              <strong className="font-semibold text-text">{row.label}</strong>
              {row.cells.map((cell) => (
                <Button
                  key={cell.group}
                  size="desk"
                  variant={cell.selected ? 'primary' : 'secondary'}
                  aria-pressed={cell.selected}
                  aria-label={cell.ariaLabel}
                  onClick={() => onSelect(row.id, cell.group)}
                  className="w-full py-1.5"
                >
                  <span className="flex w-full flex-col gap-1 text-left">
                    <span className="flex items-center justify-between gap-2">
                      <span className="text-xs font-medium">{cell.label}</span>
                      <span className="tabular-nums">{cell.text}</span>
                    </span>
                    <span aria-hidden="true" className="block h-1.5 w-full overflow-hidden rounded-sm bg-skeleton">
                      {cell.percent !== null && (
                        <span
                          className={`block h-full rounded-sm ${fillToneClass[cell.tone]}`}
                          style={{ width: `${cell.percent}%` }}
                        />
                      )}
                    </span>
                  </span>
                </Button>
              ))}
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
