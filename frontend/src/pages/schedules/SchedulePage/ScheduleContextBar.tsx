import { ArrowRightLeft, CalendarDays, Check, Clock3, History, RotateCcw, SlidersHorizontal, X } from 'lucide-react';
import { lazy, Suspense } from 'react';
import { Button } from '../../../shared/components/Button';
import { focusRing } from '../../../shared/components/styles';
import { formatThaiDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { cellLabel, cleanerKindLabel } from '../scheduleRules';
import type { SchedulePageView } from './type';

const ScheduleHistory = lazy(() => import('../ScheduleHistory/ScheduleHistory')
  .then((module) => ({ default: module.ScheduleHistory })));

export interface ScheduleContextBarProps {
  view: SchedulePageView;
}

/**
 * Contextual action bar below the roster table for the currently selected `(person, day)` cell.
 * Keeps table cells clean while preserving all per-person/per-day actions and `aria-label` contracts.
 */
export function ScheduleContextBar({ view }: ScheduleContextBarProps) {
  const { week, latest, selectedCell, role, phone, businessDate } = view;
  if (!week || !selectedCell) return null;

  const person = week.rows.find((row) => row.userId === selectedCell.userId);
  const day = person?.days.find((one) => one.date === selectedCell.date);
  if (!person || !day) return null;

  const dateText = formatThaiDate(day.date);
  const datedActionLabel = view.dateAction(day);
  const isPastDate = day.date < businessDate;
  const canEditDated = Boolean(latest && day.edit?.canChange && day.shift);
  const hasBaseline = person.days.some((one) => one.edit?.baselineAreaId != null);
  const canEditBaseline = Boolean(
    latest && person.cleanerKind === 'Regular' && person.days.some((one) => one.edit?.canChange),
  );
  const canSupervisorTeamSlot = Boolean(role === 'supervisor' && latest && day.shift && day.area.value != null);
  const size = phone ? 'phone' : 'desk';

  return (
    <section
      role="region"
      aria-label="จัดการวันที่เลือก"
      className="flex flex-col gap-2.5 rounded-xl border border-border bg-surface px-4 py-3"
    >
      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="inline-flex items-center gap-1 rounded-md border border-passed-border bg-passed-bg px-2 py-0.5 text-xs font-semibold text-passed">
            <Check aria-hidden="true" className="size-4" />
            <span>เลือกอยู่</span>
          </span>
          <strong className="font-bold text-text">{person.displayName}</strong>
          <span className="text-text-2">({cleanerKindLabel[person.cleanerKind]})</span>
          <span aria-hidden="true" className="text-text-2">·</span>
          <strong className="font-semibold text-text tabular-nums">{`${formatWeekday(day.date)} ${dateText}`}</strong>
          <span aria-hidden="true" className="text-text-2">·</span>
          <span className="font-semibold text-accent">
            {cellLabel[day.availability]}
            {day.area.value != null ? ` (พื้นที่ ${day.area.value})` : ''}
          </span>
        </div>
        <Button variant="link" size="desk" onClick={view.clearSelectedCell}>
          <X aria-hidden="true" className="size-4" />
          ปิด
        </Button>
      </div>
      {day.problems.length > 0 && (
        <span className="text-xs font-semibold text-rework">
          {day.problems.map((problem) => problem.message).join(' · ')}
        </span>
      )}

      {(canEditDated || canEditBaseline || (canSupervisorTeamSlot && day.shift)) && (
        <div className="flex flex-wrap items-center gap-2">
          {canEditDated && (
            <Button
              variant="primary"
              size={size}
              aria-label={`${datedActionLabel} ${person.displayName} ${dateText}`}
              onClick={() => view.openDated(person, day)}
            >
              {isPastDate
                ? <RotateCcw aria-hidden="true" className="size-4" />
                : <CalendarDays aria-hidden="true" className="size-4" />}
              <span>{datedActionLabel}</span>
            </Button>
          )}

          {canEditBaseline && (
            <Button
              variant="secondary"
              size={size}
              onClick={() => view.openBaseline(person)}
            >
              <SlidersHorizontal aria-hidden="true" className="size-4" />
              <span>{hasBaseline ? 'แก้ตารางประจำ' : 'ตั้งตารางประจำ'}</span>
            </Button>
          )}

          {canSupervisorTeamSlot && day.shift && (
            <>
              <Button
                variant="secondary"
                size={size}
                aria-label={`เพิ่มเวลาที่ขาด ${person.displayName} ${dateText}`}
                onClick={() => view.openTeamSlot('missing-time', { userId: person.userId, shiftDate: day.date, shift: day.shift! }, person.displayName)}
              >
                <Clock3 aria-hidden="true" className="size-4" />
                <span>เพิ่มเวลาที่ขาด</span>
              </Button>
              <Button
                variant="secondary"
                size={size}
                aria-label={`ย้ายพื้นที่ระหว่างกะ ${person.displayName} ${dateText}`}
                onClick={() => view.openTeamSlot('transfer', { userId: person.userId, shiftDate: day.date, shift: day.shift! }, person.displayName)}
              >
                <ArrowRightLeft aria-hidden="true" className="size-4" />
                <span>ย้ายพื้นที่</span>
              </Button>
            </>
          )}
        </div>
      )}

      {day.edit?.corrections.length ? (
        <details className="border-t border-border pt-2">
          <summary className={`inline-flex min-h-9 cursor-pointer items-center gap-1.5 text-sm font-semibold text-accent ${focusRing}`}>
            <History aria-hidden="true" className="size-4" />
            <span>ประวัติการแก้ไขแผน</span>
          </summary>
          <div className="mt-2">
            <Suspense fallback={<p role="status">กำลังโหลดประวัติ...</p>}>
              <ScheduleHistory corrections={day.edit.corrections} />
            </Suspense>
          </div>
        </details>
      ) : null}
    </section>
  );
}
