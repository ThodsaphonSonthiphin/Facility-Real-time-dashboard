import { CalendarDays, ChevronLeft, ChevronRight, RefreshCw } from 'lucide-react';
import { Link } from 'react-router';
import { lazy, Suspense } from 'react';
import type { ScheduleDay, SchedulePerson, ScheduleWeek } from '../../../shared/api/scheduleTypes';
import type { Shift } from '../../../shared/api/types';
import { Badge } from '../../../shared/components/Badge';
import { BuildingSelect } from '../../../shared/components/BuildingSelect';
import { Button } from '../../../shared/components/Button';
import { LogoutButton } from '../../../shared/components/LogoutButton';
import { MoreInfo } from '../../../shared/components/MoreInfo';
import { Note } from '../../../shared/components/Note';
import { PageEmpty, PageLoading } from '../../../shared/components/PageState';
import { PhoneHeader, PhoneScreen } from '../../../shared/components/PhoneScreen';
import { Segmented } from '../../../shared/components/Segmented';
import { focusRing } from '../../../shared/components/styles';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { shiftShortLabel, transferStateLabel } from '../../../shared/utils/labels';
import { formatDayTime, formatShiftDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { availabilityLabel, cellLabel, sourceLabel, weekIsUnavailable, weekRangeLabel, type ScheduleRole } from '../scheduleRules';
import type { TeamSlotArgs } from '../schedulesApi';
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
const ScheduleHistory = lazy(() => import('../ScheduleHistory/ScheduleHistory')
  .then((module) => ({ default: module.ScheduleHistory })));

/**
 * ADR facility-0176–0179; spec §4: one week per role. A Cleaner opens their own week from My Work, a Supervisor
 * the roster of the buildings they hold from My Building, an Admin every building in a shift from the menu. Reading needs
 * no QR and no location (0179). Management editors load only when opened.
 *
 * The three week states are drawn differently on purpose (spec §3, §10): a week outside retained coverage says so rather
 * than showing an empty roster, a week with nothing to read says the filter has nothing, and a real plan shows its days —
 * including the ones nobody has chosen yet, which is not a working day.
 *
 * A Supervisor's page is a table, so it takes the Admin desk's width (the sidebar is not theirs and is not drawn).
 */

/** The Cleaner's own week (0176): before the work list on งานของฉัน, with a return control. */
export function OwnSchedulePage() {
  const view = useSchedulePage('cleaner');
  return (
    <PhoneScreen
      header={
        <PhoneHeader back={{ to: '/my-work', label: 'งานของฉัน' }}>
          <h1 className="text-lg font-bold">ตารางของฉัน</h1>
        </PhoneHeader>
      }
      bottom={
        <Button variant="primary" size="main" disabled={view.isFetching} onClick={view.refetch}>
          <RefreshCw aria-hidden="true" className="size-6" />
          {view.isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </Button>
      }
    >
      <WeekControls view={view} />
      <WeekBody view={view} />
      <footer className="flex flex-col gap-2.5 border-t border-border pt-3">
        <LiveLine view={view} />
        <LogoutButton />
      </footer>
    </PhoneScreen>
  );
}

/** The Supervisor's team timetable (0177): every building assigned to them in their own shift, with a return control. */
export function ScheduleManagementPage() {
  return <ManagementWeekPage role="supervisor" back={{ to: '/my-building', label: 'ตึกของฉัน' }} />;
}

/** The Admin's timetable (0178): every building and any shift; the menu above is the way back. */
export function AdminSchedulesPage() {
  return <ManagementWeekPage role="admin" />;
}

function ManagementWeekPage({ role, back }: { role: ScheduleRole; back?: { to: string; label: string } }) {
  const view = useSchedulePage(role);
  return (
    <main className="flex min-w-0 flex-col gap-6 text-base text-text sm:text-sm">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          {back && (
            <Link to={back.to} className={`inline-flex min-h-9 items-center gap-1 self-start font-semibold text-accent hover:text-accent-hover ${focusRing}`}>
              <ChevronLeft aria-hidden="true" className="size-4" />
              {back.label}
            </Link>
          )}
          <h1 className="text-2xl font-bold">{role === 'admin' ? 'ตารางงาน' : 'จัดตารางงาน'}</h1>
          <span className="text-text-2">
            {role === 'admin'
              ? 'ดูตารางของทุกตึกและทุกกะ ใครแก้อะไรดูได้ที่ประวัติการแก้ไข'
              : 'ตารางรวมของตึกและกะที่คุณรับผิดชอบ'}
          </span>
        </div>
        <Button variant="secondary" size="desk" disabled={view.isFetching} onClick={view.refetch}>
          <RefreshCw aria-hidden="true" className="size-4" />
          {view.isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </Button>
      </header>
      <WeekControls view={view} />
      {role === 'admin' && <AdminFilters view={view} />}
      {view.latest?.rows.some((row) => row.days.some((day) => day.edit?.canChange)) && (
        <div>
          <Button variant="primary" size={view.phone ? 'main' : 'desk'} onClick={view.openWeekly}>จัดวันหยุดรายสัปดาห์</Button>
        </div>
      )}
      {!!view.latest?.destinations?.length && (
        <div className="flex flex-wrap gap-2" aria-label="จัดงานสแปร์">
          {view.latest.rows.some((row) => row.cleanerKind === 'Relief' && row.days.some((day) => day.edit?.canChange)) && (
            <>
              <Button size={view.phone ? 'phone' : 'desk'} onClick={() => view.openRelief('relief-roster')}>จัดตารางประจำสแปร์</Button>
              <Button size={view.phone ? 'phone' : 'desk'} onClick={() => view.openRelief('relief-assignment')}>มอบหมายงานสแปร์</Button>
            </>
          )}
          <Button size={view.phone ? 'phone' : 'desk'} onClick={() => view.openRelief('relief-loan')}>บันทึกการยืมสแปร์</Button>
        </div>
      )}
      <WeekBody view={view} />
      {view.week && <ReliefDuties week={view.week} view={view} />}
      {role === 'supervisor' && <RelatedTransfers view={view} />}
      <Suspense fallback={<PageLoading lines={1}>กำลังโหลดแบบแก้ไข...</PageLoading>}>
        <OpenEditor view={view} />
      </Suspense>
      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border pt-3">
        <LiveLine view={view} />
      </div>
      <Notes role={role} />
    </main>
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
    <section className="space-y-3 rounded-lg border border-border bg-surface p-4">
      <h2 className="text-lg font-semibold">งานสแปร์ที่บันทึก</h2>
      <ul className="space-y-2">
        {week.relief.map((duty) => (
          <li key={duty.id} className="space-y-1 rounded-md border border-border p-3">
            <p className="font-semibold">{duty.displayName} · {formatShiftDate(duty.shiftDate)} · {shiftShortLabel[duty.shift]}</p>
            <p>ตึก {duty.destinationBuildingCode} · {duty.areaCode} · กลุ่มพัก {duty.breakGroup ?? 'ยังไม่กำหนด'}</p>
            <p className="text-text-2">ตึกต้นทาง {duty.sourceBuildingCode}{duty.sourceSupervisorName && ` · ${duty.sourceSupervisorName}`}</p>
            {duty.replacedDisplayName && <p>แทน {duty.replacedDisplayName}</p>}
            {duty.agreementReported && <p>ผู้บันทึกยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว</p>}
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

/** The Admin's filters (0184): every building in the shift on the clock, and any shift they choose. */
function AdminFilters({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  return (
    <section className="flex flex-wrap items-end gap-3 rounded-lg border border-border bg-surface px-4 py-3">
      <BuildingSelect value={view.selection.buildingId} onChange={view.chooseBuilding} />
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
    </section>
  );
}

/**
 * The week control: the range, and the two neighbour weeks. Choosing a week needs no QR and no location (0179). One range
 * per screen: this is the only place it is written, whichever layout the page is drawn in.
 */
function WeekControls({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  return (
    <section className="flex flex-wrap items-center justify-between gap-3" aria-label="เลือกสัปดาห์">
      <div className="flex items-center gap-2">
        <Button variant="secondary" size="desk" aria-label="สัปดาห์ก่อนหน้า" onClick={() => view.goWeek(-1)}>
          <ChevronLeft aria-hidden="true" className="size-4" />
        </Button>
        <strong className="min-w-40 text-base">{weekRangeLabel(view.selection.weekStart)}</strong>
        <Button variant="secondary" size="desk" aria-label="สัปดาห์ถัดไป" onClick={() => view.goWeek(1)}>
          <ChevronRight aria-hidden="true" className="size-4" />
        </Button>
      </div>
    </section>
  );
}

/**
 * What the read says: loading, the shared error, a week outside coverage, a week with nothing under the filter, or the
 * days. The first two states are never drawn as an empty roster (spec §3, §10).
 */
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
          ช่วงนี้ไม่ได้เก็บข้อมูลไว้นานขนาดย้อนดูได้ จึงไม่ใช่สัปดาห์ที่ไม่มีคนทำงาน
        </Note>
      )}
      {week && week.availability !== 'empty' && !weekIsUnavailable(week) && (
        view.role === 'cleaner' ? <OwnWeek week={week} /> : week.rows.length > 0 && <ManagementWeek view={view} />
      )}
    </>
  );
}

/** One person's week as cards, one per day, each saying its state in words (spec §5). */
function OwnWeek({ week }: { week: ScheduleWeek }) {
  const person = week.rows[0];
  if (!person) {
    return <Note tone="info">ยังไม่มีตารางของคุณในสัปดาห์นี้</Note>;
  }
  return (
    <>
      {person.days.map((day) => (
        <article key={day.date} className="flex flex-col gap-1.5 rounded-lg border border-border bg-surface px-4 py-3">
          <header className="flex items-baseline justify-between gap-2">
            <strong className="text-base">{`${formatWeekday(day.date)} ${formatShiftDate(day.date)}`}</strong>
            <span className="text-sm font-semibold">{availabilityLabel[day.availability]}</span>
          </header>
          <DayFacts day={day} />
          {day.problems.map((problem) => (
            <Note key={`${problem.code}:${problem.date}`} tone="warn" alert={false}>
              {problem.message}
            </Note>
          ))}
        </article>
      ))}
    </>
  );
}

/** The management table: a row per person, a column per date the reader may see (keyed by date, never by index). */
function ManagementWeek({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  const week = view.week!;
  const dates = datesOf(week);
  return (
    <section className="overflow-hidden rounded-lg border border-border bg-surface">
      <div className="max-w-full overflow-x-auto">
        <table aria-label="ตารางงานรายสัปดาห์" className="w-full min-w-180 border-collapse">
          <thead className="bg-surface-2 text-xs font-semibold text-text-2">
            <tr>
              <th className="px-3 py-2.5 text-left">{view.role === 'admin' ? 'พนักงาน' : 'แม่บ้าน'}</th>
              {dates.map((date) => (
                <th key={date} className="px-3 py-2.5 text-left">
                  <span className="block">{formatWeekday(date)}</span>
                  <span className="block">{formatShiftDate(date).slice(0, 5)}</span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {week.rows.map((person) => (
              <tr key={person.userId} className="border-t border-border">
                <th scope="row" className="px-3 py-2.5 text-left font-semibold">
                  <span className="block">{person.displayName}</span>
                  <span className="block text-text-2">{kindLabel[person.cleanerKind]}</span>
                  {person.cleanerKind === 'Relief' && person.homeBuildingCode && (
                    <span className="block text-text-2">ตึกประจำ: {person.homeBuildingCode}</span>
                  )}
                  {person.cleanerKind === 'Regular' && !person.hasWeeklyChoice
                    && person.days.some((day) => day.edit) && <Badge kind="edited">ยังไม่กำหนดวันหยุด</Badge>}
                  {view.latest && person.cleanerKind === 'Regular' && person.days.some((day) => day.edit?.canChange) && (
                    <Button variant="link" size={view.phone ? 'phone' : 'desk'} onClick={() => view.openBaseline(person)}>
                      แก้ตารางประจำ
                    </Button>
                  )}
                </th>
                {dates.map((date) => {
                  const day = person.days.find((one) => one.date === date);
                  return (
                    <td key={date} className="px-3 py-2.5 align-top">
                      <Cell day={day} />
                      {view.latest && day?.edit?.canChange && (
                        <Button variant="link" size={view.phone ? 'phone' : 'desk'} onClick={() => view.openDated(person, day)}
                          aria-label={`${view.dateAction(day)} ${person.displayName} ${formatShiftDate(day.date)}`}>
                          {view.dateAction(day)}
                        </Button>
                      )}
                      {day?.shift && day.area.value != null && (
                        <TeamSlotActions view={view} displayName={person.displayName}
                          slot={{ userId: person.userId, shiftDate: day.date, shift: day.shift }} />
                      )}
                      {day?.edit?.corrections.length ? (
                        <details>
                          <summary className={`min-h-12 cursor-pointer text-accent sm:min-h-9 ${focusRing}`}>ประวัติการแก้ไขแผน</summary>
                          <Suspense fallback={<p role="status">กำลังโหลดประวัติ...</p>}>
                            <ScheduleHistory corrections={day.edit.corrections} />
                          </Suspense>
                        </details>
                      ) : null}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function RelatedTransfers({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  const slots = view.standaloneTransfers;
  if (!slots.length) return null;
  return (
    <section aria-labelledby="related-transfers" className="space-y-3 rounded-lg border border-border bg-surface p-4">
      <h2 id="related-transfers" className="text-lg font-semibold">การย้ายพื้นที่ที่เกี่ยวข้อง</h2>
      <ul className="space-y-2">
        {slots.map((slot) => (
          <li key={slot.id} className="space-y-1 rounded-md border border-border p-3">
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

/** One table cell: its short word, and the Area it works where there is one. A date it may not see says nothing false. */
function Cell({ day }: { day: ScheduleDay | undefined }) {
  if (!day) {
    return <span className="text-text-3">—</span>;
  }
  return (
    <span className="flex flex-col gap-0.5">
      <span className="font-semibold">{cellLabel[day.availability]}</span>
      {day.area.value != null && <span className="text-xs text-text-2">{`พื้นที่ ${day.area.value}`}</span>}
    </span>
  );
}

/** One day's Area and break group, each with where it came from, so a one-day choice never reads as a recurring plan. */
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

/**
 * Spec §12: the page asks again on its own; a read that may be older than the plan keeps its notice until a read that
 * started after the change succeeded, and a dropped connection says the data may not be current.
 */
function LiveLine({ view }: { view: ReturnType<typeof useSchedulePage> }) {
  if (view.hubStatus === 'reconnecting' || view.mayBeStale) {
    return (
      <span role="status" className="text-sm font-semibold text-overdue">
        ข้อมูลอาจยังไม่ล่าสุด กำลังโหลดใหม่...
      </span>
    );
  }
  return <span className="text-sm text-text-2">หน้านี้อัปเดตเองเมื่อมีคนแก้ตาราง</span>;
}

/**
 * What the page is and is not (MASTER.md: the notes wait behind ℹ), worded for the role reading it.
 */
function Notes({ role }: { role: ScheduleRole }) {
  return (
    <MoreInfo summary="เกี่ยวกับหน้านี้">
      <span>
        {role === 'admin'
          ? 'แสดงแผนตามวันของแต่ละคน ไม่ใช่เวลาที่สแกนจริง'
          : 'แสดงแผนตามวันของแม่บ้านในตึกและกะที่คุณรับผิดชอบ ไม่ใช่เวลาที่สแกนจริง'}
      </span>
      <span>วันหยุดและลาทั้งกะไม่ปิดพื้นที่และไม่ลดรอบงาน · “ยังไม่กำหนด” ไม่ใช่คำยืนยันว่ามาทำงาน</span>
    </MoreInfo>
  );
}

const kindLabel: Record<SchedulePerson['cleanerKind'], string> = {
  Regular: 'แม่บ้านประจำ',
  Relief: 'แม่บ้านสแปร',
  unknown: 'ยังไม่ระบุ',
};

/** The dates of the read week. A Supervisor's row may hold fewer days, so the columns come from the week itself. */
function datesOf(week: ScheduleWeek): string[] {
  const seen = new Set<string>();
  for (const person of week.rows) {
    for (const day of person.days) {
      seen.add(day.date);
    }
  }
  return [...seen].sort();
}
