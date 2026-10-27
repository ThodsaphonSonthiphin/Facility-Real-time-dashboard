import { useState, type FormEvent } from 'react';
import type { AreaSummary, Building } from '../../shared/api/adminSetupApi';
import { buildingOptionText } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import {
  accountRoleLabel,
  areaOptionLabel,
  cleanerShiftBlock,
  editDraftOf,
  supervisorShiftBlock,
  updateRequest,
  type EditDraft,
} from './accountRules';
import { useUpdateAccountMutation, type Account } from './accountsApi';
import { formClass, formTitleClass } from './accountStyles';
import { ShiftChoice } from './ShiftChoice';

interface EditAccountFormProps {
  account: Account;
  accounts: Account[];
  areas: AreaSummary[];
  buildings: Building[];
  onDone: (text: string) => void;
  onCancel: () => void;
}

/** D6's "แก้ไข": name and place; the role and the login name are fixed (plan 5 decision 2). Keyed by account id. */
export function EditAccountForm({ account, accounts, areas, buildings, onDone, onCancel }: EditAccountFormProps) {
  const [draft, setDraft] = useState<EditDraft>(() => editDraftOf(account));
  const [problem, setProblem] = useState<string | null>(null);
  const [updateAccount, { isLoading }] = useUpdateAccountMutation();
  const set = (patch: Partial<EditDraft>) => setDraft((current) => ({ ...current, ...patch }));
  // The current Area stays on offer even if it was deactivated since (plan 5 final-review F3)
  const areaChoices = areas.filter((area) => area.isActive || area.id === account.areaId);
  const area = areaChoices.find((option) => String(option.id) === draft.areaId);
  const buildingId = draft.buildingId ? Number(draft.buildingId) : undefined;
  const blockOf = (shift: Shift) =>
    account.role === 'Cleaner'
      ? cleanerShiftBlock(area, shift, account.id)
      : supervisorShiftBlock(accounts, buildingId, shift, account.id);
  const titleId = `accounts-edit-${account.id}`;

  const chooseArea = (areaId: string) => {
    const next = areaChoices.find((option) => String(option.id) === areaId);
    setDraft((current) => ({
      ...current,
      areaId,
      shift: current.shift && cleanerShiftBlock(next, current.shift, account.id) ? '' : current.shift,
    }));
  };
  const chooseBuilding = (value: string) => {
    const id = value ? Number(value) : undefined;
    setDraft((current) => ({
      ...current,
      buildingId: value,
      shift: current.shift && supervisorShiftBlock(accounts, id, current.shift, account.id) ? '' : current.shift,
    }));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const built = updateRequest(account, draft);
    if ('problem' in built) {
      setProblem(built.problem);
      return;
    }
    setProblem(null);
    try {
      const saved = await updateAccount({ id: account.id, body: built.body }).unwrap();
      onDone(`บันทึกบัญชี ${saved.displayName} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className={formClass} aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <h2 id={titleId} className={formTitleClass}>{`แก้ไขบัญชี · ${account.displayName}`}</h2>
      <p className="text-muted">{`${accountRoleLabel[account.role]} · ${account.loginName} (บทบาทและรหัสแก้ไม่ได้)`}</p>
      <Field label="ชื่อที่แสดง">
        <TextInput
          size="desk"
          maxLength={150}
          value={draft.displayName}
          disabled={isLoading}
          onChange={(event) => set({ displayName: event.target.value })}
        />
      </Field>
      {account.role === 'Cleaner' && (
        <Field label="พื้นที่ประจำ" help={account.isActive ? undefined : 'เปิดใช้งานบัญชีก่อน แล้วค่อยเลือกพื้นที่'}>
          <Select
            size="desk"
            value={draft.areaId}
            disabled={isLoading || !account.isActive}
            onChange={(event) => chooseArea(event.target.value)}
          >
            <option value="">— ยังไม่กำหนดพื้นที่ —</option>
            {areaChoices.map((option) => (
              <option key={option.id} value={String(option.id)}>
                {areaOptionLabel(option, account.id)}
              </option>
            ))}
          </Select>
        </Field>
      )}
      {account.role === 'Supervisor' && (
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
                {buildingOptionText(building)}
              </option>
            ))}
          </Select>
        </Field>
      )}
      {account.role !== 'Admin' && (
        <ShiftChoice
          name={`${titleId}-shift`}
          value={draft.shift}
          blockOf={blockOf}
          disabled={isLoading}
          onChange={(shift) => set({ shift })}
        />
      )}
      {problem && <Note tone="error">{problem}</Note>}
      <div className="flex flex-wrap gap-2">
        <Button type="submit" variant="primary" size="desk" disabled={isLoading}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึก'}
        </Button>
        <Button size="desk" onClick={onCancel}>
          ยกเลิก
        </Button>
      </div>
    </form>
  );
}
