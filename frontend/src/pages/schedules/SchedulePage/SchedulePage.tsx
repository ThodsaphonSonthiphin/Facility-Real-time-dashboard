import { ArrowRightLeft, CalendarDays, ChevronLeft, ChevronRight, RefreshCw, UserCheck } from 'lucide-react';
import { lazy, Suspense } from 'react';
import { useGetBuildingsQuery } from '../../../shared/api/adminSetupApi';
import { buildingOptionText } from '../../../shared/api/buildingNames';
import type { ScheduleAvailability, ScheduleDay, ScheduleWeek } from '../../../shared/api/scheduleTypes';
import type { Shift } from '../../../shared/api/types';
import { AdminPageHeader } from '../../../shared/components/AdminPageHeader/AdminPageHeader';
import { AvatarBadge } from '../../../shared/components/AvatarBadge/AvatarBadge';
import { Badge } from '../../../shared/components/Badge';
import { BuildingSelect } from '../../../shared/components/BuildingSelect';
import { Button } from '../../../shared/components/Button';
import { HorizontalScrollArea } from '../../../shared/components/Boxes';
import { ConnectionBanner } from '../../../shared/components/ConnectionBanner/ConnectionBanner';
import { Select } from '../../../shared/components/Field';
import { Note } from '../../../shared/components/Note';
import { PageEmpty, PageLoading } from '../../../shared/components/PageState';
import { PhoneHeader, PhoneScreen } from '../../../shared/components/PhoneScreen';
import { Segmented } from '../../../shared/components/Segmented';
import { focusRing } from '../../../shared/components/styles';
import { FilterToolbar } from '../../../shared/components/SurfacePanel';
import { Toast } from '../../../shared/components/Toast';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { shiftShortLabel, transferStateLabel } from '../../../shared/utils/labels';
import { formatDayTime, formatShiftDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { availabilityLabel, cellLabel, cleanerKindLabel, sourceLabel, weekIsUnavailable, weekRangeLabel, type ScheduleRole } from '../scheduleRules';
import type { TeamSlotArgs } from '../schedulesApi';
import { DesktopScheduleActions, MobileScheduleActionSheet } from './ScheduleActionsMenu';
import { ScheduleContextBar } from './ScheduleContextBar';
import { ScheduleSummaryCards } from './ScheduleSummaryCards';
import { useSchedulePage } from './useSchedulePage';

const WeeklyOffEditor = lazy(() => import('../WeeklyOffEditor/WeeklyOffEditor')
  .then((module) => ({ default: module.WeeklyOffEditor })));
const RegularBaselineEditor = lazy(() => import('../RegularBaselineEditor/RegularBaselineEditor')
  .then((module) => ({ default: module.RegularBaselineEditor })));
const DatedScheduleEditor = lazy(() => import('../DatedScheduleEditor/DatedScheduleEditor')
  .then((module) => ({ default: module.DatedScheduleEditor })));
const ReliefRosterEditor = lazy(() => import('../ReliefRosterEditor/ReliefRosterEditor')
  .then((module) => ({ default: module.ReliefRosterEditor })));
const ReliefAssignmentEditor = lazy(() => import('../ReliefAssignmentEditor/ReliefAssignmentEditor')
  .then((module) => ({ default: module.ReliefAssignmentEditor })));
const ReliefLoanEditor = lazy(() => import('../ReliefLoanEditor/ReliefLoanEditor')
  .then((module) => ({ default: module.ReliefLoanEditor })));
const TeamMissingTime = lazy(() => import('../TeamMissingTime/TeamMissingTime')
  .then((module) => ({ default: module.TeamMissingTime })));
const TransferInstruction = lazy(() => import('../TransferInstruction/TransferInstruction')
  .then((module) => ({ default: module.TransferInstruction })));
const CoverDrawer = lazy(() => import('../CoverDrawer')
  .then((module) => ({ default: module.CoverDrawer })));

/**
 * Cleaner's own weekly schedule (`/my-schedule`): top-level tab in CleanerBottomNav (no redundant back button,
 * no stacked BottomBar, compact top-right refresh, and quick `สัปดาห์นี้` shortcut).
 */
export function OwnSchedulePage() {
  const view = useSchedulePage('cleaner');
  return (
    <PhoneScreen
      header={
        <PhoneHeader
          action={
            <Button variant="secondary" size="desk" className="min-h-12" disabled={view.isFetching} onClick={view.refetch}>
              <RefreshCw aria-hidden="true" className="size-4" />
              {view.isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
            </Button>
          }
        >
          <h1 className="text-xl font-extrabold text-text">ตารางของฉัน</h1>
        </PhoneHeader>
      }
    >
      <ConnectionBanner status={view.hubStatus} stale={view.mayBeStale} refreshing={view.isFetching} onRetry={view.refetch} />
      <MobileWeekStrip view={view} />
      <WeekBody view={view} />
    </PhoneScreen>
  );
}

/** The Supervisor's team timetable: every building assigned to them in their own shift. */
export function ScheduleManagementPage() {
  return <ManagementWeekPage role="supervisor" />;
}

/** The Admin's timetable: every building and any shift. */
export function AdminSchedulesPage() {
  return <ManagementWeekPage role="admin" />;
}

function ManagementWeekPage({ role }: { role: ScheduleRole }) {
  const view = useSchedulePage(role);
  const canEditWeekly = Boolean(view.latest?.rows.some((row) => row.days.some((day) => day.edit?.canChange)));
  const hasDestinations = Boolean(view.latest?.destinations?.length);
  const canEditRelief = Boolean(
    view.latest?.rows.some((row) => row.cleanerKind === 'Relief' && row.days.some((day) => day.edit?.canChange)),
  );
  const showSummary = Boolean(
    view.week && view.week.availability !== 'empty' && !weekIsUnavailable(view.week) && view.week.rows.length > 0,
  );
  const actionProps = { view, canEditWeekly, canEditRelief, hasDestinations };

  return (
    <main className="flex min-w-0 flex-col gap-4 text-base text-text sm:gap-5 sm:text-sm">
      <AdminPageHeader
        title={role === 'admin' ? 'ตารางงาน' : 'จัดตารางงาน'}
        actions={
          <Button variant="secondary" size="desk" className="min-h-10 sm:min-h-9" disabled={view.isFetching} onClick={view.refetch}>
            <RefreshCw aria-hidden="true" className="size-4" />
            {view.isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
          </Button>
        }
      />

      <ConnectionBanner status={view.hubStatus} stale={view.mayBeStale} refreshing={view.isFetching} onRetry={view.refetch} />

      {/* Render only the active viewport's controls so screen readers and DOM never carry duplicate week/filter controls. */}
      {view.phone ? (
        <div className="flex flex-col gap-2.5">
          <MobileWeekStrip view={view} />
          {role === 'admin' && <MobileAdminFilters view={view} />}
          <MobileScheduleActionSheet {...actionProps} />
        </div>
      ) : (
        <FilterToolbar
          label="ตัวกรองและคำสั่งตารางงาน"
          align="end"
          actions={<DesktopScheduleActions {...actionProps} />}
        >
          <WeekControls view={view} />
          {role === 'admin' && <AdminFilters view={view} />}
        </FilterToolbar>
      )}

      {showSummary && view.week && <ScheduleSummaryCards week={view.week} businessDate={view.businessDate} />}

      <WeekBody view={view} />
      {view.week && <ReliefDuties week={view.week} view={view} />}
      {role === 'supervisor' && <RelatedTransfers view={view} />}
      {view.coverNotice && <Toast notice={view.coverNotice} onClose={view.clearCoverNotice} />}
      <Suspense fallback={<PageLoading lines={1}>กำลังโหลดแบบแก้ไข...</PageLoading>}>
        <OpenEditor view={view} />
        {view.coverOpen && view.coverSlot && (
          <CoverDrawer
            current={view.coverSlot}
            start={view.coverSlot}
            onDone={view.onCoverDone}
            onCancel={view.closeCover}
          />
        )}
      </Suspense>
    </main>
  );
}

/** Clean full-width mobile week navigator with direct `สัปดาห์นี้` shortcut. */
function MobileWeekStrip({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  return (
    <section
      className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-1 rounded-xl border border-border bg-surface p-1.5"
      aria-label="เลือกสัปดาห์"
    >
      <Button variant="link" size="phone" className="min-w-12 px-1.5 text-sm" aria-label="สัปดาห์ก่อนหน้า" onClick={() => view.goWeek(-1)}>
        <ChevronLeft aria-hidden="true" className="size-4 shrink-0" />
        ก่อนหน้า
      </Button>
      <div className="flex min-w-0 flex-col items-center text-center">
        <strong className="min-w-0 text-sm font-extrabold wrap-break-word tabular-nums">
          {weekRangeLabel(view.selection.weekStart)}
        </strong>
        {!view.isCurrentWeek && (
          <button
            type="button"
            data-tap=""
            onClick={view.goToday}
            className={`cursor-pointer border-0 bg-transparent text-xs font-semibold text-accent underline ${focusRing}`}
          >
            กลับสัปดาห์นี้
          </button>
        )}
      </div>
      <Button variant="link" size="phone" className="min-w-12 px-1.5 text-sm" aria-label="สัปดาห์ถัดไป" onClick={() => view.goWeek(1)}>
        ถัดไป
        <ChevronRight aria-hidden="true" className="size-4 shrink-0" />
      </Button>
    </section>
  );
}

/** Clean 2-column equal-width mobile Admin filters (Building + Shift) that never awkwardly wrap. */
function MobileAdminFilters({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  const { data: buildings } = useGetBuildingsQuery();
  return (
    <div className="grid grid-cols-2 gap-2" aria-label="ตัวกรองตึกและกะ">
      <Select
        size="phone"
        aria-label="ตึก"
        className="w-full min-w-0 text-sm"
        value={view.selection.buildingId ?? ''}
        onChange={(event) => view.chooseBuilding(event.target.value === '' ? undefined : Number(event.target.value))}
      >
        <option value="">ทุกตึก</option>
        {(buildings ?? []).map((building) => (
          <option key={building.id} value={building.id}>
            {buildingOptionText(building)}
          </option>
        ))}
      </Select>
      <Select
        size="phone"
        aria-label="กะ"
        className="w-full min-w-0 text-sm"
        value={view.selection.shift ?? ''}
        onChange={(event) => view.chooseShift((event.target.value || undefined) as Shift | undefined)}
      >
        <option value="">กะของฉัน</option>
        <option value="Day">{shiftShortLabel.Day}</option>
        <option value="Night">{shiftShortLabel.Night}</option>
      </Select>
    </div>
  );
}

function OpenEditor({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  const { editor, phone, latest: week, closeEditor, refetch, reloadEditorWeek, selection } = view;
  if (!editor) return null;
  const shared = { phone, onClose: closeEditor, onCompare: refetch, reloadEditorWeek };
  if (editor.kind === 'relief-roster') {
    return <ReliefRosterEditor {...shared} week={editor.week} latest={week} selection={selection} />;
  }
  if (editor.kind === 'relief-assignment') {
    return <ReliefAssignmentEditor {...shared} week={editor.week} latest={week} />;
  }
  if (editor.kind === 'relief-loan') {
    return <ReliefLoanEditor {...shared} week={editor.week} latest={week} />;
  }
  if (editor.kind === 'weekly') {
    return <WeeklyOffEditor {...shared} week={editor.week} latest={week} selection={selection} />;
  }
  if (editor.kind === 'baseline') {
    return <RegularBaselineEditor {...shared} week={editor.week} latest={week} person={editor.person} selection={selection} />;
  }
  if (editor.kind === 'missing-time' || editor.kind === 'transfer') {
    const props = { phone, slot: editor.slot, displayName: editor.displayName, onClose: closeEditor };
    return editor.kind === 'missing-time'
      ? <TeamMissingTime {...props} />
      : <TransferInstruction {...props} latest={week} reloadEditorWeek={reloadEditorWeek} />;
  }
  const latest = week?.rows.find((person) => person.userId === editor.person.userId)
    ?.days.find((day) => day.date === editor.day.date);
  return <DatedScheduleEditor {...shared} person={editor.person} day={editor.day} latest={latest} />;
}

function ReliefDuties({ week, view }: { week: ScheduleWeek; view: ReturnType<typeof useSchedulePage> }) {
  if (!week.relief.length) return null;
  return (
    <section className="space-y-3 rounded-xl border border-border bg-surface p-4">
      <div className="flex items-center gap-2">
        <UserCheck aria-hidden="true" className="size-4 text-accent" />
        <h2 className="text-lg font-semibold">งานสแปร์ที่บันทึก</h2>
      </div>
      <ul className="space-y-2">
        {week.relief.map((duty) => (
          <li key={duty.id} className="space-y-1 rounded-lg border border-border bg-surface-2/40 p-3">
            <p className="font-semibold">{duty.displayName} · {formatShiftDate(duty.shiftDate)} · {shiftShortLabel[duty.shift]}</p>
            <p>ตึก {duty.destinationBuildingCode} · {duty.areaCode} · กลุ่มพัก {duty.breakGroup ?? 'ยังไม่กำหนด'}</p>
            <p className="text-text-2">ตึกต้นทาง {duty.sourceBuildingCode}{duty.sourceSupervisorName && ` · ${duty.sourceSupervisorName}`}</p>
            {duty.replacedDisplayName && <p>แทน {duty.replacedDisplayName}</p>}
            {duty.agreementReported && <p className="text-text-2">ตกลงกับตึกต้นทางแล้ว</p>}
            {!duty.isCancelled && (
              <TeamSlotActions view={view} displayName={duty.displayName}
                slot={{ userId: duty.userId, shiftDate: duty.shiftDate, shift: duty.shift }} />
            )}
            {duty.isCancelled && <Badge kind="edited">ยกเลิกแล้ว</Badge>}
          </li>
        ))}
      </ul>
    </section>
  );
}

/** Desktop Admin filters: every building in the shift on the clock, and any shift they choose. */
function AdminFilters({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  return (
    <div className="flex min-w-0 flex-wrap items-end gap-2">
      <div className="w-44 max-w-full">
        <BuildingSelect value={view.selection.buildingId} onChange={view.chooseBuilding} />
      </div>
      <Segmented<'' | Shift>
        label="กะ"
        value={view.selection.shift ?? ''}
        options={[
          { value: '', label: 'กะของฉัน' },
          { value: 'Day', label: shiftShortLabel.Day },
          { value: 'Night', label: shiftShortLabel.Night },
        ]}
        onChange={(value) => view.chooseShift(value === '' ? undefined : value)}
      />
    </div>
  );
}

/** Desktop week control: previous/next week plus `สัปดาห์นี้` quick reset. */
function WeekControls({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  return (
    <section className="flex flex-wrap items-center gap-2" aria-label="เลือกสัปดาห์">
      <div className="flex items-center gap-2">
        <Button variant="secondary" size="desk" aria-label="สัปดาห์ก่อนหน้า" onClick={() => view.goWeek(-1)}>
          <ChevronLeft aria-hidden="true" className="size-4" />
        </Button>
        <strong className="text-sm font-semibold tabular-nums">{weekRangeLabel(view.selection.weekStart)}</strong>
        <Button variant="secondary" size="desk" aria-label="สัปดาห์ถัดไป" onClick={() => view.goWeek(1)}>
          <ChevronRight aria-hidden="true" className="size-4" />
        </Button>
      </div>
      {!view.isCurrentWeek && (
        <Button variant="secondary" size="desk" onClick={view.goToday}>
          สัปดาห์นี้
        </Button>
      )}
    </section>
  );
}

function WeekBody({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  const { week, error } = view;
  return (
    <>
      {view.filterDropped && (
        <Note tone="info">ตึกที่เลือกไว้ไม่อยู่ในสิทธิ์ที่ดูได้แล้ว จึงแสดงทุกตึกที่ดูได้แทน</Note>
      )}
      {view.isLoading && <PageLoading lines={3}>กำลังโหลดตาราง...</PageLoading>}
      {error && (
        <Note tone="error" onRetry={view.refetch}>
          {getErrorMessage(error)}
        </Note>
      )}
      {week && week.availability === 'empty' && (
        <PageEmpty
          icon={<CalendarDays aria-hidden="true" className="size-8 text-border-control" />}
          title="ไม่มีตารางให้แสดงในสัปดาห์นี้"
        >
          {view.role === 'admin' ? 'ลองเลือกตึกหรือกะอื่น' : 'ยังไม่มีตารางของตึกและกะที่คุณรับผิดชอบในสัปดาห์นี้'}
        </PageEmpty>
      )}
      {week && week.availability !== 'empty' && weekIsUnavailable(week) && (
        <Note tone="warn" title="ไม่มีข้อมูลของสัปดาห์นี้">
          ไม่พบข้อมูลย้อนหลังของสัปดาห์นี้
        </Note>
      )}
      {week && week.availability !== 'empty' && !weekIsUnavailable(week) && (
        view.role === 'cleaner' ? <OwnWeek week={week} businessDate={view.businessDate} /> : week.rows.length > 0 && <ManagementWeek view={view} />
      )}
    </>
  );
}

function OwnWeek({ week, businessDate }: { week: ScheduleWeek; businessDate: string }) {
  const person = week.rows[0];
  if (!person) {
    return <Note tone="info">ยังไม่มีตารางของคุณในสัปดาห์นี้</Note>;
  }
  return (
    <div className="flex min-w-0 flex-col gap-2">
      {person.days.map((day) => {
        const isToday = day.date === businessDate;
        return (
          <article
            key={day.date}
            className={`flex min-w-0 flex-col gap-1.5 rounded-xl border px-3.5 py-3 shadow-xs ${
              isToday ? 'border-accent bg-accent-subtle' : 'border-border bg-surface'
            }`}
          >
            <header className="flex flex-wrap items-center justify-between gap-2">
              <strong className={`min-w-0 text-sm font-extrabold tabular-nums ${isToday ? 'text-accent' : 'text-text'}`}>
                {`${formatWeekday(day.date)} ${formatShiftDate(day.date)}${isToday ? ' (วันนี้)' : ''}`}
              </strong>
              <span
                className={`inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-semibold ${cellPillClass[day.availability]}`}
              >
                {availabilityLabel[day.availability]}
              </span>
            </header>
            <DayFacts day={day} />
            {day.problems.map((problem) => (
              <Note key={`${problem.code}:${problem.date}`} tone="warn" alert={false}>
                {problem.message}
              </Note>
            ))}
          </article>
        );
      })}
    </div>
  );
}

function ManagementWeek({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  const week = view.week!;
  const dates = datesOf(week);
  const nameColumnWidth = 160;
  const dateColumnMinWidth = 86;
  return (
    <div className="flex flex-col gap-3">
      <section className="overflow-hidden rounded-xl border border-border bg-surface">
        <HorizontalScrollArea label="ตารางงานรายสัปดาห์" leadingOverlayWidth={nameColumnWidth}>
          <table
            aria-label="ตารางงานรายสัปดาห์"
            className="w-full table-fixed border-separate border-spacing-0"
            style={{ minWidth: nameColumnWidth + dates.length * dateColumnMinWidth }}
          >
            <colgroup><col className="w-28 sm:w-40" /></colgroup>
            <thead className="bg-surface-2 text-xs font-bold text-text-2">
              <tr>
                <th scope="col" className="sticky left-0 z-20 border-r border-border bg-surface-2 px-3 py-2.5 text-left">
                  {view.role === 'admin' ? 'พนักงาน' : 'แม่บ้าน'}
                </th>
                {dates.map((date) => {
                  const isToday = date === view.businessDate;
                  return (
                    <th
                      key={date}
                      className={`px-2 py-2.5 text-center ${isToday ? 'bg-accent-subtle text-accent' : 'bg-surface-2 text-text-2'}`}
                    >
                      <span className="block">{formatWeekday(date)}</span>
                      <span className="block tabular-nums">{formatShiftDate(date).slice(0, 5)}</span>
                    </th>
                  );
                })}
              </tr>
            </thead>
            <tbody>
              {week.rows.map((person) => (
                <tr key={person.userId}>
                  <th scope="row" className="sticky left-0 z-10 border-t border-r border-border bg-surface px-2 py-2.5 text-left align-top font-semibold sm:px-3">
                    <div className="flex items-start gap-1.5 sm:gap-2.5">
                      <AvatarBadge
                        name={person.displayName}
                        tone={person.cleanerKind === 'Relief' ? 'teal' : 'blue'}
                        size="sm"
                      />
                      <div className="min-w-0 flex-1 wrap-anywhere">
                        <span className="block whitespace-normal font-semibold text-text">{person.displayName}</span>
                        <span className="block text-xs font-normal text-text-2">{cleanerKindLabel[person.cleanerKind]}</span>
                        {person.cleanerKind === 'Relief' && person.homeBuildingCode && (
                          <span className="block text-xs font-normal text-text-2">ตึกประจำ: {person.homeBuildingCode}</span>
                        )}
                        {person.cleanerKind === 'Regular' && !person.hasWeeklyChoice
                          && person.days.some((day) => day.edit) && (
                            <div className="mt-1 [&>span]:whitespace-normal">
                              <Badge kind="edited">ยังไม่กำหนดวันหยุด</Badge>
                            </div>
                        )}
                      </div>
                    </div>
                  </th>
                  {dates.map((date) => {
                    const day = person.days.find((one) => one.date === date);
                    const isToday = date === view.businessDate;
                    const isSelected = view.selectedCell?.userId === person.userId && view.selectedCell?.date === date;
                    return (
                      <td
                        key={date}
                        className={`border-t border-border p-1.5 align-top ${isToday ? 'bg-accent-subtle/30' : ''}`}
                      >
                        {day ? (
                          <button
                            type="button"
                            aria-pressed={isSelected}
                            onClick={() => view.selectCell(person.userId, day.date)}
                            className={`flex min-h-14 w-full cursor-pointer flex-col items-start gap-1 rounded-lg px-1.5 py-1.5 text-left wrap-anywhere transition-colors ${focusRing} ${
                              isSelected
                                ? 'bg-accent-subtle ring-2 ring-accent ring-inset'
                                : 'hover:bg-surface-2'
                            }`}
                          >
                            <span className="sr-only">
                              {`เลือกช่อง ${person.displayName} ${formatWeekday(day.date)} ${formatShiftDate(day.date)} `}
                            </span>
                            <Cell day={day} />
                          </button>
                        ) : (
                          <span className="block px-2 py-1.5 text-text-3">—</span>
                        )}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </HorizontalScrollArea>
      </section>
      <ScheduleContextBar view={view} />
    </div>
  );
}

function RelatedTransfers({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  const slots = view.standaloneTransfers;
  if (!slots.length) return null;
  return (
    <section aria-labelledby="related-transfers" className="space-y-3 rounded-xl border border-border bg-surface p-4">
      <div className="flex items-center gap-2">
        <ArrowRightLeft aria-hidden="true" className="size-4 text-accent" />
        <h2 id="related-transfers" className="text-lg font-semibold">การย้ายพื้นที่ที่เกี่ยวข้อง</h2>
      </div>
      <ul className="space-y-2">
        {slots.map((slot) => (
          <li key={slot.id} className="space-y-1 rounded-lg border border-border bg-surface-2/40 p-3">
            <p className="font-semibold">{slot.displayName} · {formatShiftDate(slot.shiftDate)} · {shiftShortLabel[slot.shift]}</p>
            <p>{slot.fromAreaCode} → {slot.toAreaCode}</p>
            <p className="text-text-2">
              {slot.confirmedAt
                ? `${transferStateLabel.confirmed} · ${formatDayTime(slot.confirmedAt)}`
                : transferStateLabel.instructed}
            </p>
            <TeamSlotActions view={view} displayName={slot.displayName}
              slot={{ userId: slot.userId, shiftDate: slot.shiftDate, shift: slot.shift }}
              canOpenMissingTime={!!slot.confirmedAt} />
          </li>
        ))}
      </ul>
    </section>
  );
}

function TeamSlotActions({ view, displayName, slot, canOpenMissingTime = true }: {
  view: ReturnType<typeof useSchedulePage>;
  displayName: string;
  slot: TeamSlotArgs;
  canOpenMissingTime?: boolean;
}) {
  if (view.role !== 'supervisor' || !view.latest) return null;
  const size = view.phone ? 'phone' : 'desk';
  const context = `${displayName} ${formatShiftDate(slot.shiftDate)}`;
  return (
    <div className="flex flex-col items-start">
      {canOpenMissingTime && (
        <Button variant="link" size={size} aria-label={`เพิ่มเวลาที่ขาด ${context}`}
          onClick={() => view.openTeamSlot('missing-time', slot, displayName)}>เพิ่มเวลาที่ขาด</Button>
      )}
      <Button variant="link" size={size} aria-label={`ย้ายพื้นที่ระหว่างกะ ${context}`}
        onClick={() => view.openTeamSlot('transfer', slot, displayName)}>ย้ายพื้นที่</Button>
    </div>
  );
}

const cellPillClass: Record<ScheduleAvailability, string> = {
  available: 'border-passed-border bg-passed-bg text-passed',
  leave: 'border-overdue-border bg-overdue-bg text-overdue',
  'day-off': 'border-idle-border bg-idle-bg text-idle',
  unset: 'border-rework-border bg-rework-bg text-rework',
  unavailable: 'border-idle-border bg-surface-2 text-text-2',
};

function Cell({ day }: { day: ScheduleDay }) {
  return (
    <>
      <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-semibold ${cellPillClass[day.availability]}`}>
        {cellLabel[day.availability]}
      </span>
      {day.area.value != null && (
        <span className="text-xs font-medium text-text-3">{`พื้นที่ ${day.area.value}`}</span>
      )}
      {day.breakGroup.value != null && (
        <span className="text-xs text-text-3">{`กลุ่ม ${day.breakGroup.value}`}</span>
      )}
      {day.problems.length > 0 && (
        <span className="text-xs font-semibold text-rework">มีข้อขัดแย้ง</span>
      )}
    </>
  );
}

function DayFacts({ day }: { day: ScheduleDay }) {
  return (
    <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
      <dt className="text-text-2">พื้นที่</dt>
      <dd className="m-0 font-semibold">
        {day.area.value != null ? day.area.value : '—'}
        <span className="ml-2 text-text-2">{sourceLabel[day.area.sourceKind]}</span>
      </dd>
      <dt className="text-text-2">กลุ่มพัก</dt>
      <dd className="m-0 font-semibold">
        {day.breakGroup.value != null ? `กลุ่ม ${day.breakGroup.value}` : '—'}
        <span className="ml-2 text-text-2">{sourceLabel[day.breakGroup.sourceKind]}</span>
      </dd>
    </dl>
  );
}

function datesOf(week: ScheduleWeek): string[] {
  const seen = new Set<string>();
  for (const person of week.rows) {
    for (const day of person.days) {
      seen.add(day.date);
    }
  }
  return [...seen].sort();
}
