import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { blockedScanOf, isNotSent, statusOf } from '../../shared/api/scanAnswers';
import type { AttendanceEvent, AttendanceState, BlockedScan, OwnShift } from '../../shared/api/types';
import { BlockedScanScreen } from '../../shared/components/BlockedScanScreen';
import { Button } from '../../shared/components/Button';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { buttonClass } from '../../shared/components/styles';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { eventLabel, shiftLabel } from '../../shared/utils/labels';
import { formatClock, formatTime } from '../../shared/utils/thaiTime';
import { AttendanceTimeline } from './AttendanceTimeline';
import { useCheckSignQuery, useRecordAttendanceMutation } from './checkInApi';

type Outcome =
  | { kind: 'answered'; eventType: AttendanceEvent; state: AttendanceState; accuracyM: number }
  | { kind: 'failed'; message: string }
  | { kind: 'blocked'; blocked: BlockedScan }
  | { kind: 'location'; problem: LocationProblem; eventType: AttendanceEvent };

/**
 * Wireframes M1, M2, M3, M8 and M10: the page an Area's check-in sign opens (/check-in/<token>, plan 8 decision 1).
 * On opening it asks whether the sign still works (facility-0082). The buttons are the events the server allows next
 * (facility-0031). The server records, flags or blocks (facility-0035, 0036, 0041) and never computes late or early
 * (facility-0050).
 */
export function CheckInPage() {
  const { token = '' } = useParams();
  const signQuery = useCheckSignQuery(token, freshOnOpen);
  const workQuery = useGetMyWorkQuery(undefined, freshOnOpen);
  const [record] = useRecordAttendanceMutation();
  const [pressed, setPressed] = useState<AttendanceEvent | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const work = workQuery.data;
  const attendance = work?.attendance;
  const ownArea = work?.areas.find((area) => !area.isCover);
  // A 404 is a sign that no longer works; any other failure of the check shows like a failed load
  const signGone = statusOf(signQuery.error) === 404;
  const loadError = workQuery.error ?? (signGone ? undefined : signQuery.error);
  // Buttons only from fresh answers, once the server has said the sign works: a cached answer may be from before
  // another device recorded something
  const signWorks = signQuery.isSuccess && !signQuery.isFetching;
  const liveAttendance = workQuery.isFetching || !signWorks ? undefined : attendance;

  const press = async (eventType: AttendanceEvent) => {
    setPressed(eventType);
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'location', problem: reading.kind, eventType });
        return;
      }
      const answer = await record({ qrToken: token, eventType, ...reading.fix });
      if (answer.data) {
        setOutcome({ kind: 'answered', eventType, state: answer.data, accuracyM: reading.fix.accuracyM });
        return;
      }
      const blocked = blockedScanOf(answer.error);
      if (blocked) {
        setOutcome({ kind: 'blocked', blocked });
        return;
      }
      setOutcome({
        kind: 'failed',
        message: isNotSent(answer.error)
          ? `ยังไม่ได้บันทึกเวลา ${getErrorMessage(answer.error)}`
          : getErrorMessage(answer.error),
      });
      if (statusOf(answer.error) === 409) {
        void workQuery.refetch(); // out of order or outside the window: show what the server allows now
      }
    } finally {
      setPressed(null);
    }
  };

  if (signGone) {
    return (
      <StatusScreen
        title="ป้ายนี้ใช้ไม่ได้แล้ว"
        detail="ยังไม่ได้ลงเวลา ป้ายนี้อาจถูกสร้าง QR ใหม่แล้ว หรือไม่มีในระบบ แจ้งแอดมิน"
        linkTo="/my-work"
        linkText="ดูงานของฉัน"
      />
    );
  }
  if (outcome?.kind === 'location') {
    const { problem, eventType } = outcome;
    return <LocationHelp problem={problem} action="ลงเวลา" busy={pressed !== null} onRetry={() => void press(eventType)} />;
  }
  if (outcome?.kind === 'blocked') {
    return <BlockedScanScreen blocked={outcome.blocked} sign="check-in" ownAreaName={ownArea?.areaName} />;
  }

  return (
    // flex-1: the rest of the screen under the shell's bar, so the buttons sit low, in thumb reach. The shell is
    // min-h-svh, not dvh: a phone hides its toolbar while the page scrolls, dvh grows with it, and the buttons slid
    // along with the scroll (owner, 2026-10-27)
    <main className="flex flex-1 flex-col gap-4 text-base text-text">
      <header className="flex flex-col gap-1">
        <span className="text-sm tracking-wide text-text-2">ป้ายลงเวลา</span>
        <h1 className="text-2xl font-bold">ลงเวลา</h1>
        {attendance?.shift && <p className="text-lg font-semibold">{shiftLabel[attendance.shift]}</p>}
        {ownArea && <p className="text-sm text-text-2">{`พื้นที่ของคุณ: ${ownArea.areaName} · ${ownArea.points.length} จุด`}</p>}
      </header>
      {(workQuery.isLoading || signQuery.isLoading) && <p className="text-sm text-text-2">กำลังโหลด...</p>}
      {loadError != null && <Note tone="error">{getErrorMessage(loadError)}</Note>}
      {attendance && !attendance.shift && <OffShiftNote ownShift={work?.ownShift} />}
      {attendance?.shift && <AttendanceTimeline attendance={attendance} />}
      {outcome?.kind === 'answered' && (
        <AnsweredNote eventType={outcome.eventType} state={outcome.state} accuracyM={outcome.accuracyM} />
      )}
      {outcome?.kind === 'failed' && <Note tone="error">{outcome.message}</Note>}
      {liveAttendance?.shift && liveAttendance.nextEvents.length === 0 && (
        <Note tone="info">ลงเวลาครบแล้วสำหรับกะนี้</Note>
      )}
      {liveAttendance && liveAttendance.nextEvents.length > 0 && (
        // At most one blue button per screen (pages/phone.md): the next event; the others are outlined, all 56 px
        <div className="mt-auto flex flex-col gap-2.5">
          {liveAttendance.nextEvents.map((eventType, index) => (
            <Button
              key={eventType}
              variant={index === 0 ? 'primary' : 'secondary'}
              size="main"
              disabled={pressed !== null}
              onClick={() => void press(eventType)}
            >
              {pressed === eventType ? 'กำลังบันทึก...' : eventLabel[eventType]}
            </Button>
          ))}
        </div>
      )}
      <MoreInfo size="phone" summary="เกี่ยวกับการลงเวลา">
        ระบบแสดงปุ่มถัดไปให้เองตามลำดับ · กดซ้ำภายใน 5 นาทีไม่นับ · ไม่คิดสายหรือกลับก่อน
      </MoreInfo>
      <Link className={buttonClass('secondary', 'phone')} data-tap="" to="/my-work">
        ดูงานของฉัน
      </Link>
    </main>
  );
}

