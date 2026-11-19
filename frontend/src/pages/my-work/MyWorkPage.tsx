import { ArrowRightLeft, CalendarDays, Check, ChevronRight, CircleCheck, ClipboardList, Clock, Hourglass, Moon, RefreshCw, TriangleAlert } from 'lucide-react';
import type { ReactNode } from 'react';
import { NavLink } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { MY_WORK_HUB, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import type { AttendanceState, MyWork, OwnShift, Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { LogoutButton } from '../../shared/components/LogoutButton';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { PhoneHeader, PhoneScreen } from '../../shared/components/PhoneScreen';
import { focusRing } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { formatClock, formatTime } from '../../shared/utils/thaiTime';
import { useAppSelector } from '../../store/hooks';
import { selectHubStatus, type HubStatus } from '../../store/slices/hubSlice';
import { NextJob } from './NextJob';
import { WorkGroup } from './WorkGroup';
import { byUrgency, laterStatuses, sentStatuses, toDo } from './workRules';

/**
 * facility-0052, wireframes M9 and M14: every point of the Cleaner's own Area, then of the Areas they cover this
 * shift. It changes by itself when the hub says a point changed (the Supervisor's inspection, owner 2026-10-26, which
 * amends facility-0052), and also asks each time it opens and on "อัปเดต". No card links to the scan page; the only way
 * there is the sign's QR (facility-0059). The name is not shown: the bar above already shows it.
 * The layout is variant B of the mockups (#73, `design-system/janitorial/pages/phone.md`): the next job first.
 */
/** The footnote by the hub's state, as My Building's (plan 9 decision 9). */
const liveLine: Record<HubStatus, string> = {
  connecting: 'กำลังเตรียมอัปเดตข้อมูลอัตโนมัติ...',
  live: 'หน้านี้อัปเดตเองเมื่อหัวหน้าตรวจงาน',
  reconnecting: 'ข้อมูลอาจยังไม่ล่าสุด กด "อัปเดต" เพื่อลองโหลดใหม่',
};

export function MyWorkPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyWorkQuery(undefined, freshOnOpen);
  const shift = data?.attendance.shift;
  const hubStatus = useAppSelector(selectHubStatus(MY_WORK_HUB));
  const user = useAppSelector((state) => state.auth.currentUser);
  const own = data?.areas.find((one) => !one.isCover);
  const where = own
    ? `${own.areaCode} ${own.areaName}${own.points[0] ? ` · ตึก ${own.points[0].buildingCode}` : ''}`
    : '';

  // The header takes its place before the answer comes (name, and lines kept as tall as their words), so nothing under
  // it moves when the answer lands (layout shift, measured 2026-10-28)
  const header = (
    <PhoneHeader>
      <h1 className="sr-only">งานของฉัน</h1>
      <span className="text-lg font-bold">{user?.fullName}</span>
      <span className="min-h-6 text-sm text-text-2">{where}</span>
      <span className="min-h-6 text-sm text-text-2">{data && shift ? attendanceLine(shift, data.attendance) : ''}</span>
    </PhoneHeader>
  );

  return (
    <PhoneScreen
      header={header}
      bottom={
        // The mockup's error state keeps its one action in the bar: try again
        error ? (
          <Button variant="primary" size="main" disabled={isFetching} onClick={() => void refetch()}>
            <RefreshCw aria-hidden="true" className="size-6" />
            {isFetching ? 'กำลังโหลด...' : 'ลองอีกครั้ง'}
          </Button>
        ) : undefined
      }
    >
      {/* The schedule link needs no work data. Keep it above the loading state so it stays in place. */}
      <ScheduleLink />
      {isLoading && <PageLoading lines={3}>กำลังโหลดงาน...</PageLoading>}
      {error && <Note tone="error">{getErrorMessage(error)}</Note>}
      {data && (shift ? <WorkList work={data} /> : <OffShift ownShift={data.ownShift} />)}
      {/* Not before the first answer: the list then pushed it down the screen (layout shift, measured 2026-10-28) */}
      {!isLoading && (
        <footer className="flex flex-col gap-2.5 border-t border-border pt-3">
          <span className="text-sm text-text-2">{liveLine[hubStatus]}</span>
          <Button className="w-full" disabled={isFetching} onClick={() => void refetch()}>
            <RefreshCw aria-hidden="true" className="size-5" />
            {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
          </Button>
          <LogoutButton />
        </footer>
      )}
    </PhoneScreen>
  );
}

/**
 * facility-0176: "ตารางของฉัน" before the work list. It is a plain route, so it needs no QR and no location (0179); the
 * page returns here with its own back control.
 */
function ScheduleLink() {
  return (
    <NavLink
      to="/my-schedule"
      data-tap=""
      className={`flex min-h-12 items-center gap-3 rounded-md border border-border-control bg-surface px-4 text-base font-semibold no-underline text-text hover:border-accent ${focusRing}`}
    >
      <CalendarDays aria-hidden="true" className="size-5 shrink-0 text-accent" />
      <span className="flex-1">ตารางของฉัน</span>
      <ChevronRight aria-hidden="true" className="size-5 shrink-0 text-text-2" />
    </NavLink>
  );
}

function attendanceLine(shift: Shift, attendance: AttendanceState): string {
  const shiftIn = attendance.events.find((e) => e.eventType === 'ShiftIn');
  const shiftOut = attendance.events.find((e) => e.eventType === 'ShiftOut');
  return [
    shiftLabel[shift],
    shiftIn ? `เข้างาน ${formatTime(shiftIn.occurredAt)}` : 'ยังไม่ได้ลงเวลาเข้างาน',
    shiftOut ? `เลิกงาน ${formatTime(shiftOut.occurredAt)}` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}

/**
 * facility-0077: outside the own shift's attendance window the server answers with the shift on the clock, which is
 * someone else's work. So the page shows no counts and no cards, only the own shift and when its check-in opens.
 */
function OffShift({ ownShift }: { ownShift?: OwnShift }) {
  return (
    <StateCard icon={<Moon aria-hidden="true" className="size-8" />} tone="idle" title="ตอนนี้ไม่อยู่ในกะของคุณ">
      {ownShift && <span>{shiftLabel[ownShift.shift]}</span>}
      {ownShift && <span>{`ลงเวลาเข้างานได้ตั้งแต่ ${formatClock(ownShift.opensAt)}`}</span>}
    </StateCard>
  );
}

const tones = {
  idle: { frame: 'border-border', circle: 'bg-idle-bg text-idle' },
  passed: { frame: 'border-passed-border', circle: 'bg-passed-bg text-passed' },
} as const;

/** The design file's centred card for a page state: a 64 px circle with its icon, a 20/700 line, then the words. */
function StateCard({
  icon,
  tone,
  title,
  children,
}: {
  icon: ReactNode;
  tone: 'idle' | 'passed';
  title: string;
  children?: ReactNode;
}) {
  const look = tones[tone];
  return (
    <section
      className={`flex flex-col items-center gap-3 rounded-lg border bg-surface px-4 py-6 text-center ${look.frame}`}
    >
      <span className={`flex size-16 items-center justify-center rounded-full ${look.circle}`}>{icon}</span>
      <strong className="text-xl">{title}</strong>
      {children && <div className="flex flex-col gap-1 text-text-2">{children}</div>}
    </section>
  );
}

function WorkList({ work }: { work: MyWork }) {
  if (work.areas.length === 0) {
    return <Note tone="info">ยังไม่มีจุดให้ทำความสะอาดในรายการนี้ แจ้งแอดมินให้ตรวจพื้นที่และจุดที่คุณรับผิดชอบ</Note>;
  }
  const covered = work.areas.filter((area) => area.isCover);
  const ordered = byUrgency(work.areas);
  const next = ordered.find((item) => toDo.includes(item.point.status));
  const rest = ordered.filter((item) => item !== next);
  const passedCount = ordered.filter((item) => item.point.status === 'Passed').length;
  const pendingCount = ordered.filter((item) => item.point.status === 'PendingInspection').length;
  const sentCount = passedCount + pendingCount;
  const todoCount = ordered.filter((item) => toDo.includes(item.point.status)).length;
  const issueCount = ordered.filter((item) => item.point.status === 'Rework' || Boolean(item.point.issue)).length;

  return (
    <>
      <ShiftBanner attendance={work.attendance} />
      <MiniKpis total={ordered.length} sent={sentCount} remaining={todoCount} issues={issueCount} />
      {covered.length > 0 && (
        // The design file's cover line: purple, the colour of ทำแทน
        <p className="flex items-start gap-2 rounded-md border border-break-border bg-break-bg px-3 py-2 text-sm text-break">
          <ArrowRightLeft aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          {`กะนี้ทำแทนที่ ${covered.map((area) => area.areaName).join(', ')}`}
        </p>
      )}
      {next ? (
        <NextJob item={next} />
      ) : (
        <StateCard
          icon={<Check aria-hidden="true" className="size-9" />}
          tone="passed"
          title="ส่งครบทุกจุดที่ต้องทำตอนนี้แล้ว"
        />
      )}
      <Progress
        passed={passedCount}
        pending={pendingCount}
        total={ordered.length}
      />
      <WorkGroup title="ต้องทำต่อ" items={rest.filter((item) => toDo.includes(item.point.status))} />
      <WorkGroup title="ส่งแล้ว" items={rest.filter((item) => sentStatuses.includes(item.point.status))} />
      <WorkGroup title="ยังไม่ต้องทำ" items={rest.filter((item) => laterStatuses.includes(item.point.status))} />
    </>
  );
}

/**
 * "x จาก y จุด ส่งแล้ว", where ส่งแล้ว is รอตรวจ and ตรวจผ่าน, and a dark grey bar of that share (DESIGN_SPEC §7: grey,
 * never blue or green, since blue is for what can be pressed and green for a status). The words carry it; the bar is a
 * `progressbar` with the same numbers for a screen reader.
 */
function Progress({ passed, pending, total }: { passed: number; pending: number; total: number }) {
  const sent = passed + pending;
  return (
    <div className="flex flex-col gap-2">
      <span className="text-base">{`${sent} จาก ${total} จุด ส่งแล้ว`}</span>
      <div
        role="progressbar"
        aria-label="ความคืบหน้า"
        aria-valuemin={0}
        aria-valuemax={total}
        aria-valuenow={sent}
        className="h-2 overflow-hidden rounded-sm bg-border"
      >
        {/* The share is data, not a token, so the width is an inline style */}
        <div className="h-full rounded-sm bg-text" style={{ width: `${(100 * sent) / total}%` }} />
      </div>
    </div>
  );
}

function ShiftBanner({ attendance }: { attendance: AttendanceState }) {
  if (!attendance.shift) return null;
  const shiftIn = attendance.events.find((e) => e.eventType === 'ShiftIn');
  const shiftOut = attendance.events.find((e) => e.eventType === 'ShiftOut');
  const statusLook = shiftOut
    ? { label: 'เลิกงานแล้ว', pill: 'border-idle-border bg-idle-bg text-idle', Icon: Check }
    : shiftIn
      ? { label: 'กำลังปฏิบัติงาน', pill: 'border-passed-border bg-passed-bg text-passed', Icon: CircleCheck }
      : { label: 'ยังไม่เข้างาน', pill: 'border-overdue-border bg-overdue-bg text-overdue', Icon: Clock };

  return (
    <section
      aria-label="สถานะกะวันนี้"
      className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border bg-surface px-4 py-3"
    >
      <div className="flex min-w-0 items-center gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-md bg-accent-subtle text-accent">
          <CalendarDays aria-hidden="true" className="size-5" />
        </span>
        <div className="flex min-w-0 flex-col">
          <span className="font-bold text-text">{shiftLabel[attendance.shift]}</span>
          <span className="text-sm text-text-2 tabular-nums">
            {shiftIn ? `เข้างาน ${formatTime(shiftIn.occurredAt)}` : 'ยังไม่ได้ลงเวลาเข้างาน'}
            {shiftOut ? ` · เลิกงาน ${formatTime(shiftOut.occurredAt)}` : ''}
          </span>
        </div>
      </div>
      <span className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold ${statusLook.pill}`}>
        <statusLook.Icon aria-hidden="true" className="size-4 shrink-0" />
        <span>{statusLook.label}</span>
      </span>
    </section>
  );
}

function MiniKpis({
  total,
  sent,
  remaining,
  issues,
}: {
  total: number;
  sent: number;
  remaining: number;
  issues: number;
}) {
  const tiles = [
    { label: 'งานวันนี้', value: `${total} จุด`, Icon: ClipboardList, tone: 'text-accent' },
    { label: 'ส่งแล้ว', value: `${sent} จุด`, Icon: CircleCheck, tone: 'text-passed' },
    { label: 'คงเหลือ', value: `${remaining} จุด`, Icon: Hourglass, tone: 'text-pending' },
    { label: 'พบปัญหา', value: `${issues} จุด`, Icon: TriangleAlert, tone: 'text-overdue' },
  ] as const;

  return (
    <section aria-label="สรุปงานวันนี้" className="grid grid-cols-4 gap-1.5 sm:gap-2">
      {tiles.map(({ label, value, Icon, tone }) => (
        <div
          key={label}
          className="flex min-w-0 flex-col items-center justify-center gap-1 rounded-lg border border-border bg-surface px-1.5 py-2.5 text-center"
        >
          <Icon aria-hidden="true" className={`size-5 shrink-0 ${tone}`} />
          <span className="text-xs text-text-2">{label}</span>
          <span className="text-sm font-bold text-text tabular-nums">{value}</span>
        </div>
      ))}
    </section>
  );
}
