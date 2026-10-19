import { useState } from 'react';
import type { AssignedCleaner } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import type { AttendanceEvent } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { attendanceEvents, eventLabel, shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate, formatTime } from '../../shared/utils/thaiTime';
import { useCorrectAttendanceMutation } from './attendanceApi';
import { correctionProblem, eventOf, firstEvent, MAX_REASON } from './attendanceRules';

interface CorrectionFormProps {
  board: AttendanceBoard;
  row: BoardRow;
  /** The row's regular Cleaner: only they can be corrected on this row (the server checks their shift). */
  cleaner: AssignedCleaner;
  initialEvent?: AttendanceEvent;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * facility-0051, wireframe D2: the Admin adds a missing time or moves one, always with a reason. The row then carries
 * "แก้โดย Admin" and the old time stays in the history. The draft lives here only (facility-0024); one send at a time.
 */
export function CorrectionForm({ board, row, cleaner, initialEvent, onDone, onCancel }: CorrectionFormProps) {
  const start = firstEvent(row, initialEvent);
  const [eventType, setEventType] = useState<AttendanceEvent>(start);
  const [time, setTime] = useState(() => timeOf(row, start));
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [correct, { isLoading }] = useCorrectAttendanceMutation();
  const headingId = `attendance-correction-${row.areaId}`;

  const choose = (next: AttendanceEvent) => {
    setEventType(next);
    setTime(timeOf(row, next));
  };

  const save = async () => {
    const check = correctionProblem({ time, reason });
    if (check) {
      setProblem(check);
      return;
    }
    setProblem(null);
    try {
      await correct({
        userId: cleaner.id,
        shiftDate: board.shiftDate,
        shift: board.shift,
        eventType,
        time: time.trim(),
        reason: reason.trim(),
      }).unwrap();
      onDone(`บันทึกเวลา${eventLabel[eventType]}ของ ${cleaner.displayName} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <section className="app-panel attendance-form" aria-labelledby={headingId}>
      <h2 id={headingId}>{`เพิ่ม/แก้เวลา · ${cleaner.displayName} · ${row.areaCode}`}</h2>
      <span className="app-muted">{`${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)}`}</span>
      <div className="attendance-form__row">
        <label className="app-field">
          รายการ
          <select className="app-input" value={eventType} onChange={(event) => choose(event.target.value as AttendanceEvent)}>
            {attendanceEvents.map((type) => {
              const existing = eventOf(row, type);
              return (
                <option key={type} value={type}>
                  {`${eventLabel[type]} (${existing ? `ตอนนี้ ${formatTime(existing.occurredAt)}` : 'ยังไม่มี'})`}
                </option>
              );
            })}
          </select>
        </label>
        <label className="app-field">
          เวลา (ชม.:นาที)
          <input
            className="app-input"
            value={time}
            placeholder="07:05"
            inputMode="numeric"
            maxLength={5}
            onChange={(event) => setTime(event.target.value)}
          />
        </label>
      </div>
      <label className="app-field">
        เหตุผล (ต้องกรอก)
        <input
          className="app-input"
          value={reason}
          maxLength={MAX_REASON}
          onChange={(event) => setReason(event.target.value)}
        />
      </label>
      <span className="app-field__help">
        ระบบเก็บค่าเดิม ผู้แก้ และเวลาที่แก้ไว้ในประวัติ · รายการนี้จะมีป้าย "แก้โดย Admin" · แม่บ้านและหัวหน้าแก้เองไม่ได้
      </span>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="app-confirm__buttons">
        <button type="button" className="app-button app-button--primary" disabled={isLoading} onClick={() => void save()}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </section>
  );
}

function timeOf(row: BoardRow, type: AttendanceEvent): string {
  const existing = eventOf(row, type);
  return existing ? formatTime(existing.occurredAt) : '';
}
