import { ArrowRight, Check, Info, TriangleAlert, Undo2 } from 'lucide-react';
import { useState } from 'react';
import type { AssignedCleaner } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import type { AttendanceEvent } from '../../shared/api/types';
import { EditedMark } from '../../shared/components/Badge';
import { Button } from '../../shared/components/Button';
import { TextArea, TextInput } from '../../shared/components/Field';
import { DialogFooter, DialogHeader, Modal } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { Spinner } from '../../shared/components/PageState';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { attendanceEvents, eventLabel, shiftLabel } from '../../shared/utils/labels';
import { formatShiftDate, formatTime } from '../../shared/utils/thaiTime';
import { useCorrectAttendanceMutation } from './attendanceApi';
import {
  changedEvents,
  correctionReason,
  correctionReasons,
  draftProblems,
  eventOf,
  firstEvent,
  MAX_REASON,
  OTHER_REASON,
  reasonProblem,
  type CorrectionReason,
  type DraftTimes,
} from './attendanceRules';

interface CorrectionFormProps {
  board: AttendanceBoard;
  row: BoardRow;
  /** The row's regular Cleaner: only they can be corrected on this row (the server checks their shift). */
  cleaner: AssignedCleaner;
  /** The time an alert's "เติมเวลา…" link asks for: its row is tinted, so the Admin sees where to type. */
  initialEvent?: AttendanceEvent;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/** A send that failed: what the server said, and which times were already saved before it. */
type Failure = { message: string; saved: AttendanceEvent[]; left: AttendanceEvent[] };

const labels = (types: readonly AttendanceEvent[]) => types.map((type) => eventLabel[type]).join(' และ');

/**
 * facility-0051, the design file's ฟอร์มแก้เวลา (600 px): the Admin changes any of the four times at once, always with a
 * reason. The server takes one time per request, so the form sends each changed time in turn (owner, 2026-10-29); one
 * the server refuses for its order is tried again after the others, since moving two times may need the later one
 * first. Each corrected time then carries "แก้โดย Admin" and the old one stays in the history. The draft lives here
 * only (facility-0024); one save at a time.
 */
export function CorrectionForm({ board, row, cleaner, initialEvent, onDone, onCancel }: CorrectionFormProps) {
  const recorded = (type: AttendanceEvent) => {
    const event = eventOf(row, type);
    return event ? formatTime(event.occurredAt) : '';
  };
  const [values, setValues] = useState<Record<AttendanceEvent, string>>(() => {
    const start = {} as Record<AttendanceEvent, string>;
    attendanceEvents.forEach((type) => (start[type] = recorded(type)));
    return start;
  });
  // The times already saved by a send that stopped half way: the board may not have them yet
  const [saved, setSaved] = useState<Partial<Record<AttendanceEvent, string>>>({});
  const [reason, setReason] = useState<CorrectionReason | null>(null);
  const [detail, setDetail] = useState('');
  const [tried, setTried] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [busy, setBusy] = useState(false);
  const [correct] = useCorrectAttendanceMutation();
  const headingId = `attendance-correction-${row.areaId}`;
  const asked = firstEvent(row, initialEvent);

  const times = {} as DraftTimes;
  attendanceEvents.forEach((type) => (times[type] = { old: saved[type] ?? recorded(type), value: values[type] }));
  const changed = changedEvents(times);
  const problems = draftProblems(times, board.shift);
  const reasonError = reasonProblem(reason, detail);
  const nothing = changed.length === 0 ? 'ยังไม่ได้แก้เวลาช่องไหน' : null;
  const messages = [nothing, ...attendanceEvents.map((type) => problems[type]), reasonError].filter(
    (message): message is string => !!message,
  );
  const show = tried && messages.length > 0;

  const save = async () => {
    setTried(true);
    setFailure(null);
    if (messages.length > 0 || !reason) {
      return;
    }
    setBusy(true);
    const text = correctionReason(reason, detail);
    const done: AttendanceEvent[] = [];
    let pending = changed;
    let message = '';
    // Each pass sends what is left; a refusal for the order may pass once another time has moved
    while (pending.length > 0) {
      const left: AttendanceEvent[] = [];
      let stop = false;
      for (const type of pending) {
        if (stop) {
          left.push(type);
          continue;
        }
        try {
          await correct({
            userId: cleaner.id,
            shiftDate: board.shiftDate,
            shift: board.shift,
            eventType: type,
            time: values[type].trim(),
            reason: text,
          }).unwrap();
          done.push(type);
        } catch (error) {
          left.push(type);
          message = getErrorMessage(error);
          // Only a 400 (the order, the window) may pass later; anything else stops the send
          stop = (error as { status?: unknown }).status !== 400;
        }
      }
      if (left.length === pending.length || stop) {
        pending = left;
        break;
      }
      pending = left;
    }
    setBusy(false);
    if (pending.length === 0) {
      onDone(
        `บันทึกเวลา${labels(attendanceEvents.filter((type) => done.includes(type)))}ของ ${cleaner.displayName} แล้ว`,
      );
      return;
    }
    setSaved((before) => {
      const next = { ...before };
      done.forEach((type) => (next[type] = values[type].trim()));
      return next;
    });
    setFailure({ message, saved: done, left: pending });
  };

  return (
    <Modal labelledBy={headingId} width="lg" scrollBody onClose={() => !busy && onCancel()}>
      <DialogHeader
        id={headingId}
        title="แก้เวลา"
        detail={`${row.areaCode} ${row.areaName} · ${cleaner.displayName} · ${shiftLabel[board.shift]} ${formatShiftDate(board.shiftDate)}`}
        closeDisabled={busy}
        onClose={onCancel}
      />
      <div className="relative -mx-6 flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto px-6 pb-5">
        {failure && (
          <Note tone="error" title="บันทึกไม่สำเร็จ">
            <span>{failure.message}</span>
            {failure.saved.length > 0 && (
              <span>{`บันทึกแล้ว: ${labels(failure.saved)} · ยังไม่ได้บันทึก: ${labels(failure.left)}`}</span>
            )}
            <span>เวลาที่กรอกยังอยู่ แก้แล้วกด "บันทึกการแก้ไข" อีกครั้ง</span>
          </Note>
        )}
        {show && (
          <Note tone="error" title={`ยังบันทึกไม่ได้ มี ${messages.length} จุดที่ต้องแก้`}>
            <span>{messages.join(' · ')}</span>
          </Note>
        )}
        <div className="flex gap-2 rounded-md bg-bg px-3 py-2.5 text-text-3">
          <Info aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-text-2" />
          <span>เวลาต้องอยู่ในช่วงลงเวลาของกะ และเรียง เข้างาน → ออกพัก → กลับจากพัก → เลิกงาน</span>
        </div>
        <fieldset className="flex flex-col gap-2" disabled={busy}>
          <legend className="mb-2 text-base font-bold">เลือกช่องที่จะแก้</legend>
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full border-collapse text-sm [&_td]:border-b [&_td]:border-border [&_td]:px-3 [&_td]:py-2.5 [&_th]:border-b [&_th]:border-border [&_th]:bg-surface-2 [&_th]:px-3 [&_th]:py-2.5 [&_th]:text-left [&_th]:text-xs [&_th]:font-semibold [&_th]:text-text-2 [&_tr:last-child>td]:border-b-0">
              <thead>
                <tr>
                  <th scope="col" className="w-33">
                    จังหวะ
                  </th>
                  <th scope="col" className="w-37.5 text-right!">
                    เวลาที่ลงไว้
                  </th>
                  <th scope="col" className="w-11">
                    <span className="sr-only">เปลี่ยนเป็น</span>
                  </th>
                  <th scope="col">เวลาใหม่</th>
                </tr>
              </thead>
              <tbody>
                {attendanceEvents.map((type) => {
                  const event = eventOf(row, type);
                  const old = times[type].old;
                  const isChanged = changed.includes(type);
                  const problem = tried ? problems[type] : undefined;
                  return (
                    <tr key={type} className={isChanged || type === asked ? '[&>td]:bg-accent-subtle/50' : undefined}>
                      <td className="font-semibold">{eventLabel[type]}</td>
                      <td className="text-right whitespace-nowrap tabular-nums">
                        <span className="flex items-center justify-end gap-1.5">
                          {event && event.source !== 'Scan' && <EditedMark />}
                          <span className={isChanged ? 'text-text-2 line-through' : old ? undefined : 'text-text-2'}>
                            {old || 'ไม่ได้ลง'}
                          </span>
                        </span>
                      </td>
                      <td className="text-center text-text-2">
                        {isChanged && <ArrowRight aria-hidden="true" className="inline size-4" />}
                      </td>
                      <td>
                        <span className="flex items-center gap-2">
                          <TextInput
                            size="desk"
                            aria-label={`เวลาใหม่ ${eventLabel[type]}`}
                            className="w-32 tabular-nums data-changed:border-2 data-changed:border-accent"
                            data-changed={isChanged && !problem ? '' : undefined}
                            aria-invalid={problem ? true : undefined}
                            aria-describedby={problem ? `${headingId}-${type}-error` : undefined}
                            value={values[type]}
                            placeholder="--:--"
                            inputMode="numeric"
                            maxLength={5}
                            onChange={(change) => setValues((before) => ({ ...before, [type]: change.target.value }))}
                          />
                          {isChanged && (
                            <Button
                              variant="link"
                              size="desk"
                              aria-label={`คืนค่า${eventLabel[type]}`}
                              onClick={() => setValues((before) => ({ ...before, [type]: old }))}
                            >
                              <Undo2 aria-hidden="true" className="size-4" />
                              คืนค่า
                            </Button>
                          )}
                        </span>
                        {problem && (
                          <span id={`${headingId}-${type}-error`} className="mt-1 flex items-center gap-1 text-xs font-semibold text-rework">
                            <TriangleAlert aria-hidden="true" className="size-4 shrink-0" />
                            {problem}
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <span className="text-xs text-text-2">ช่องที่ไม่ได้เปลี่ยนจะไม่ถูกแก้ และไม่ติดป้าย</span>
        </fieldset>
        <fieldset className="flex flex-col gap-2" disabled={busy}>
          <legend className="mb-2 text-base font-bold">
            เหตุผล <span className="text-rework">*</span>
          </legend>
          <div
            role="radiogroup"
            aria-label="เหตุผล"
            aria-invalid={tried && !reason ? true : undefined}
            aria-describedby={tried && !reason ? `${headingId}-reason-error` : undefined}
            className="flex flex-wrap gap-2"
          >
            {correctionReasons.map((choice) => (
              <label
                key={choice}
                className={
                  'm-px inline-flex min-h-9 cursor-pointer items-center gap-1.5 rounded-md border px-3 font-semibold ' +
                  `${tried && !reason ? 'border-rework' : 'border-border-control'} bg-surface text-text ` +
                  'has-checked:m-0 has-checked:border-2 has-checked:border-accent has-checked:bg-accent-subtle ' +
                  'has-checked:text-accent-hover has-focus-visible:outline-3 has-focus-visible:outline-offset-2 ' +
                  'has-focus-visible:outline-accent has-disabled:cursor-not-allowed has-disabled:opacity-50'
                }
              >
                <input
                  type="radio"
                  className="sr-only"
                  name={`${headingId}-reason`}
                  checked={reason === choice}
                  onChange={() => setReason(choice)}
                />
                {reason === choice && <Check aria-hidden="true" className="size-4" />}
                {choice}
              </label>
            ))}
          </div>
          {tried && reasonError && !reason && (
            <span id={`${headingId}-reason-error`} className="flex items-center gap-1 text-xs font-semibold text-rework">
              <TriangleAlert aria-hidden="true" className="size-4 shrink-0" />
              {reasonError}
            </span>
          )}
          <label className="mt-2 flex flex-col gap-1">
            <span className="font-semibold">
              {'รายละเอียดเพิ่มเติม '}
              <span className="font-normal text-text-2">
                {reason === OTHER_REASON ? '(ต้องใส่เมื่อเลือก อื่น ๆ)' : '(ไม่บังคับ)'}
              </span>
            </span>
            <TextArea
              size="desk"
              rows={2}
              aria-invalid={tried && reason && reasonError ? true : undefined}
              aria-describedby={tried && reason && reasonError ? `${headingId}-detail-error` : undefined}
              maxLength={MAX_REASON - 20}
              placeholder="เช่น แจ้งหัวหน้าทางโทรศัพท์ตอน 09:35"
              value={detail}
              onChange={(event) => setDetail(event.target.value)}
            />
          </label>
          {tried && reason && reasonError && (
            <span id={`${headingId}-detail-error`} className="flex items-center gap-1 text-xs font-semibold text-rework">
              <TriangleAlert aria-hidden="true" className="size-4 shrink-0" />
              {reasonError}
            </span>
          )}
        </fieldset>
        <div className="flex flex-col gap-1 rounded-md border border-pending-border bg-accent-subtle px-4 py-3">
          <strong>สรุปก่อนบันทึก</strong>
          <span>
            {changed.length === 0
              ? 'ยังไม่ได้แก้เวลาช่องไหน'
              : `จะแก้ ${changed.length} ช่อง: ${changed
                  .map((type) => `${eventLabel[type]} ${times[type].old || 'ไม่ได้ลง'} → ${values[type].trim()}`)
                  .join(' · ')}`}
          </span>
          <span className="text-xs text-text-3">
            ช่องที่แก้จะติดป้าย [แก้] ในหน้าการเข้างาน และบันทึกผู้แก้ เวลา ค่าก่อน/หลัง และเหตุผลในประวัติการแก้ไข ·
            แม่บ้านและหัวหน้าแก้เองไม่ได้
          </span>
        </div>
      </div>
      <DialogFooter>
        <Button size="desk" disabled={busy} onClick={onCancel}>
          ยกเลิก
        </Button>
        <Button variant="primary" size="desk" disabled={busy} onClick={() => void save()}>
          {busy && <Spinner />}
          {busy ? 'กำลังบันทึก…' : `บันทึกการแก้ไข (${changed.length} ช่อง)`}
        </Button>
      </DialogFooter>
    </Modal>
  );
}
