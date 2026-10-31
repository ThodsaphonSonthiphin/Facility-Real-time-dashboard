import { LockKeyhole } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { AreaDetail, Building, CleanerOption } from '../../shared/api/adminSetupApi';
import { buildingOptionText } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { buttonClass, focusRing } from '../../shared/components/styles';
import { shiftShortLabel } from '../../shared/utils/labels';
import { cleanerChoices } from './areaFormRules';
import { useAreaForm } from './useAreaForm';

export interface AreaFormProps {
  children?: ReactNode;
  /** Missing: a new Area. */
  detail?: AreaDetail;
  buildings: Building[];
  cleaners: CleanerOption[];
  /** Given: the save hands the saved Area to the caller (D4's Drawer) instead of opening the Area's page. */
  onSaved?: (saved: AreaDetail) => void;
  /** Given: "ยกเลิก" closes the form (D4's Drawer) instead of linking back to the Area's page. */
  onCancel?: () => void;
  /** Its own white box on D10's page; off inside the Drawer, which already is one. */
  framed?: boolean;
}


// D10's own card. The Drawer presents the same fields without a box inside a box.
const cardClass = 'flex flex-col gap-4 rounded-lg border border-border bg-surface px-4 py-4 sm:px-6 sm:py-5';
const bareClass = 'flex min-h-0 flex-1 flex-col';

