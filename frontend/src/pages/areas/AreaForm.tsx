import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import type { AreaDetail, Building, CleanerOption } from '../../shared/api/adminSetupApi';
import { buildingOptionText } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Field, FieldGroup, Select, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { buttonClass, focusRing } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftPatternLabel, shiftShortLabel } from '../../shared/utils/labels';
import { useCreateAreaMutation, useUpdateAreaMutation } from './areaFormApi';
import { cleanerChoices, draftOf, draftProblem, formBody, type AreaDraft } from './areaFormRules';

interface AreaFormProps {
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

const SHIFT_PATTERNS = ['DayAndNight', 'DayOnly'] as const;

// D10's own card. The Drawer presents the same fields without a box inside a box.
const cardClass = 'flex flex-col gap-4 rounded-lg border border-border bg-surface px-4 py-4 sm:px-6 sm:py-5';
const bareClass = 'flex flex-col gap-4';

/** Wireframe D10's form. A save opens the Area on the D4 page. The page keys it by Area id. */
export function AreaForm({ detail, buildings, cleaners, onSaved, onCancel, framed = true }: AreaFormProps) {
  const navigate = useNavigate();
  const isNew = !detail;
  const areaId = detail?.area.id;
  const [draft, setDraft] = useState<AreaDraft>(() => draftOf(detail));
  const [problem, setProblem] = useState<string | null>(null);
  const [createArea, created] = useCreateAreaMutation();
  const [updateArea, updated] = useUpdateAreaMutation();
  const sending = created.isLoading || updated.isLoading;
  const set = (patch: Partial<AreaDraft>) => setDraft((current) => ({ ...current, ...patch }));
  const areaIsOff = detail !== undefined && !detail.area.isActive;
  const losesNightRounds = detail?.area.shiftPattern === 'DayAndNight' && draft.shiftPattern === 'DayOnly';

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const found = draftProblem(draft, isNew);
    setProblem(found);
    if (found) {
      return;
    }
    try {
      const saved =
        areaId === undefined
          ? await createArea({ code: draft.code.trim(), ...formBody(draft) }).unwrap()
          : await updateArea({ areaId, body: formBody(draft) }).unwrap();
      if (onSaved) {
        onSaved(saved);
      } else {
        navigate(`/admin/areas/${saved.area.id}`);
      }
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className={framed ? cardClass : bareClass} onSubmit={(event) => void submit(event)}>
      <h2 className="text-base font-bold">ข้อมูลพื้นที่</h2>
      {problem && <Note tone="error">{problem}</Note>}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
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
            <span className="font-mono">{detail.area.code}</span>
            <span className="text-xs text-text-2">แก้ไม่ได้ พิมพ์อยู่บนทุกป้าย</span>
          </div>
        )}
        <Field label="ชื่อพื้นที่">
          <TextInput
            size="desk"
            value={draft.name}
            maxLength={150}
            disabled={sending}
            onChange={(event) => set({ name: event.target.value })}
          />
        </Field>
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
        <FieldGroup legend="รูปแบบกะ" disabled={sending}>
          <div className="flex flex-wrap gap-2">
            {SHIFT_PATTERNS.map((pattern) => (
              <label
                key={pattern}
                className="flex min-h-11 cursor-pointer items-center gap-2 rounded-md border border-border-control px-3 has-checked:border-accent has-checked:bg-accent-subtle has-checked:font-semibold has-focus-visible:outline-3 has-focus-visible:outline-offset-2 has-focus-visible:outline-accent"
              >
                <input
                  type="radio"
                  className={`size-4 accent-accent ${focusRing}`}
                  name="areaform-pattern"
                  checked={draft.shiftPattern === pattern}
                  onChange={() => set({ shiftPattern: pattern })}
                />
                {shiftPatternLabel[pattern]}
              </label>
            ))}
          </div>
        </FieldGroup>
      </div>
      <CleanerSelect
        shift="Day"
        value={draft.dayCleanerId}
        choices={cleanerChoices(cleaners, 'Day', areaId, areaIsOff)}
        disabled={sending}
        onChange={(dayCleanerId) => set({ dayCleanerId })}
      />
      {draft.shiftPattern === 'DayAndNight' && (
        <CleanerSelect
          shift="Night"
          value={draft.nightCleanerId}
          choices={cleanerChoices(cleaners, 'Night', areaId, areaIsOff)}
          disabled={sending}
          onChange={(nightCleanerId) => set({ nightCleanerId })}
        />
      )}
      <p className="text-text-2">
        พื้นที่ละ 1 คนต่อกะ · รายชื่อแสดงเฉพาะแม่บ้านที่ยังไม่มีพื้นที่ในกะนั้น · เปลี่ยนเป็น "เฉพาะกะเช้า"
        แล้วช่องกะดึกและเวลารอบกะดึกหายไป
      </p>
      {areaIsOff && <Note tone="warn">พื้นที่นี้ปิดใช้งานอยู่ เปิดใช้งานก่อน แล้วค่อยใส่แม่บ้าน</Note>}
      {losesNightRounds && (
        <Note tone="warn">บันทึกแล้วเวลารอบกะดึกของทุกจุดในพื้นที่นี้จะถูกลบ และแม่บ้านกะดึกออกจากพื้นที่</Note>
      )}
      <div className="flex flex-wrap justify-end gap-2 border-t border-border pt-4">
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
          {sending ? 'กำลังบันทึก...' : 'บันทึก'}
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
