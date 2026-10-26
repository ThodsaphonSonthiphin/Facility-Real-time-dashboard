import { X } from 'lucide-react';
import { useState, type FormEvent, type KeyboardEvent } from 'react';
import type { AdminPoint, AreaSummary } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Field, FieldGroup, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { focusRing, inputClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { useCreatePointMutation, useSetPointActiveMutation, useUpdatePointMutation } from './areasApi';
import { clockOf, pointEditorTitleId, sameWindow, sortWindows, typedTime, type WindowDraft } from './pointRules';
import { SignPanel } from './SignPanel';

/** What is typed in one shift's two time fields but not yet added (facility-0083): up to four digits, e.g. "0700". */
interface Typed {
  start: string;
  end: string;
}

const noTyped: Typed = { start: '', end: '' };

// The pill's ✕: only the cross, and exactly the 24 px it looks like (`border-0 bg-transparent` repeat preflight on
// purpose, as the Toast's ✕ does), and there is no `::after` layer growing a bigger target: anything past the cross
// must not delete. A bigger invisible target reached over the window's own words, so a click beside the time deleted
// it (owner, 2026-10-23). The pill is the target's room.
const removeClass =
  `inline-flex size-6 shrink-0 cursor-pointer items-center justify-center rounded-full border-0 bg-transparent ` +
  `text-info-text hover:text-primary-strong ${focusRing}`;

interface PointEditorProps {
  area: AreaSummary;
  /** Missing: a new point. */
  point?: AdminPoint;
  /** After a save or an (de)activation, what to tell the Admin; the page closes the editor and floats it. */
  onSaved: (message: string) => void;
  onClose: () => void;
}

/**
 * Wireframe D4's point editor (facility-0045, 0047), in a Drawer: the point's name and its Round Windows per shift.
 * The whole list is sent on every save; the server keeps unchanged windows and checks overlap and shift bounds.
 */
export function PointEditor({ area, point, onSaved, onClose }: PointEditorProps) {
  const [name, setName] = useState(point?.name ?? '');
  const [windows, setWindows] = useState<WindowDraft[]>(() =>
    (point?.roundWindows ?? []).map(({ shift, start, end }) => ({ shift, start, end })),
  );
  // What is typed in the fields but not yet added, per shift: a save must not drop it silently
  const [pending, setPending] = useState<Record<Shift, Typed>>({ Day: noTyped, Night: noTyped });
  const [problem, setProblem] = useState<string | null>(null);
  const [createPoint, created] = useCreatePointMutation();
  const [updatePoint, updated] = useUpdatePointMutation();
  const [setPointActive, toggled] = useSetPointActiveMutation();
  const sending = created.isLoading || updated.isLoading || toggled.isLoading;
  const shifts: Shift[] = area.shiftPattern === 'DayOnly' ? ['Day'] : ['Day', 'Night'];
  const titleId = pointEditorTitleId(point?.id);

  const save = async (event: FormEvent) => {
    event.preventDefault();
    const trimmed = name.trim();
    if (trimmed === '') {
      setProblem('ใส่ชื่อจุดก่อน');
      return;
    }
    if (shifts.some((shift) => Object.values(pending[shift]).some((box) => box.trim() !== ''))) {
      setProblem('มีเวลาที่พิมพ์ไว้แต่ยังไม่ได้เพิ่ม กด "+ เพิ่มช่วงเวลา" หรือลบเวลานั้นก่อนบันทึก');
      return;
    }
    setProblem(null);
    // A day-only Area shows no night windows, so none are sent
    const body = { name: trimmed, roundWindows: sortWindows(windows.filter((w) => shifts.includes(w.shift))) };
    try {
      const saved = point
        ? await updatePoint({ pointId: point.id, body }).unwrap()
        : await createPoint({ areaId: area.id, body }).unwrap();
      onSaved(`บันทึกจุด ${saved.name} แล้ว`);
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
      onSaved(`${active ? 'เปิด' : 'ปิด'}ใช้งานจุด ${saved.name} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <section className="flex flex-col gap-3" aria-labelledby={titleId}>
      <div className="flex items-start justify-between gap-2">
        <h3 id={titleId} tabIndex={-1} className="text-lg font-semibold">
          {`${point ? 'แก้ไขจุด' : 'เพิ่มจุด'} · ${area.code} ${area.name}`}
        </h3>
        <Button size="desk" onClick={onClose}>
          ปิด
        </Button>
      </div>
      <form className="flex flex-col gap-3" onSubmit={(event) => void save(event)}>
        <Field label="ชื่อจุด">
          <TextInput
            size="desk"
            value={name}
            maxLength={150}
            disabled={sending}
            onChange={(event) => setName(event.target.value)}
          />
        </Field>
        <div className="flex flex-col gap-2.5">
          <span className="font-semibold">ช่วงเวลารอบทำความสะอาด</span>
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
          <p className="text-muted">
            ช่วงห้ามซ้อนกันและต้องอยู่ในกะ · ส่งหลังช่วงจบนับเป็นส่งช้า · พื้นที่ที่ทำเฉพาะกะเช้าจะไม่มีช่องกะดึก
          </p>
        </div>
        {problem && <Note tone="error">{problem}</Note>}
        <div>
          <Button type="submit" variant="primary" size="desk" disabled={sending}>
            {sending ? 'กำลังบันทึก...' : 'บันทึกจุด'}
          </Button>
        </div>
      </form>
      {point && (
        <div className="flex flex-col items-start gap-2">
          {point.isActive ? (
            <>
              <p className="text-muted">ปิดใช้งานแล้วจุดและป้ายหายจากหน้าสถานะจุดและสแกนไม่ได้ ประวัติยังอยู่ เปิดกลับได้</p>
              <Button size="desk" disabled={sending} onClick={() => void toggle(false)}>
                ปิดใช้งานจุดนี้
              </Button>
            </>
          ) : (
            <>
              <Note tone="warn">จุดนี้ปิดใช้งานอยู่ ป้ายสแกนไม่ได้</Note>
              <Button size="desk" disabled={sending} onClick={() => void toggle(true)}>
                เปิดใช้งานจุดนี้
              </Button>
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

/** One shift's windows (D4: "07:00–09:00 ×", then เริ่ม [07:00] – ถึง [09:00] + เพิ่มช่วงเวลา). Enter adds. */
function ShiftWindows({ shift, windows, disabled, typed, onTyped, onAdd, onRemove }: ShiftWindowsProps) {
  const [problem, setProblem] = useState<string | null>(null);
  const label = shiftShortLabel[shift];
  const mine = sortWindows(windows.filter((w) => w.shift === shift));

  const add = () => {
    const start = clockOf(typed.start);
    const end = clockOf(typed.end);
    if (!start || !end) {
      setProblem('ใส่เวลาเป็น 07:00 (ชั่วโมง 0–23 นาที 00–59)');
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
    <FieldGroup legend={label} disabled={disabled}>
      <ul className="m-0 flex list-none flex-wrap gap-x-3 gap-y-3 p-0" aria-label={`ช่วงรอบ${label}`}>
        {mine.length === 0 && <li className="text-muted">ยังไม่มีช่วงรอบ</li>}
        {mine.map((w) => (
          <li
            key={`${w.start}-${w.end}`}
            className="inline-flex items-center gap-2 rounded-full border border-info-border bg-info-bg py-0.5 pr-0.5 pl-2.5 text-xs"
          >
            <span>{`${w.start}–${w.end}`}</span>
            <button
              type="button"
              className={removeClass}
              aria-label={`ลบช่วง ${w.start}–${w.end}`}
              onClick={() => onRemove(w)}
            >
              <X aria-hidden="true" className="size-3.5" />
            </button>
          </li>
        ))}
      </ul>
      <div className="flex flex-wrap items-center gap-2">
        <TimeField
          label="เริ่ม"
          hint="07:00"
          value={typed.start}
          disabled={disabled}
          onChange={(start) => onTyped({ ...typed, start })}
          onEnter={add}
        />
        <span aria-hidden="true">–</span>
        <TimeField
          label="ถึง"
          hint="09:00"
          value={typed.end}
          disabled={disabled}
          onChange={(end) => onTyped({ ...typed, end })}
          onEnter={add}
        />
        <Button size="desk" disabled={disabled} onClick={add}>
          + เพิ่มช่วงเวลา
        </Button>
      </div>
      {problem && <Note tone="error">{problem}</Note>}
    </FieldGroup>
  );
}

interface TimeFieldProps {
  /** The label beside the field, e.g. "เริ่ม". */
  label: string;
  /** The empty field's example, e.g. "07:00". */
  hint: string;
  /** Up to four digits, e.g. "0700". */
  value: string;
  disabled: boolean;
  onChange: (digits: string) => void;
  onEnter: () => void;
}

/**
 * facility-0083 (amended 2026-10-23): one field per time, the colon added as the four digits are typed, so it reads
 * "07:00" and never shows a locale's AM/PM (a browser time input would). Only digits are kept; Backspace takes the
 * last digit, so a click never has to land on the added colon; Enter adds the window, not the point.
 */
function TimeField({ label, hint, value, disabled, onChange, onEnter }: TimeFieldProps) {
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      onEnter();
      return;
    }
    if (event.key === 'Backspace') {
      event.preventDefault();
      onChange(value.slice(0, -1));
    }
  };

  return (
    <label className="inline-flex items-center gap-1.5">
      <span>{label}</span>
      {/* four digits and the colon; no token names that width */}
      <input
        className={`${inputClass('desk')} w-20 text-center`}
        inputMode="numeric"
        autoComplete="off"
        maxLength={5}
        placeholder={hint}
        value={typedTime(value)}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value.replace(/\D/g, '').slice(0, 4))}
        onKeyDown={onKeyDown}
      />
    </label>
  );
}
