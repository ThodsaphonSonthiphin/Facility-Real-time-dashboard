import { useEffect, useRef, useState } from 'react';
import type { AssignedCleaner } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import type { AttendanceEvent } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { boxClass } from '../../shared/components/styles';
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
  const headingRef = useRef<HTMLHeadingElement>(null);

  // The form opens above the table: take focus so it scrolls into view and keyboard and screen-reader users land on it
  useEffect(() => {
    headingRef.current?.focus();
  }, []);

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
    <section className={`flex max-w-160 flex-col gap-3 ${boxClass}`} aria-labelledby={headingId}>
      <div className="flex flex-col gap-0.5">
        <h2 id={headingId} ref={headingRef} tabIndex={-1} className="text-base font-bold">
          {`เพิ่ม/แก้เวลา · ${cleaner.displayName} · ${row.areaCode}`}
        </h2>
        <span className="text-muted">{`${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)}`}</span>
      </div>
      <div className="flex flex-wrap gap-3">
        <Field label="รายการ">
          <Select size="desk" value={eventType} onChange={(event) => choose(event.target.value as AttendanceEvent)}>
            {attendanceEvents.map((type) => {
              const existing = eventOf(row, type);
              return (
                <option key={type} value={type}>
                  {`${eventLabel[type]} (${existing ? `ตอนนี้ ${formatTime(existing.occurredAt)}` : 'ยังไม่มี'})`}
                </option>
              );
            })}
          </Select>
        </Field>
        <Field label="เวลา (ชม.:นาที)">
          <TextInput
            size="desk"
            value={time}
            placeholder="07:05"
            inputMode="numeric"
            maxLength={5}
            onChange={(event) => setTime(event.target.value)}
          />
        </Field>
      </div>
      <Field label="เหตุผล (ต้องกรอก)">
        <TextInput size="desk" value={reason} maxLength={MAX_REASON} onChange={(event) => setReason(event.target.value)} />
      </Field>
      <span className="text-muted">
        ระบบเก็บค่าเดิม ผู้แก้ และเวลาที่แก้ไว้ในประวัติ · รายการนี้จะมีป้าย "แก้โดยแอดมิน" · แม่บ้านและหัวหน้าแก้เองไม่ได้
      </span>
      {problem && <Note tone="error">{problem}</Note>}
      <div className="flex flex-wrap gap-2">
        <Button variant="primary" size="desk" disabled={isLoading} onClick={() => void save()}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึก'}
        </Button>
        <Button size="desk" disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </Button>
      </div>
    </section>
  );
}

function timeOf(row: BoardRow, type: AttendanceEvent): string {
  const existing = eventOf(row, type);
  return existing ? formatTime(existing.occurredAt) : '';
}