/** facility-0077: outside the attendance window the Cleaner is told their own shift and when it can be recorded. */
function OffShiftNote({ ownShift }: { ownShift?: OwnShift }) {
  return (
    <Note tone="warn" title="ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ">
      {ownShift && <span>{`กะของคุณ: ${shiftLabel[ownShift.shift]}`}</span>}
      {ownShift && (
        <span>{`ลงเวลาได้ตั้งแต่ ${formatClock(ownShift.opensAt)} ถึง ${formatClock(ownShift.closesAt)}`}</span>
      )}
    </Note>
  );
}

interface AnsweredNoteProps {
  eventType: AttendanceEvent;
  state: AttendanceState;
  accuracyM: number;
}

/** What the server did with the press: recorded (maybe flagged, M3), already recorded, or too soon (facility-0026, 0031). */
function AnsweredNote({ eventType, state, accuracyM }: AnsweredNoteProps) {
  const label = eventLabel[eventType];
  const recorded = state.events.find((e) => e.eventType === eventType);
  const time = recorded ? formatTime(recorded.occurredAt) : '';

  if (state.outcome === 'TooSoon') {
    return (
      <Note tone="warn" status>
        {`ยังไม่บันทึก${label} เพราะเพิ่งลงเวลาไปไม่ถึง 5 นาที กดซ้ำภายใน 5 นาทีไม่นับ`}
      </Note>
    );
  }
  if (state.outcome === 'AlreadyRecorded') {
    return (
      <Note tone="info" status>
        {`${label}: ลงเวลาไว้แล้วตอน ${time} ระบบเก็บเวลาแรกไว้`}
      </Note>
    );
  }
  if (recorded?.withinRadius === false) {
    return (
      <Note tone="warn" status title={`บันทึกเวลา${label}แล้ว ${time} แต่อยู่ไกลป้าย`}>
        <span>{`ความแม่นยำของตำแหน่ง ±${Math.round(accuracyM)} ม.`}</span>
        <span>แอดมินจะเห็นรายการนี้ ถ้าคุณยืนอยู่หน้าป้ายจริง แจ้งแอดมินได้เลย</span>
      </Note>
    );
  }
  return (
    <Note tone="info" status>
      {`บันทึกเวลา${label}แล้ว ${time}`}
    </Note>
  );
}
