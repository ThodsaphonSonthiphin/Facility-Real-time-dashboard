import { ArrowRightLeft, CalendarDays, Check, ChevronRight, Clock3, History, RotateCcw, SlidersHorizontal, TriangleAlert, X } from 'lucide-react';
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

const mobileShiftRowClass =
  'flex min-h-12 w-full cursor-pointer items-center gap-3 bg-surface px-3.5 py-2.5 text-left text-text ' +
  'transition-colors duration-150 hover:bg-surface-2 active:bg-accent-subtle ' +
  'focus-visible:outline-3 focus-visible:-outline-offset-3 focus-visible:outline-accent';

/**
 * Contextual action bar below the roster table for the currently selected `(person, day)` cell (Owner-approved Option A).
 * Mobile (< 640 px): full-width primary + baseline buttons and grouped full-width in-shift action rows with pinned top-right close.
 * Tablet / Desktop (>= 640 px): single horizontal split toolbar separating schedule actions from in-shift team actions.
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
  const hasScheduleGroup = canEditDated || canEditBaseline;
  const hasShiftGroup = Boolean(canSupervisorTeamSlot && day.shift);

  return (
    <section
      role="region"
      aria-label="จัดการวันที่เลือก"
      className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 sm:py-3"
    >
      <div className="flex items-start justify-between gap-2 sm:items-center sm:gap-4">
        <div className="flex min-w-0 flex-1 flex-col gap-1 sm:flex-row sm:flex-wrap sm:items-center sm:gap-x-2 sm:gap-y-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className="inline-flex items-center gap-1 rounded-md border border-passed-border bg-passed-bg px-2 py-0.5 text-xs font-semibold text-passed">
              <Check aria-hidden="true" className="size-4" />
              <span>เลือกอยู่</span>
            </span>
            <strong className="text-base font-bold text-text sm:text-sm">{person.displayName}</strong>
            <span className="text-sm text-text-2">({cleanerKindLabel[person.cleanerKind]})</span>
          </div>
          <span aria-hidden="true" className="hidden text-text-2 sm:inline">·</span>
          <div className="flex flex-wrap items-center gap-x-1.5 gap-y-0.5 text-sm">
            <strong className="font-semibold text-text tabular-nums">{`${formatWeekday(day.date)} ${dateText}`}</strong>
            <span aria-hidden="true" className="text-text-2">·</span>
            <span className="font-semibold text-text-3">
              {cellLabel[day.availability]}
              {day.area.value != null ? ` (พื้นที่ ${day.area.value})` : ''}
            </span>
          </div>
        </div>
        <Button variant="link" size="adaptive" className="min-w-12 shrink-0 sm:min-w-0" onClick={view.clearSelectedCell}>
          <X aria-hidden="true" className="size-4" />
          ปิด
        </Button>
      </div>

      {day.problems.length > 0 && (
        <div className="flex items-start gap-2 rounded-md border border-rework-border bg-rework-bg px-3 py-2 text-xs font-semibold text-rework">
          <TriangleAlert aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          <span>{day.problems.map((problem) => problem.message).join(' · ')}</span>
        </div>
      )}

      {(hasScheduleGroup || hasShiftGroup) && (
        phone ? (
          <div className="flex flex-col gap-2">
            {canEditDated && (
              <Button
                variant="primary"
                size="phone"
                className="w-full"
                aria-label={`${datedActionLabel} ${person.displayName} ${dateText}`}
                onClick={() => view.openDated(person, day)}
              >
                {isPastDate
                  ? <RotateCcw aria-hidden="true" className="size-5" />
                  : <CalendarDays aria-hidden="true" className="size-5" />}
                <span>{datedActionLabel}</span>
              </Button>
            )}

            {canEditBaseline && (
              <Button
                variant="secondary"
                size="phone"
                className="w-full"
                onClick={() => view.openBaseline(person)}
              >
                <SlidersHorizontal aria-hidden="true" className="size-5" />
                <span>{hasBaseline ? 'แก้ตารางประจำ' : 'ตั้งตารางประจำ'}</span>
              </Button>
            )}

            {hasShiftGroup && day.shift && (
              <div className="flex flex-col gap-1.5 pt-1">
                <span className="text-xs font-semibold text-text-2">จัดการหน้างานในกะ</span>
                <div className="divide-y divide-border overflow-hidden rounded-md border border-border-control bg-surface">
                  <button
                    type="button"
                    data-tap=""
                    className={mobileShiftRowClass}
                    aria-label={`เพิ่มเวลาที่ขาด ${person.displayName} ${dateText}`}
                    onClick={() => view.openTeamSlot('missing-time', { userId: person.userId, shiftDate: day.date, shift: day.shift! }, person.displayName)}
                  >
                    <Clock3 aria-hidden="true" className="size-5 shrink-0 text-text-3" />
                    <span className="min-w-0 flex-1">
                      <span className="block text-base font-semibold text-text">เพิ่มเวลาที่ขาด</span>
                      <span className="block text-xs text-text-2">บันทึกเวลาเข้า-ออกหรือพักที่ตกหล่น</span>
                    </span>
                    <ChevronRight aria-hidden="true" className="size-5 shrink-0 text-text-2" />
                  </button>
                  <button
                    type="button"
                    data-tap=""
                    className={mobileShiftRowClass}
                    aria-label={`ย้ายพื้นที่ระหว่างกะ ${person.displayName} ${dateText}`}
                    onClick={() => view.openTeamSlot('transfer', { userId: person.userId, shiftDate: day.date, shift: day.shift! }, person.displayName)}
                  >
                    <ArrowRightLeft aria-hidden="true" className="size-5 shrink-0 text-text-3" />
                    <span className="min-w-0 flex-1">
                      <span className="block text-base font-semibold text-text">ย้ายพื้นที่</span>
                      <span className="block text-xs text-text-2">สั่งย้ายแม่บ้านไปช่วยอีกพื้นที่ระหว่างกะ</span>
                    </span>
                    <ChevronRight aria-hidden="true" className="size-5 shrink-0 text-text-2" />
                  </button>
                </div>
              </div>
            )}
          </div>
        ) : (
          <div className="flex flex-wrap items-center gap-2 border-t border-border pt-2.5">
            {hasScheduleGroup && (
              <div className="flex flex-wrap items-center gap-2">
                {canEditDated && (
                  <Button
                    variant="primary"
                    size="desk"
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
                    size="desk"
                    onClick={() => view.openBaseline(person)}
                  >
                    <SlidersHorizontal aria-hidden="true" className="size-4" />
                    <span>{hasBaseline ? 'แก้ตารางประจำ' : 'ตั้งตารางประจำ'}</span>
                  </Button>
                )}
              </div>
            )}

            {hasScheduleGroup && hasShiftGroup && (
              <span aria-hidden="true" className="mx-1 hidden h-6 w-px bg-border sm:inline-block" />
            )}

            {hasShiftGroup && day.shift && (
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs font-medium text-text-2">หน้างานในกะ:</span>
                <Button
                  variant="secondary"
                  size="desk"
                  aria-label={`เพิ่มเวลาที่ขาด ${person.displayName} ${dateText}`}
                  onClick={() => view.openTeamSlot('missing-time', { userId: person.userId, shiftDate: day.date, shift: day.shift! }, person.displayName)}
                >
                  <Clock3 aria-hidden="true" className="size-4" />
                  <span>เพิ่มเวลาที่ขาด</span>
                </Button>
                <Button
                  variant="secondary"
                  size="desk"
                  aria-label={`ย้ายพื้นที่ระหว่างกะ ${person.displayName} ${dateText}`}
                  onClick={() => view.openTeamSlot('transfer', { userId: person.userId, shiftDate: day.date, shift: day.shift! }, person.displayName)}
                >
                  <ArrowRightLeft aria-hidden="true" className="size-4" />
                  <span>ย้ายพื้นที่</span>
                </Button>
              </div>
            )}
          </div>
        )
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
