import { useState, type FormEvent } from 'react';
import type { AreaSummary, Building } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Field, FieldGroup, Select, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
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
import { formClass, formTitleClass, radioClass, radioLabelClass } from './accountStyles';
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
    <form className={formClass} aria-labelledby="accounts-create-title" onSubmit={(event) => void submit(event)}>
      <h2 id="accounts-create-title" className={formTitleClass}>
        เพิ่มบัญชี
      </h2>
      <FieldGroup legend="บทบาท" disabled={isLoading}>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
          {(['Cleaner', 'Supervisor', 'Admin'] as const).map((role) => (
            <label key={role} className={radioLabelClass}>
              <input
                type="radio"
                className={radioClass}
                name="accounts-create-role"
                checked={draft.role === role}
                onChange={() =>
                  setDraft({
                    ...emptyDraft(role),
                    displayName: draft.displayName,
                  })
                }
              />
              {accountRoleLabel[role]}
            </label>
          ))}
        </div>
      </FieldGroup>
      <Field label="ชื่อที่แสดง">
        <TextInput
          size="desk"
          maxLength={150}
          value={draft.displayName}
          disabled={isLoading}
          onChange={(event) => set({ displayName: event.target.value })}
        />
      </Field>
      {draft.role === 'Admin' ? (
        <>
          <Field label="ชื่อผู้ใช้">
            <TextInput
              size="desk"
              autoComplete="off"
              maxLength={100}
              value={draft.username}
              disabled={isLoading}
              onChange={(event) => set({ username: event.target.value })}
            />
          </Field>
          <Field label="รหัสผ่านชั่วคราว">
            <TextInput
              size="desk"
              type="password"
              autoComplete="new-password"
              maxLength={100}
              value={draft.password}
              disabled={isLoading}
              onChange={(event) => set({ password: event.target.value })}
            />
          </Field>
          <p className="text-muted">แอดมิน: ชื่อผู้ใช้ + รหัสผ่านชั่วคราว 15–100 ตัวอักษร เป็นวลียาว ๆ ได้ (บอกเองด้วยวาจาหรือกระดาษ)</p>
        </>
      ) : (
        <>
          <Field label="รหัสพนักงาน" help="ห้ามซ้ำ แก้ทีหลังไม่ได้">
            <TextInput
              size="desk"
              autoComplete="off"
              maxLength={20}
              value={draft.employeeId}
              disabled={isLoading}
              onChange={(event) => set({ employeeId: event.target.value })}
            />
          </Field>
          <Field
            label="เบอร์โทรศัพท์"
            help={
              'ใช้แทนรหัสผ่าน ระบบเก็บเบอร์แบบที่เปิดดูไม่ได้ ใช้ตรวจตอน login อย่างเดียว · เปลี่ยนเบอร์ทีหลังได้ด้วยปุ่ม "แก้เบอร์โทร"'
            }
          >
            <TextInput
              size="desk"
              type="tel"
              inputMode="numeric"
              autoComplete="off"
              maxLength={16}
              value={draft.phone}
              disabled={isLoading}
              onChange={(event) => set({ phone: event.target.value })}
            />
          </Field>
          {draft.role === 'Cleaner' ? (
            <Field label="พื้นที่ประจำ">
              <Select
                size="desk"
                value={draft.areaId}
                disabled={isLoading}
                onChange={(event) => chooseArea(event.target.value)}
              >
                <option value="">— ยังไม่กำหนดพื้นที่ —</option>
                {activeAreas.map((option) => (
                  <option key={option.id} value={String(option.id)}>
                    {areaOptionLabel(option)}
                  </option>
                ))}
              </Select>
            </Field>
          ) : (
            <Field label="ตึกที่ประจำ">
              <Select
                size="desk"
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
              </Select>
            </Field>
          )}
          <ShiftChoice
            name="accounts-create-shift"
            value={draft.shift}
            blockOf={blockOf}
            disabled={isLoading}
            onChange={(shift) => set({ shift })}
          />
          <p className="text-muted">
            {draft.role === 'Cleaner'
              ? 'พื้นที่ละ 1 คนต่อกะ · ถ้ากะนี้มีคนประจำแล้วจะเลือกไม่ได้ · พื้นที่ที่ทำเฉพาะกะเช้าเลือกได้แค่กะเช้า · ยังไม่กำหนดพื้นที่ได้ เลือกทีหลังที่ปุ่ม "แก้ไข"'
              : 'หัวหน้าประจำตึกและกะ ตึกละ 1 คนต่อกะ · ตรวจได้เฉพาะในกะตัวเอง · ไม่ต้องลงเวลา'}
          </p>
        </>
      )}
      {problem && <Note tone="error">{problem}</Note>}
      {notice && (
        <Note tone="info" status>
          {notice}
        </Note>
      )}
      <Button type="submit" variant="primary" size="desk" disabled={isLoading}>
        {isLoading ? 'กำลังสร้าง...' : 'สร้างบัญชี'}
      </Button>
    </form>
  );
}
