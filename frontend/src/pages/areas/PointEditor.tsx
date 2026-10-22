import { useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import type { AdminPoint, AreaSummary } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { useCreatePointMutation, useSetPointActiveMutation, useUpdatePointMutation } from './areasApi';
import { clockOf, sameWindow, sortWindows, type WindowDraft } from './pointRules';
import { SignPanel } from './SignPanel';

/** What is typed in one shift's four boxes but not yet added (facility-0083). */
interface Typed {
  startHour: string;
  startMinute: string;
  endHour: string;
  endMinute: string;
}

const noTyped: Typed = { startHour: '', startMinute: '', endHour: '', endMinute: '' };

interface PointEditorProps {
  area: AreaSummary;
  /** Missing: a new point. */
  point?: AdminPoint;
  /** After a save or an (de)activation: the point as the server now has it, and what to tell the Admin. */
  onSaved: (point: AdminPoint, message: string) => void;
  onClose: () => void;
}

/**
 * Wireframe D4's lower panel (facility-0045, 0047): the point's name and its Round Windows per shift. The whole list
 * is sent on every save; the server keeps unchanged windows and checks overlap and shift bounds.
 */
export function PointEditor({ area, point, onSaved, onClose }: PointEditorProps) {
  const [name, setName] = useState(point?.name ?? '');
  const [windows, setWindows] = useState<WindowDraft[]>(() =>
    (point?.roundWindows ?? []).map(({ shift, start, end }) => ({ shift, start, end })),
  );
  // What is typed in the boxes but not yet added, per shift: a save must not drop it silently
  const [pending, setPending] = useState<Record<Shift, Typed>>({ Day: noTyped, Night: noTyped });
  const [problem, setProblem] = useState<string | null>(null);
  const [createPoint, created] = useCreatePointMutation();
  const [updatePoint, updated] = useUpdatePointMutation();
  const [setPointActive, toggled] = useSetPointActiveMutation();
  const sending = created.isLoading || updated.isLoading || toggled.isLoading;
  const shifts: Shift[] = area.shiftPattern === 'DayOnly' ? ['Day'] : ['Day', 'Night'];
  const titleId = point ? `point-editor-${point.id}` : 'point-editor-new';

  const save = async (event: FormEvent) => {
    event.preventDefault();
    const trimmed = name.trim();
    if (trimmed === '') {
      setProblem('ใส่ชื่อจุดก่อน');
      return;
    }
    if (shifts.some((shift) => Object.values(pending[shift]).some((box) => box.trim() !== ''))) {
      setProblem('มีเวลาที่พิมพ์ไว้แต่ยังไม่ได้กด "เพิ่ม" กด "เพิ่ม" หรือลบเวลานั้นก่อนบันทึก');
      return;
    }
    setProblem(null);
    // A day-only Area shows no night windows, so none are sent
    const body = { name: trimmed, roundWindows: sortWindows(windows.filter((w) => shifts.includes(w.shift))) };
    try {
      const saved = point
        ? await updatePoint({ pointId: point.id, body }).unwrap()
        : await createPoint({ areaId: area.id, body }).unwrap();
      onSaved(saved, `บันทึกจุด ${saved.name} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  const toggle = async (active: boolean) => {
    if (!point) {
      return;
    }
    setProblem(null);
    try {
      const saved = await setPointActive({ pointId: point.id, active }).unwrap();
      onSaved(saved, `${active ? 'เปิด' : 'ปิด'}ใช้งานจุด ${saved.name} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <section className="areas-editor app-panel" aria-labelledby={titleId}>
      <h3 id={titleId}>{`${point ? 'แก้ไขจุด' : 'เพิ่มจุด'} · ${area.code} ${area.name}`}</h3>
      <form className="areas-editor__form" onSubmit={(event) => void save(event)}>
        <label className="app-field">
          ชื่อจุด
          <input
            className="app-input"
            value={name}
            maxLength={150}
            disabled={sending}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <div className="areas-editor__shifts">
          <span className="areas-editor__label">ช่วงเวลารอบทำความสะอาด</span>
          {shifts.map((shift) => (
            <ShiftWindows
              key={shift}
              shift={shift}
              windows={windows}
              disabled={sending}
              typed={pending[shift]}
              onTyped={(typed) => setPending((all) => ({ ...all, [shift]: typed }))}
              onAdd={(draft) => setWindows((all) => [...all, draft])}
              onRemove={(draft) => setWindows((all) => all.filter((w) => !sameWindow(w, draft)))}
            />
          ))}
          <p className="app-field__help">
            ช่วงห้ามซ้อนกันและต้องอยู่ในกะ · ส่งหลังช่วงจบนับเป็นส่งช้า · พื้นที่ที่ทำเฉพาะกะเช้าจะไม่มีช่องกะดึก
          </p>
        </div>
        {problem && (
          <p className="app-note app-note--error" role="alert">
            {problem}
          </p>
        )}
        <div className="areas-editor__buttons">
          <button type="submit" className="app-button app-button--primary" disabled={sending}>
            {sending ? 'กำลังบันทึก...' : 'บันทึกจุด'}
          </button>
          <button type="button" className="app-button" onClick={onClose}>
            ปิด
          </button>
        </div>
      </form>
      {point && (
        <div className="areas-editor__active">
          {point.isActive ? (
            <>
              <p className="app-field__help">ปิดใช้งานแล้วจุดและป้ายหายจากหน้าสถานะจุดและสแกนไม่ได้ ประวัติยังอยู่ เปิดกลับได้</p>
              <button type="button" className="app-button" disabled={sending} onClick={() => void toggle(false)}>
                ปิดใช้งานจุดนี้
              </button>
            </>
          ) : (
            <>
              <p className="app-note app-note--warn">จุดนี้ปิดใช้งานอยู่ ป้ายสแกนไม่ได้</p>
              <button type="button" className="app-button" disabled={sending} onClick={() => void toggle(true)}>
                เปิดใช้งานจุดนี้
              </button>
            </>
          )}
        </div>
      )}
      {point && (
        <SignPanel
          key={point.sign.id}
          areaId={area.id}
          sign={point.sign}
          label={point.name}
          canIssueQr={area.isActive && point.isActive}
        />
      )}
    </section>
  );
}

interface ShiftWindowsProps {
  shift: Shift;
  windows: WindowDraft[];
  disabled: boolean;
  typed: Typed;
  onTyped: (typed: Typed) => void;
  onAdd: (draft: WindowDraft) => void;
  onRemove: (draft: WindowDraft) => void;
}

/** One shift's windows (D4: "07:00–09:00 ×", then เริ่ม [ชม.]:[นาที] ถึง [ชม.]:[นาที] เพิ่ม). Enter in a box adds, not saves. */
function ShiftWindows({ shift, windows, disabled, typed, onTyped, onAdd, onRemove }: ShiftWindowsProps) {
  const [problem, setProblem] = useState<string | null>(null);
  const label = shiftShortLabel[shift];
  const mine = sortWindows(windows.filter((w) => w.shift === shift));

  const add = () => {
    const start = clockOf(typed.startHour, typed.startMinute);
    const end = clockOf(typed.endHour, typed.endMinute);
    if (!start || !end) {
      setProblem('ชั่วโมงใส่ 0–23 นาทีใส่ 00–59');
      return;
    }
    const draft = { shift, start, end };
    if (windows.some((w) => sameWindow(w, draft))) {
      setProblem('มีช่วงนี้แล้ว');
      return;
    }
    setProblem(null);
    onAdd(draft);
    onTyped(noTyped);
  };

  return (
    <fieldset className="areas-shift" disabled={disabled}>
      <legend>{label}</legend>
      <ul className="areas-shift__windows" aria-label={`ช่วงรอบ${label}`}>
        {mine.length === 0 && <li className="app-muted">ยังไม่มีช่วงรอบ</li>}
        {mine.map((w) => (
          <li key={`${w.start}-${w.end}`} className="areas-shift__chip">
            <span>{`${w.start}–${w.end}`}</span>
            <button
              type="button"
              className="app-link-button"
              aria-label={`ลบช่วง ${w.start}–${w.end}`}
              onClick={() => onRemove(w)}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
      <div className="areas-shift__add">
        <TimeBoxes
          label="เริ่ม"
          hour={typed.startHour}
          minute={typed.startMinute}
          hint={['07', '00']}
          onChange={(startHour, startMinute) => onTyped({ ...typed, startHour, startMinute })}
          onEnter={add}
        />
        <TimeBoxes
          label="ถึง"
          hour={typed.endHour}
          minute={typed.endMinute}
          hint={['09', '00']}
          onChange={(endHour, endMinute) => onTyped({ ...typed, endHour, endMinute })}
          onEnter={add}
        />
        <button type="button" className="app-button" onClick={add}>
          เพิ่ม
        </button>
      </div>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
    </fieldset>
  );
}

interface TimeBoxesProps {
  label: string;
  hour: string;
  minute: string;
  /** Placeholders for the hour and the minute, e.g. ['07', '00']. */
  hint: [string, string];
  onChange: (hour: string, minute: string) => void;
  onEnter: () => void;
}

/**
 * facility-0083: one time as an hour box and a minute box, never one "HH:mm" string. Only digits are kept, and the
 * second hour digit moves the cursor to the minute box.
 */
function TimeBoxes({ label, hour, minute, hint, onChange, onEnter }: TimeBoxesProps) {
  const minuteBox = useRef<HTMLInputElement>(null);
  const enterAdds = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      onEnter();
    }
  };

  return (
    <span className="areas-time">
      <span className="areas-time__label">{label}</span>
      <input
        className="app-input areas-time__box"
        aria-label={`${label} ชั่วโมง`}
        inputMode="numeric"
        maxLength={2}
        placeholder={hint[0]}
        value={hour}
        onChange={(event) => {
          const digits = event.target.value.replace(/\D/g, '');
          onChange(digits, minute);
          if (digits.length === 2) {
            minuteBox.current?.focus();
          }
        }}
        onKeyDown={enterAdds}
      />
      <span aria-hidden="true">:</span>
      <input
        ref={minuteBox}
        className="app-input areas-time__box"
        aria-label={`${label} นาที`}
        inputMode="numeric"
        maxLength={2}
        placeholder={hint[1]}
        value={minute}
        onChange={(event) => onChange(hour, event.target.value.replace(/\D/g, ''))}
        onKeyDown={enterAdds}
      />
    </span>
  );
}
