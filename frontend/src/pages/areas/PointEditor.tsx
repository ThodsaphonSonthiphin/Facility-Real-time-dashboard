import { Plus, Trash2 } from 'lucide-react';
import { ConfirmationDialog } from '../../shared/components/ConfirmationDialog';
import type { AdminPoint, AreaSummary } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Field, FieldGroup, TextInput } from '../../shared/components/Field';
import { DialogHeader } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { inputClass } from '../../shared/components/styles';
import { typedTime, type WindowDraft } from './pointRules';
import { SignPanel } from './SignPanel';
import { usePointEditor } from './usePointEditor';
import { timeKeyDown, useShiftWindows } from './useShiftWindows';



export interface PointEditorProps {
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
  const { confirmation, requestDeactivate, name, setName, windows, setWindows, problem, sending, shifts, titleId, save, toggle } = usePointEditor({ area, point, onSaved, onClose });

  return (
    <section className="flex h-full min-h-0 flex-col" aria-labelledby={titleId}>
      <DialogHeader
        id={titleId}
        title={point ? `แก้ไขจุด · ${point.sign.code}` : 'เพิ่มจุด'}
        detail={point ? `${point.name} · ${area.code} ${area.name}` : `${area.code} ${area.name}`}
        onClose={onClose}
        closeDisabled={sending}
      />
      <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void save(event)}>
        <div className="-mx-6 flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto px-6 pb-5">
        <Field label="ชื่อจุด" help="เปลี่ยนชื่อแล้วต้องพิมพ์ป้ายใหม่ เพราะชื่อพิมพ์อยู่บนป้าย">
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
              onAdd={(draft) => setWindows((all) => [...all, draft])}
              onEdit={(index, patch) => setWindows((all) => all.map((w, i) => i === index ? { ...w, ...patch } : w))}
              onRemove={(index) => setWindows((all) => all.filter((_, i) => i !== index))}
            />
          ))}
          <p className="text-text-2">
            ช่วงห้ามซ้อนกันและต้องอยู่ในกะ · ส่งหลังช่วงจบนับเป็นส่งช้า · พื้นที่ที่ทำเฉพาะกะเช้าจะไม่มีช่องกะดึก
          </p>
        </div>
      {point && (
        <SignPanel
          key={point.sign.id}
          areaId={area.id}
          sign={point.sign}
          label={point.name}
          canIssueQr={area.isActive && point.isActive}
        />
      )}
        <Note tone="info">{point ? 'บันทึกชื่อและรอบเวลาของจุดแยกจากรัศมีป้าย · หากเปลี่ยนชื่อ ให้พิมพ์ป้ายใหม่' : 'บันทึกแล้วระบบสร้าง QR ให้ ไปพิมพ์ป้ายได้ที่หน้าพิมพ์ป้าย แล้วเก็บพิกัดด้วยมือถือที่ป้าย'}</Note>
        {problem && <Note tone="error">{problem}</Note>}
      {point && (
        <div className="flex flex-col items-start gap-2 rounded-md border border-border p-3">
          <h4 className="font-semibold">การใช้งานจุด</h4>
          {point.isActive ? (
            <>
              <p className="text-text-2">ปิดใช้งานแล้วจุดและป้ายหายจากหน้าสถานะจุดและสแกนไม่ได้ ประวัติยังอยู่ เปิดกลับได้</p>
              <Button size="desk" disabled={sending} onClick={requestDeactivate}>
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
        </div>
        <div className="-mx-6 -mb-5 flex shrink-0 flex-wrap justify-end gap-2 border-t border-border bg-surface px-6 py-3">
          <Button size="desk" disabled={sending} onClick={onClose}>
            ยกเลิก
          </Button>
          <Button type="submit" variant="primary" size="desk" disabled={sending}>
            {sending ? 'กำลังบันทึก...' : 'บันทึกจุด'}
          </Button>
        </div>
      </form>
      {point && confirmation.question && <ConfirmationDialog
          kind="point"
          title={`ปิดใช้งานจุด ${point.sign.code} ${point.name}?`}
          body="จุดและป้ายหายจากหน้าสถานะจุดและสแกนไม่ได้"
          impacts={['ประวัติยังอยู่ เปิดกลับได้']}
          confirmLabel="ปิดใช้งานจุด"
          onCancel={confirmation.cancel}
          onConfirm={confirmation.confirm}
        />}


    </section>
  );
}

interface ShiftWindowsProps {
  shift: Shift;
  windows: WindowDraft[];
  disabled: boolean;
  onAdd: (draft: WindowDraft) => void;
  onRemove: (index: number) => void;
  onEdit: (index: number, patch: Partial<WindowDraft>) => void;
}

/** Editable round rows; adding creates an empty draft that must be completed before saving. */
function ShiftWindows({ shift, windows, disabled, onAdd, onRemove, onEdit }: ShiftWindowsProps) {
  const { label, mine, timeline, ticks } = useShiftWindows({ shift, windows });

  return (
    <FieldGroup legend={<>{label} <span aria-hidden="true" className="text-sm font-normal text-text-2">{shift === 'Day' ? '07:00–19:00' : '19:00–07:00'}</span></>} disabled={disabled}>
      <div aria-hidden="true" className="flex flex-col gap-1">
        <div className="relative h-6 overflow-hidden rounded-sm border border-border bg-idle-bg">
          {timeline.map((block, index) => <span key={index} style={block} className="absolute inset-y-1 rounded-sm bg-idle" />)}
        </div>
        <div className="flex justify-between text-xs tabular-nums text-text-2">{ticks.map((tick) => <span key={tick}>{tick}</span>)}</div>
      </div>
      <div className="flex flex-col gap-3" aria-label={`ช่วงรอบ${label}`}>
        {mine.length === 0 && <p className="text-text-2">ยังไม่มีช่วงรอบ · จุดจะแสดงนอกเวลาในกะนี้</p>}
        {mine.map(({ window: w, index, duration }, n) => (
          <div key={index} role="group" aria-label={`${label} รอบ ${n + 1}`} className="flex flex-wrap items-center gap-2">
            <span className="inline-flex min-h-9 w-13 shrink-0 items-center text-xs text-text-2">รอบ {n + 1}<span className="sr-only">{`${w.start}–${w.end}`}</span></span>
            <TimeField label={`เริ่มรอบ ${n + 1}`} displayLabel="" hint="07:00" value={w.start.replace(/:/g, '')} disabled={disabled} onChange={(start) => onEdit(index, { start: typedTime(start) })} onEnter={() => {}} />
            <TimeField label={`สิ้นสุดรอบ ${n + 1}`} displayLabel="ถึง" hint="09:00" value={w.end.replace(/:/g, '')} disabled={disabled} onChange={(end) => onEdit(index, { end: typedTime(end) })} onEnter={() => {}} />
            <span className="inline-flex min-h-9 items-center whitespace-nowrap text-xs text-text-2">{duration}</span>
            <Button variant="link" size="desk" className="ml-auto" disabled={disabled} aria-label={`ลบรอบ ${n + 1} ${label}`} onClick={() => onRemove(index)}><Trash2 aria-hidden="true" className="size-4" />ลบ</Button>
          </div>
        ))}
      </div>
      <Button variant="add" size="desk" className="self-start" disabled={disabled} onClick={() => onAdd({ shift, start: '', end: '' })}>
        <Plus aria-hidden="true" className="size-4" />เพิ่มรอบ
      </Button>
    </FieldGroup>
  );
}

interface TimeFieldProps {
  /** The label beside the field, e.g. "เริ่ม". */
  label: string;
  displayLabel?: string;
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
function TimeField({ label, displayLabel, hint, value, disabled, onChange, onEnter }: TimeFieldProps) {
  return (
    <div className="inline-flex items-center gap-1.5">
      {displayLabel !== '' && <span>{displayLabel ?? label}</span>}
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
        aria-label={label}
        onKeyDown={(event) => timeKeyDown(event, value, onChange, onEnter)}
      />
    </div>
  );
}
