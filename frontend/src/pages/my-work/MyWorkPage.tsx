import { ArrowRightLeft, Check, CircleCheck, Clock, Moon, RefreshCw } from 'lucide-react';
import type { ReactNode } from 'react';
import type { AttendanceState, MyWork, OwnShift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { ConnectionBanner } from '../../shared/components/ConnectionBanner/ConnectionBanner';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { PhoneHeader, PhoneScreen } from '../../shared/components/PhoneScreen';
import { shiftLabel } from '../../shared/utils/labels';
import { formatClock, formatTime } from '../../shared/utils/thaiTime';
import { NextJob } from './NextJob';
import { WorkGroup } from './WorkGroup';
import { useMyWorkPage } from './useMyWorkPage';
import { byUrgency, laterStatuses, sentStatuses, toDo } from './workRules';

/**
 * Cleaner home screen (`/my-work`): puts the single most urgent point (`ทำอันนี้ก่อน`) immediately above the fold,
 * consolidates shift + progress into one compact strip, moves refresh to the top header, and keeps navigation and
 * account/logout strictly in the 3-tab bottom bar (Heuristics #1, #4, #8).
 */
export function MyWorkPage() {
  const { data, errorMessage, isLoading, isFetching, refetch, where } = useMyWorkPage();

  const header = (
    <PhoneHeader
      action={
        <Button variant="secondary" size="phone" disabled={isFetching} onClick={() => void refetch()}>
          <RefreshCw aria-hidden="true" className="size-5" />
          {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </Button>
      }
    >
      <h1 className="text-2xl font-bold text-text">งานของฉัน</h1>
      <span className="min-h-5 min-w-0 text-sm wrap-break-word text-text-2">{where}</span>
    </PhoneHeader>
  );

  return (
    <PhoneScreen
      header={header}
      bottom={
        errorMessage ? (
          <Button variant="primary" size="main" disabled={isFetching} onClick={() => void refetch()}>
            <RefreshCw aria-hidden="true" className="size-6" />
            {isFetching ? 'กำลังโหลด...' : 'ลองอีกครั้ง'}
          </Button>
        ) : undefined
      }
    >
      <ConnectionBanner refreshing={isFetching} onRetry={() => void refetch()} />
      {isLoading && <PageLoading lines={3}>กำลังโหลดงาน...</PageLoading>}
      {errorMessage && <Note tone="error">{errorMessage}</Note>}
      {data && (data.attendance.shift ? <WorkList work={data} /> : <OffShift ownShift={data.ownShift} />)}
    </PhoneScreen>
  );
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
      <ShiftSummaryCard
        attendance={work.attendance}
        sent={sentCount}
        total={ordered.length}
        remaining={todoCount}
        issues={issueCount}
      />
      {covered.length > 0 && (
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
      <WorkGroup title="ต้องทำต่อ" items={rest.filter((item) => toDo.includes(item.point.status))} />
      <WorkGroup title="ส่งแล้ว" items={rest.filter((item) => sentStatuses.includes(item.point.status))} />
      <WorkGroup title="ยังไม่ต้องทำ" items={rest.filter((item) => laterStatuses.includes(item.point.status))} />
    </>
  );
}

/**
 * Single compact strip combining shift check-in status and work progress so NextJob stays above the fold on 375px screens.
 */
function ShiftSummaryCard({
  attendance,
  sent,
  total,
  remaining,
  issues,
}: {
  attendance: AttendanceState;
  sent: number;
  total: number;
  remaining: number;
  issues: number;
}) {
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
      aria-label="สถานะกะและความคืบหน้า"
      className="flex flex-col gap-2.5 border-b border-border pb-3"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex min-w-0 flex-col">
          <span className="font-bold text-text tabular-nums">{shiftLabel[attendance.shift]}</span>
          <span className="text-sm text-text-2 tabular-nums">
            {shiftIn ? `เข้างาน ${formatTime(shiftIn.occurredAt)}` : 'ยังไม่ได้ลงเวลาเข้างาน'}
            {shiftOut ? ` · เลิกงาน ${formatTime(shiftOut.occurredAt)}` : ''}
          </span>
        </div>
        <span className={`inline-flex items-center gap-1.5 rounded-md border px-2.5 py-1 text-xs font-semibold ${statusLook.pill}`}>
          <statusLook.Icon aria-hidden="true" className="size-4 shrink-0" />
          <span>{statusLook.label}</span>
        </span>
      </div>

      <div className="flex flex-col gap-1.5">
        <div className="flex flex-wrap items-center justify-between gap-2 text-sm tabular-nums">
          <span className="font-semibold text-text">{`ส่งแล้ว ${sent} จาก ${total} จุด`}</span>
          <span className="text-text-2">
            {`คงเหลือ ${remaining}`}
            {issues > 0 && (
              <>
                {' · '}
                <span className="font-semibold text-rework">{`ปัญหา ${issues}`}</span>
              </>
            )}
          </span>
        </div>
        {total > 0 && (
          <div
            role="progressbar"
            aria-label="ความคืบหน้า"
            aria-valuemin={0}
            aria-valuemax={total}
            aria-valuenow={sent}
            className="h-1.5 overflow-hidden rounded-sm bg-skeleton"
          >
            <div className="h-full rounded-sm bg-passed" style={{ width: `${(100 * sent) / total}%` }} />
          </div>
        )}
      </div>
    </section>
  );
}
