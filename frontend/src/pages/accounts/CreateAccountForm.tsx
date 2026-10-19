import { useState, type FormEvent } from 'react';
import type { AreaSummary, Building } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import {
  accountRoleLabel,
  areaOptionLabel,
  cleanerShiftBlock,
  createRequest,
  emptyDraft,
  supervisorShiftBlock,
  type CreateDraft,
} from './accountRules';
import { useCreateAccountMutation, type Account } from './accountsApi';
import { ShiftChoice } from './ShiftChoice';

interface CreateAccountFormProps {
  accounts: Account[];
  areas: AreaSummary[];
  buildings: Building[];
}

/**
 * Wireframe D6's "เพิ่มบัญชี" (facility-0020, 0054, 0056): the fields of the chosen role only. A phone or password lives
 * in this form's state until it is sent, then it is cleared.
 */
export function CreateAccountForm({ accounts, areas, buildings }: CreateAccountFormProps) {
  const [draft, setDraft] = useState<CreateDraft>(() => emptyDraft());
  const [problem, setProblem] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [createAccount, { isLoading }] = useCreateAccountMutation();
  const set = (patch: Partial<CreateDraft>) => setDraft((current) => ({ ...current, ...patch }));
  const activeAreas = areas.filter((area) => area.isActive);
  const area = activeAreas.find((option) => String(option.id) === draft.areaId);
  const buildingId = draft.buildingId ? Number(draft.buildingId) : undefined;
  const blockOf = (shift: Shift) =>
    draft.role === 'Cleaner' ? cleanerShiftBlock(area, shift) : supervisorShiftBlock(accounts, buildingId, shift);

  // A shift that the new Area or building does not allow is dropped
  const chooseArea = (areaId: string) => {
    const next = activeAreas.find((option) => String(option.id) === areaId);
    setDraft((current) => ({
      ...current,
      areaId,
      shift: current.shift && cleanerShiftBlock(next, current.shift) ? '' : current.shift,
    }));
  };
  const chooseBuilding = (value: string) => {
    const id = value ? Number(value) : undefined;
    setDraft((current) => ({
      ...current,
      buildingId: value,
      shift: current.shift && supervisorShiftBlock(accounts, id, current.shift) ? '' : current.shift,
    }));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setNotice(null);
    const built = createRequest(draft);
    if ('problem' in built) {
      setProblem(built.problem);
      return;
    }
    setProblem(null);
    try {
      const created = await createAccount(built.request).unwrap();
      setDraft(emptyDraft(draft.role));
      setNotice(`สร้างบัญชี ${created.displayName} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className="accounts-form app-panel" aria-labelledby="accounts-create-title" onSubmit={(event) => void submit(event)}>
      <h2 id="accounts-create-title">เพิ่มบัญชี</h2>
      <fieldset className="accounts-choice accounts-choice--row" disabled={isLoading}>
        <legend>บทบาท</legend>
        {(['Cleaner', 'Supervisor', 'Admin'] as const).map((role) => (
          <label key={role}>
            <input
              type="radio"
              name="accounts-create-role"
              checked={draft.role === role}
              onChange={() => setDraft({ ...emptyDraft(role), displayName: draft.displayName })}
            />
            {` ${accountRoleLabel[role]}`}
          </label>
        ))}
      </fieldset>
      <label className="app-field">
        ชื่อที่แสดง
        <input
          className="app-input"
          maxLength={150}
          value={draft.displayName}
          disabled={isLoading}
          onChange={(event) => set({ displayName: event.target.value })}
        />
      </label>
      {draft.role === 'Admin' ? (
        <>
          <label className="app-field">
            ชื่อผู้ใช้
            <input
              className="app-input"
              autoComplete="off"
              maxLength={100}
              value={draft.username}
              disabled={isLoading}
              onChange={(event) => set({ username: event.target.value })}
            />
          </label>
          <label className="app-field">
            รหัสผ่านชั่วคราว
            <input
              type="password"
              className="app-input"
              autoComplete="new-password"
              maxLength={100}
              value={draft.password}
              disabled={isLoading}
              onChange={(event) => set({ password: event.target.value })}
            />
          </label>
          <p className="app-field__help">Admin: ชื่อผู้ใช้ + รหัสผ่านชั่วคราว 8–100 ตัวอักษร (บอกเองด้วยวาจาหรือกระดาษ)</p>
        </>
      ) : (
        <>
          <div className="app-field">
            <label htmlFor="accounts-create-employee">รหัสพนักงาน</label>
            <input
              id="accounts-create-employee"
              className="app-input"
              autoComplete="off"
              maxLength={20}
              value={draft.employeeId}
              disabled={isLoading}
              onChange={(event) => set({ employeeId: event.target.value })}
            />
            <span className="app-field__help">ห้ามซ้ำ แก้ทีหลังไม่ได้</span>
          </div>
          <div className="app-field">
            <label htmlFor="accounts-create-phone">เบอร์โทรศัพท์</label>
            <input
              id="accounts-create-phone"
              type="tel"
              inputMode="numeric"
              className="app-input"
              autoComplete="off"
              maxLength={12}
              value={draft.phone}
              disabled={isLoading}
              onChange={(event) => set({ phone: event.target.value })}
            />
            <span className="app-field__help">
              ใช้แทนรหัสผ่าน ระบบเก็บเบอร์แบบที่เปิดดูไม่ได้ ใช้ตรวจตอน login อย่างเดียว · เปลี่ยนเบอร์ทีหลังได้ด้วยปุ่ม
              "แก้เบอร์โทร"
            </span>
          </div>
          {draft.role === 'Cleaner' ? (
            <div className="app-field">
              <label htmlFor="accounts-create-area">Area ประจำ</label>
              <select
                id="accounts-create-area"
                className="app-input"
                value={draft.areaId}
                disabled={isLoading}
                onChange={(event) => chooseArea(event.target.value)}
              >
                <option value="">— ยังไม่กำหนด Area —</option>
                {activeAreas.map((option) => (
                  <option key={option.id} value={String(option.id)}>
                    {areaOptionLabel(option)}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <div className="app-field">
              <label htmlFor="accounts-create-building">ตึกที่ประจำ</label>
              <select
                id="accounts-create-building"
                className="app-input"
                value={draft.buildingId}
                disabled={isLoading}
                onChange={(event) => chooseBuilding(event.target.value)}
              >
                <option value="">เลือกตึก</option>
                {buildings.map((building) => (
                  <option key={building.id} value={String(building.id)}>
                    {building.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          <ShiftChoice
            name="accounts-create-shift"
            value={draft.shift}
            blockOf={blockOf}
            disabled={isLoading}
            onChange={(shift) => set({ shift })}
          />
          <p className="app-field__help">
            {draft.role === 'Cleaner'
              ? 'Area ละ 1 คนต่อกะ · ถ้ากะนี้มีคนประจำแล้วจะเลือกไม่ได้ · Area ที่ทำเฉพาะกะเช้าเลือกได้แค่กะเช้า · ยังไม่กำหนด Area ได้ เลือกทีหลังที่ปุ่ม "แก้ไข"'
              : 'หัวหน้าประจำตึกและกะ ตึกละ 1 คนต่อกะ · ตรวจได้เฉพาะในกะตัวเอง · ไม่ต้องลงเวลา'}
          </p>
        </>
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      {notice && (
        <p className="app-note app-note--info" role="status">
          {notice}
        </p>
      )}
      <button type="submit" className="app-button app-button--primary" disabled={isLoading}>
        {isLoading ? 'กำลังสร้าง...' : 'สร้างบัญชี'}
      </button>
    </form>
  );
}
