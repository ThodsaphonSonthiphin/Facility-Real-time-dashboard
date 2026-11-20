import { ArrowRight, Check, Clock, Minus, RotateCcw, Users } from 'lucide-react';
import { Link } from 'react-router';
import { Table } from '../../../shared/components/Boxes';
import { Button } from '../../../shared/components/Button';
import { Note } from '../../../shared/components/Note';
import { PageEmpty } from '../../../shared/components/PageState';
import { buttonClass, tableCell } from '../../../shared/components/styles';
import type { AreaResultBadge, AreaResultsProps } from './type';

const updatingLabel = 'กำลังอัปเดต...';

const badgeTones: Record<AreaResultBadge['tone'], string> = {
  passed: 'border-passed-border bg-passed-bg text-passed',
  pending: 'border-pending-border bg-pending-bg text-pending',
  overdue: 'border-overdue-border bg-overdue-bg text-overdue',
  rework: 'border-rework-border bg-rework-bg text-rework',
  idle: 'border-idle-border bg-idle-bg text-idle',
};

const entryIcons: Record<'passed' | 'pending' | 'overdue', typeof Check> = {
  passed: Check,
  pending: Users,
  overdue: Clock,
};

/** Paginated Area results matching the active group, rendered inside a self-scrolling shared Table. */
export function AreaResults({
  title,
  contextText,
  countText,
  availabilityWarning,
  updating = false,
  rows,
  emptyState,
  pagination,
  onPage,
}: AreaResultsProps) {
  return (
    <section
      aria-labelledby="overview-results-heading"
      className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="overview-results-heading" className="text-lg font-semibold">
          {title}
        </h2>
        <div className="flex flex-wrap items-center gap-2 text-xs text-text-2">
          <span role="status" className="font-semibold text-text-3">
            {updating ? updatingLabel : <span aria-hidden="true" className="invisible">{updatingLabel}</span>}
          </span>
          <span>{contextText}</span>
        </div>
      </div>
      {availabilityWarning && (
        <Note tone="warn" alert={false}>
          {availabilityWarning}
        </Note>
      )}
      <div
        aria-busy={updating || undefined}
        className={`motion-safe:transition-colors duration-150${updating ? ' rounded-md bg-surface-2' : ''}`}
      >
        {rows.length === 0 && emptyState ? (
          emptyState.kind === 'empty' ? (
            <PageEmpty title={emptyState.title}>{emptyState.detail}</PageEmpty>
          ) : (
            <Note tone={emptyState.kind === 'notStarted' ? 'info' : 'warn'} alert={false} title={emptyState.title}>
              {emptyState.detail}
            </Note>
          )
        ) : (
          <Table
            label={title}
            framed={false}
            footer={
              <div className="flex flex-wrap items-center justify-between gap-3 px-2 py-3 text-text-2">
                <span className="tabular-nums">{countText}</span>
                <div className="flex gap-2">
                  <Button
                    size="desk"
                    disabled={!pagination.hasPrevious || pagination.disabled}
                    onClick={() => onPage(pagination.page - 1)}
                  >
                    ก่อนหน้า
                  </Button>
                  <Button
                    size="desk"
                    disabled={!pagination.hasNext || pagination.disabled}
                    onClick={() => onPage(pagination.page + 1)}
                  >
                    ถัดไป
                  </Button>
                </div>
              </div>
            }
          >
            <thead>
              <tr>
                <th scope="col" className="min-w-44">
                  พื้นที่
                </th>
                <th scope="col" className="min-w-32">
                  ตึก
                </th>
                <th scope="col" className="min-w-52">
                  สถานะและเหตุผล
                </th>
                <th scope="col" className="min-w-40">
                  จำนวนจุด
                </th>
                <th scope="col" className={`relative ${tableCell.actions}`}>
                  <span className="sr-only">เปิดดู</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => {
                const EntryIcon =
                  row.entryBadge.tone === 'passed' || row.entryBadge.tone === 'pending' || row.entryBadge.tone === 'overdue'
                    ? entryIcons[row.entryBadge.tone]
                    : Minus;
                return (
                  <tr key={row.areaId}>
                    <th scope="row" className="font-semibold">
                      <div>{`${row.code} · ${row.name}`}</div>
                      {row.originsIncomplete && (
                        <span className="mt-1 inline-flex items-center gap-1 rounded-sm border border-overdue-border bg-overdue-bg px-1.5 py-0.5 text-xs font-semibold text-overdue">
                          <RotateCcw aria-hidden="true" className="size-3 shrink-0" />
                          ข้อมูลจับคู่รอบไม่ครบ
                        </span>
                      )}
                    </th>
                    <td>{row.buildingText}</td>
                    <td>
                      <div className="flex flex-wrap items-center gap-1.5">
                        <span
                          className={`inline-flex min-h-6 items-center gap-1 rounded-md border px-2 text-xs font-semibold whitespace-nowrap ${badgeTones[row.entryBadge.tone]}`}
                        >
                          <EntryIcon aria-hidden="true" className="size-3.5 shrink-0" />
                          {row.entryBadge.label}
                        </span>
                        {row.reasonBadges.map((reason, idx) => (
                          <span
                            key={`${reason.label}:${idx}`}
                            className={`inline-flex min-h-6 items-center rounded-sm border px-2 text-xs font-semibold whitespace-nowrap ${badgeTones[reason.tone]}`}
                          >
                            {reason.label}
                          </span>
                        ))}
                      </div>
                    </td>
                    <td className="tabular-nums">{row.pointsText}</td>
                    <td className={tableCell.actions}>
                      <div className="flex flex-col items-end gap-1">
                        <Link
                          to={row.href}
                          data-tap=""
                          aria-label={row.viewAriaLabel}
                          className={buttonClass('secondary', 'desk')}
                        >
                          ดูพื้นที่
                          <ArrowRight aria-hidden="true" className="size-4" />
                        </Link>
                        {row.singleRoundLabel && (
                          <span className="text-xs text-text-2">{row.singleRoundLabel}</span>
                        )}
                        {row.roundLinks.length > 0 && (
                          <div className="flex flex-wrap justify-end gap-x-3 gap-y-1">
                            {row.roundLinks.map((roundLink) => (
                              <Link
                                key={roundLink.key}
                                to={roundLink.href}
                                data-tap=""
                                aria-label={roundLink.ariaLabel}
                                className={buttonClass('link', 'desk')}
                              >
                                {roundLink.label}
                              </Link>
                            ))}
                          </div>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </Table>
        )}
      </div>
    </section>
  );
}
