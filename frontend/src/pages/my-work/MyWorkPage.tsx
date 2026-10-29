import { RefreshCw } from 'lucide-react';
import { freshOnOpen } from '../../shared/api/baseApi';
import { MY_WORK_HUB, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import type { AttendanceState, MyWork, OwnShift, Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
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

  return (
    <main className="flex flex-col gap-4 text-base text-text">
      <header className="flex flex-col gap-0.5">
        <h1 className="text-2xl font-bold">งานของฉัน</h1>
        {data && shift && <p className="text-sm text-text-2">{attendanceLine(shift, data.attendance)}</p>}
      </header>
      {isLoading && <p className="text-text-2">กำลังโหลดงาน...</p>}
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
        </footer>
      )}
    </main>
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
    <Note tone="info" title="ตอนนี้ไม่อยู่ในกะของคุณ">
      {ownShift && <span>{shiftLabel[ownShift.shift]}</span>}
      {ownShift && <span>{`ลงเวลาเข้างานได้ตั้งแต่ ${formatClock(ownShift.opensAt)}`}</span>}
    </Note>
  );
}

function WorkList({ work }: { work: MyWork }) {
  if (work.areas.length === 0) {
    return (
      <Note tone="info">ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับพื้นที่ หรือพื้นที่ยังไม่มีจุด แจ้งแอดมิน</Note>
    );
  }
  const covered = work.areas.filter((area) => area.isCover);
  const ordered = byUrgency(work.areas);
  const next = ordered.find((item) => toDo.includes(item.point.status));
  const rest = ordered.filter((item) => item !== next);

  return (
    <>
      {covered.length > 0 && (
        <Note tone="info">
          {`วันนี้ทำแทน ${covered.map((area) => area.areaName).join(', ')} ด้วย · สิทธิ์หมดเองเมื่อหมดกะ`}
        </Note>
      )}
      {next ? <NextJob item={next} /> : <Note tone="info">ส่งครบทุกจุดที่ต้องทำตอนนี้แล้ว</Note>}
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
 * "x จาก y จุด ส่งแล้ว", where ส่งแล้ว is รอตรวจ and ตรวจผ่าน, and a bar of the same shares in their status colours;
 * the words carry it, the bar is for the eye only.
 */
function Progress({ passed, pending, total }: { passed: number; pending: number; total: number }) {
  const share = (n: number) => `${(100 * n) / total}%`;
  return (
    <div className="flex flex-col gap-1.5">
      <span className="text-sm">{`${passed + pending} จาก ${total} จุด ส่งแล้ว`}</span>
      {/* The shares are data, not tokens, so the widths are inline styles */}
      <div aria-hidden="true" className="flex h-2.5 overflow-hidden rounded-full bg-border">
        <div className="h-full bg-passed" style={{ width: share(passed) }} />
        <div className="h-full bg-pending" style={{ width: share(pending) }} />
      </div>
    </div>
  );
}
