import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { blockedScanOf, isNotSent, statusOf } from '../../shared/api/scanAnswers';
import type { AttendanceEvent, AttendanceState, BlockedScan } from '../../shared/api/types';
import { BlockedScanScreen } from '../../shared/components/BlockedScanScreen';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { eventLabel, shiftLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';
import { AttendanceTimeline } from './AttendanceTimeline';
import { useRecordAttendanceMutation } from './checkInApi';
import './checkIn.css';

type Outcome =
  | { kind: 'answered'; eventType: AttendanceEvent; state: AttendanceState; accuracyM: number }
  | { kind: 'failed'; message: string }
  | { kind: 'blocked'; blocked: BlockedScan }
  | { kind: 'location'; problem: LocationProblem; eventType: AttendanceEvent };

/**
 * Wireframes M1, M2, M3, M8 and M10: the page an Area's check-in sign opens (/check-in/<token>, plan 8 decision 1).
 * The buttons are the events the server allows next (facility-0031). The server records, flags or blocks
 * (facility-0035, 0036, 0041) and never computes late or early (facility-0050).
 */
export function CheckInPage() {
  const { token = '' } = useParams();
  const workQuery = useGetMyWorkQuery(undefined, freshOnOpen);
  const [record] = useRecordAttendanceMutation();
  const [pressed, setPressed] = useState<AttendanceEvent | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const work = workQuery.data;
  const attendance = work?.attendance;
  const ownArea = work?.areas.find((area) => !area.isCover);
  // Buttons only from a fresh answer: a cached one may be from before another device recorded something
  const liveAttendance = workQuery.isFetching ? undefined : attendance;

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

  if (outcome?.kind === 'location') {
    const { problem, eventType } = outcome;
    return <LocationHelp problem={problem} action="ลงเวลา" busy={pressed !== null} onRetry={() => void press(eventType)} />;
  }
  if (outcome?.kind === 'blocked') {
    return <BlockedScanScreen blocked={outcome.blocked} sign="check-in" ownAreaName={ownArea?.areaName} />;
  }

  return (
    <main className="checkin-page">
      <header className="checkin-heading">
        <span className="checkin-kicker">ป้ายลงเวลา</span>
        <h1>ลงเวลา</h1>
        {attendance?.shift && <p className="checkin-sub">{shiftLabel[attendance.shift]}</p>}
        {ownArea && <p className="checkin-area">{`Area ของคุณ: ${ownArea.areaName} · ${ownArea.points.length} จุด`}</p>}
      </header>
      {workQuery.isLoading && <p className="checkin-hint">กำลังโหลด...</p>}
      {workQuery.error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(workQuery.error)}
        </p>
      )}
      {attendance && !attendance.shift && (
        <p className="app-note app-note--warn">
          ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ ลงเวลาได้ตั้งแต่ 60 นาทีก่อนกะเริ่มจนถึง 60 นาทีหลังกะจบ
        </p>
      )}
      {attendance?.shift && <AttendanceTimeline attendance={attendance} />}
      {outcome?.kind === 'answered' && (
        <AnsweredNote eventType={outcome.eventType} state={outcome.state} accuracyM={outcome.accuracyM} />
      )}
      {outcome?.kind === 'failed' && (
        <p className="app-note app-note--error" role="alert">
          {outcome.message}
        </p>
      )}
      {liveAttendance?.shift && liveAttendance.nextEvents.length === 0 && (
        <p className="app-note app-note--info">ลงเวลาครบแล้วสำหรับกะนี้</p>
      )}
      {liveAttendance && liveAttendance.nextEvents.length > 0 && (
        <div className="checkin-actions">
          {liveAttendance.nextEvents.map((eventType, index) => (
            <button
              key={eventType}
              type="button"
              className={index === 0 ? 'checkin-press checkin-press--main' : 'checkin-press'}
              disabled={pressed !== null}
              onClick={() => void press(eventType)}
            >
              {pressed === eventType ? 'กำลังบันทึก...' : eventLabel[eventType]}
            </button>
          ))}
        </div>
      )}
      <p className="checkin-hint">ระบบแสดงปุ่มถัดไปให้เองตามลำดับ · กดซ้ำภายใน 5 นาทีไม่นับ · ไม่คิดสายหรือกลับก่อน</p>
      <Link className="app-button" to="/my-work">
        ดูงานของฉัน
      </Link>
    </main>
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
      <p className="app-note app-note--warn" role="status">
        {`ยังไม่บันทึก${label} เพราะเพิ่งลงเวลาไปไม่ถึง 5 นาที กดซ้ำภายใน 5 นาทีไม่นับ`}
      </p>
    );
  }
  if (state.outcome === 'AlreadyRecorded') {
    return (
      <p className="app-note app-note--info" role="status">
        {`${label}: ลงเวลาไว้แล้วตอน ${time} ระบบเก็บเวลาแรกไว้`}
      </p>
    );
  }
  if (recorded?.withinRadius === false) {
    return (
      <div className="app-note app-note--warn" role="status">
        <strong>{`บันทึกเวลา${label}แล้ว ${time} แต่อยู่ไกลจากป้าย · ติดธง`}</strong>
        <span>{`ความแม่นยำของตำแหน่ง ±${Math.round(accuracyM)} ม.`}</span>
        <span>Admin จะเห็นรายการนี้ ถ้าคุณยืนอยู่หน้าป้ายจริง แจ้ง Admin ได้เลย</span>
      </div>
    );
  }
  return (
    <p className="app-note app-note--info" role="status">
      {`บันทึกเวลา${label}แล้ว ${time}`}
    </p>
  );
}