/** Wireframe D10's form. A save opens the Area on the D4 page. The page keys it by Area id. */
export function AreaForm({ detail, buildings, cleaners, onSaved, onCancel, framed = true, children }: AreaFormProps) {
  const { renamed, isNew, areaId, draft, set, problem, sending, areaIsOff, losesNightRounds, submit } = useAreaForm({ detail, buildings, cleaners, onSaved });

  return (
    <form className={framed ? cardClass : bareClass} onSubmit={(event) => void submit(event)}>
      <div className={framed ? "flex flex-col gap-6" : "-mx-6 flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto px-6 pb-5"}>
      <h2 className="text-base font-bold">ข้อมูลพื้นที่</h2>
      {problem && <Note tone="error">{problem}</Note>}
      <section className="flex flex-col gap-4" aria-label="ข้อมูลพื้นที่">
        {isNew ? (
          <Field
            label="รหัสพื้นที่"
            help="ตัวอักษรอังกฤษหรือตัวเลข 2–20 ตัว เช่น AR03 · พิมพ์บนทุกป้ายของพื้นที่ แก้ทีหลังไม่ได้"
          >
            <TextInput
              size="desk"
              value={draft.code}
              maxLength={20}
              disabled={sending}
              onChange={(event) => set({ code: event.target.value })}
            />
          </Field>
        ) : (
          <div className="flex flex-col gap-1">
            <span className="font-semibold">รหัสพื้นที่</span>
            <span className="flex flex-wrap items-center gap-1.5 text-xs text-text-2"><LockKeyhole className="size-4" aria-hidden="true" /><b className="font-mono text-sm text-text">{detail!.area.code}</b> แก้ไม่ได้ เพราะพิมพ์อยู่บนป้ายแล้ว</span>
          </div>
        )}
        <Field label="ชื่อพื้นที่" help={detail ? `เดิม: ${detail.area.name}` : undefined}>
          <TextInput
            size="desk"
            value={draft.name}
            maxLength={150}
            disabled={sending}
            onChange={(event) => set({ name: event.target.value })}
          />
        </Field>
        {detail && draft.name.trim() !== detail.area.name && (
          <Note tone="warn">ชื่อพื้นที่พิมพ์บนป้ายทั้ง {detail.points.length + 1} ป้าย หลังบันทึกจะไปหน้าพิมพ์ป้ายเพื่อพิมพ์ใหม่ ป้ายเดิมยังสแกนได้จนกว่าจะสร้าง QR ใหม่</Note>
        )}
        <Field label="ตึก">
          <Select
            size="desk"
            value={draft.buildingId}
            disabled={sending}
            onChange={(event) => set({ buildingId: event.target.value })}
          >
            <option value="">เลือกตึก</option>
            {buildings.map((building) => (
              <option key={building.id} value={String(building.id)}>
                {buildingOptionText(building)}
              </option>
            ))}
          </Select>
        </Field>
      </section>
      <section className="flex flex-col gap-4 border-t border-border pt-5" aria-label="กะและแม่บ้านประจำ">
        <h3 className="text-base font-bold">กะและแม่บ้านประจำ</h3>
        <div className="grid grid-cols-1 items-center gap-3 sm:grid-cols-3">
          <label className="flex min-h-9 items-center gap-2 font-semibold"><input type="checkbox" className={`size-4 accent-accent ${focusRing}`} checked disabled />กะเช้า</label>
          <div className="sm:col-span-2"><CleanerSelect shift="Day" value={draft.dayCleanerId} choices={cleanerChoices(cleaners, 'Day', areaId, areaIsOff)} disabled={sending} onChange={(dayCleanerId) => set({ dayCleanerId })} /></div>
          <label className="flex min-h-9 items-center gap-2 font-semibold"><input type="checkbox" className={`size-4 accent-accent ${focusRing}`} checked={draft.shiftPattern === 'DayAndNight'} disabled={sending} onChange={(event) => set({ shiftPattern: event.target.checked ? 'DayAndNight' : 'DayOnly' })} />กะดึก</label>
          <div className="sm:col-span-2">{draft.shiftPattern === 'DayAndNight' ? <CleanerSelect shift="Night" value={draft.nightCleanerId} choices={cleanerChoices(cleaners, 'Night', areaId, areaIsOff)} disabled={sending} onChange={(nightCleanerId) => set({ nightCleanerId })} /> : <p className="text-text-2">เฉพาะกะเช้า</p>}</div>
        </div>
      <p className="text-xs text-text-2">กะเช้าเปิดเสมอ · ปิดกะดึกเป็นเฉพาะกะเช้า · พื้นที่ละ 1 คนต่อกะ เลือกได้เฉพาะแม่บ้านว่าง</p>
      {areaIsOff && <Note tone="warn">พื้นที่นี้ปิดใช้งานอยู่ เปิดใช้งานก่อน แล้วค่อยใส่แม่บ้าน</Note>}
      {losesNightRounds && (
        <Note tone="warn">บันทึกแล้วเวลารอบกะดึกของทุกจุดในพื้นที่นี้จะถูกลบ และแม่บ้านกะดึกออกจากพื้นที่</Note>
      )}
      </section>
      {children}
      </div>
      <div className={framed ? "flex flex-wrap justify-end gap-2 border-t border-border pt-4" : "-mx-6 -mb-5 flex shrink-0 flex-wrap justify-end gap-2 border-t border-border bg-surface px-6 py-3"}>
        {onCancel ? (
          <Button size="desk" disabled={sending} onClick={onCancel}>
            ยกเลิก
          </Button>
        ) : (
          <Link
            className={buttonClass('secondary', 'desk')}
            data-tap=""
            to={areaId === undefined ? '/admin/areas' : `/admin/areas/${areaId}`}
          >
            ยกเลิก
          </Link>
        )}
        <Button type="submit" variant="primary" size="desk" disabled={sending}>
          {sending ? 'กำลังบันทึก...' : renamed ? 'บันทึก แล้วไปพิมพ์ป้าย' : 'บันทึก'}
        </Button>
      </div>
    </form>
  );
}

interface CleanerSelectProps {
  shift: Shift;
  value: string;
  choices: CleanerOption[];
  disabled: boolean;
  onChange: (value: string) => void;
}

/** D10: "แม่บ้านประจำกะเช้า สมศรี (650123)". */
function CleanerSelect({ shift, value, choices, disabled, onChange }: CleanerSelectProps) {
  return (
    <Field label={`แม่บ้านประจำ${shiftShortLabel[shift]}`}>
      <Select size="desk" value={value} disabled={disabled} onChange={(event) => onChange(event.target.value)}>
        <option value="">— ยังไม่มี —</option>
        {choices.map((cleaner) => (
          <option key={cleaner.id} value={String(cleaner.id)}>
            {`${cleaner.displayName} (${cleaner.employeeId})`}
          </option>
        ))}
      </Select>
    </Field>
  );
}
