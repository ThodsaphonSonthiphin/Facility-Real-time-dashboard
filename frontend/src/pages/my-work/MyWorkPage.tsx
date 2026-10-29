import { ArrowRightLeft, Check, LogOut, Moon, RefreshCw } from 'lucide-react';
import type { ReactNode } from 'react';
import { freshOnOpen } from '../../shared/api/baseApi';
import { MY_WORK_HUB, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import type { AttendanceState, MyWork, OwnShift, Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { PhoneHeader, PhoneScreen } from '../../shared/components/PhoneScreen';
import { endSession } from '../../shared/auth/endSession';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { formatClock, formatTime } from '../../shared/utils/thaiTime';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { selectHubStatus, type HubStatus } from '../../store/slices/hubSlice';
import { NextJob } from './NextJob';
import { WorkGroup } from './WorkGroup';
import { byUrgency, laterStatuses, sentStatuses, toDo } from './workRules';

/**
 * facility-0052, wireframes M9 and M14: every point of the Cleaner's own Area, then of the Areas they cover this
 * shift. It changes by itself when the hub says a point changed (the Supervisor's inspection, owner 2026-10-26, which
 * amends facility-0052), and also asks each time it opens and on "อัปเดต". No card links to the scan page; the only way
 * there is the sign's QR (facility-0059). The name is not shown: the bar above already shows it.
 * The layout is variant B of the mockups (#73, `design-system/facility/pages/phone.md`): the next job first.
 */
/** The footnote by the hub's state, as My Building's (plan 9 decision 9). */
const liveLine: Record<HubStatus, string> = {
  connecting: 'กำลังเชื่อมต่อการอัปเดตสด...',
  live: 'หน้านี้อัปเดตเองเมื่อหัวหน้าตรวจงาน',
  reconnecting: 'การอัปเดตสดหลุด กำลังต่อใหม่ กด "อัปเดต" เพื่อดูงานล่าสุด',
};

export function MyWorkPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyWorkQuery(undefined, freshOnOpen);
  const shift = data?.attendance.shift;
  const hubStatus = useAppSelector(selectHubStatus(MY_WORK_HUB));
  const user = useAppSelector((state) => state.auth.currentUser);
  const dispatch = useAppDispatch();
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
          <Button variant="link" className="self-center" onClick={() => void endSession(dispatch)}>
            <LogOut aria-hidden="true" className="size-4" />
            ออกจากระบบ
          </Button>
        </footer>
      )}
    </PhoneScreen>
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
    return <Note tone="info">ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับพื้นที่ หรือพื้นที่ยังไม่มีจุด แจ้งแอดมิน</Note>;
  }
  const covered = work.areas.filter((area) => area.isCover);
  const ordered = byUrgency(work.areas);
  const next = ordered.find((item) => toDo.includes(item.point.status));
  const rest = ordered.filter((item) => item !== next);

  return (
    <>
      {covered.length > 0 && (
        // The design file's cover line: purple, the colour of ทำแทน
        <p className="flex items-start gap-2 rounded-md border border-break-border bg-break-bg px-3 py-2 text-sm text-break">
          <ArrowRightLeft aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          {`วันนี้ทำแทน ${covered.map((area) => area.areaName).join(', ')} ด้วย · สิทธิ์หมดเองเมื่อหมดกะ`}
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
        passed={ordered.filter((item) => item.point.status === 'Passed').length}
        pending={ordered.filter((item) => item.point.status === 'PendingInspection').length}
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
