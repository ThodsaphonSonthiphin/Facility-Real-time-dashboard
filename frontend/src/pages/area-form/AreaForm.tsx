import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import type { AreaDetail, Building, CleanerOption } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftPatternLabel, shiftShortLabel } from '../../shared/utils/labels';
import { useCreateAreaMutation, useUpdateAreaMutation } from './areaFormApi';
import { cleanerChoices, draftOf, draftProblem, formBody, type AreaDraft } from './areaFormRules';

interface AreaFormProps {
  /** Missing: a new Area. */
  detail?: AreaDetail;
  buildings: Building[];
  cleaners: CleanerOption[];
}

/** Wireframe D10's form. A save opens the Area on the D4 page. The page keys it by Area id. */
export function AreaForm({ detail, buildings, cleaners }: AreaFormProps) {
  const navigate = useNavigate();
  const isNew = !detail;
  const areaId = detail?.area.id;
  const [draft, setDraft] = useState<AreaDraft>(() => draftOf(detail));
  const [problem, setProblem] = useState<string | null>(null);
  const [createArea, created] = useCreateAreaMutation();
  const [updateArea, updated] = useUpdateAreaMutation();
  const sending = created.isLoading || updated.isLoading;
  const set = (patch: Partial<AreaDraft>) => setDraft((current) => ({ ...current, ...patch }));
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
      navigate(`/admin/areas/${saved.area.id}`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className="areaform-form app-panel" onSubmit={(event) => void submit(event)}>
      {isNew ? (
        <div className="app-field">
          <label htmlFor="areaform-code">รหัสพื้นที่</label>
          <input
            id="areaform-code"
            className="app-input"
            value={draft.code}
            maxLength={20}
            disabled={sending}
            onChange={(event) => set({ code: event.target.value })}
          />
          {/* Outside the label, so the field's name stays "รหัสพื้นที่" */}
          <span className="app-field__help">ตัวอักษรอังกฤษหรือตัวเลข 2–20 ตัว เช่น AR03 · พิมพ์บนทุกป้ายของพื้นที่ แก้ทีหลังไม่ได้</span>
        </div>
      ) : (
        <p className="areaform-fixed">{`รหัสพื้นที่ ${detail.area.code} (แก้ไม่ได้ พิมพ์อยู่บนทุกป้าย)`}</p>
      )}
      <label className="app-field">
        ชื่อพื้นที่
        <input
          className="app-input"
          value={draft.name}
          maxLength={150}
          disabled={sending}
          onChange={(event) => set({ name: event.target.value })}
        />
      </label>
      <div className="app-field">
        <label htmlFor="areaform-building">ตึก</label>
        <select
          id="areaform-building"
          className="app-input"
          value={draft.buildingId}
          disabled={sending}
          onChange={(event) => set({ buildingId: event.target.value })}
        >
          <option value="">เลือกตึก</option>
          {buildings.map((building) => (
            <option key={building.id} value={String(building.id)}>
              {building.name}
            </option>
          ))}
        </select>
      </div>
      <fieldset className="areaform-choice" disabled={sending}>
        <legend>รูปแบบกะ</legend>
        {(['DayAndNight', 'DayOnly'] as const).map((pattern) => (
          <label key={pattern}>
            <input
              type="radio"
              name="areaform-pattern"
              checked={draft.shiftPattern === pattern}
              onChange={() => set({ shiftPattern: pattern })}
            />{' '}
            {shiftPatternLabel[pattern]}
          </label>
        ))}
      </fieldset>
      <CleanerSelect
        shift="Day"
        value={draft.dayCleanerId}
        choices={cleanerChoices(cleaners, 'Day', areaId)}
        disabled={sending}
        onChange={(dayCleanerId) => set({ dayCleanerId })}
      />
      {draft.shiftPattern === 'DayAndNight' && (
        <CleanerSelect
          shift="Night"
          value={draft.nightCleanerId}
          choices={cleanerChoices(cleaners, 'Night', areaId)}
          disabled={sending}
          onChange={(nightCleanerId) => set({ nightCleanerId })}
        />
      )}
      <p className="app-field__help">
        พื้นที่ละ 1 คนต่อกะ · รายชื่อแสดงเฉพาะแม่บ้านที่ยังไม่มีพื้นที่ในกะนั้น · เปลี่ยนเป็น "เฉพาะกะเช้า"
        แล้วช่องกะดึกและเวลารอบกะดึกหายไป
      </p>
      {losesNightRounds && (
        <p className="app-note app-note--warn">บันทึกแล้วเวลารอบกะดึกของทุกจุดในพื้นที่นี้จะถูกลบ และแม่บ้านกะดึกออกจากพื้นที่</p>
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="areaform-buttons">
        <button type="submit" className="app-button app-button--primary" disabled={sending}>
          {sending ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
        <Link className="app-button" to={areaId === undefined ? '/admin/areas' : `/admin/areas/${areaId}`}>
          ยกเลิก
        </Link>
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
  const id = `areaform-cleaner-${shift}`;
  return (
    <div className="app-field">
      <label htmlFor={id}>{`แม่บ้านประจำ${shiftShortLabel[shift]}`}</label>
      <select
        id={id}
        className="app-input"
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">— ยังไม่มี —</option>
        {choices.map((cleaner) => (
          <option key={cleaner.id} value={String(cleaner.id)}>
            {`${cleaner.displayName} (${cleaner.employeeId})`}
          </option>
        ))}
      </select>
    </div>
  );
}
