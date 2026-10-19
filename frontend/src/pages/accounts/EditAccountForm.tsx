import { useState, type FormEvent } from 'react';
import type { AreaSummary, Building } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
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
    <form className="accounts-form app-panel" aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <h2 id={titleId}>{`แก้ไขบัญชี · ${account.displayName}`}</h2>
      <p className="app-muted">{`${accountRoleLabel[account.role]} · ${account.loginName} (บทบาทและรหัสแก้ไม่ได้)`}</p>
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
      {account.role === 'Cleaner' && (
        <div className="app-field">
          <label htmlFor={`${titleId}-area`}>Area ประจำ</label>
          <select
            id={`${titleId}-area`}
            className="app-input"
            value={draft.areaId}
            disabled={isLoading || !account.isActive}
            onChange={(event) => chooseArea(event.target.value)}
          >
            <option value="">— ยังไม่กำหนด Area —</option>
            {areaChoices.map((option) => (
              <option key={option.id} value={String(option.id)}>
                {areaOptionLabel(option, account.id)}
              </option>
            ))}
          </select>
          {!account.isActive && <span className="app-field__help">เปิดใช้งานบัญชีก่อน แล้วค่อยเลือก Area</span>}
        </div>
      )}
      {account.role === 'Supervisor' && (
        <div className="app-field">
          <label htmlFor={`${titleId}-building`}>ตึกที่ประจำ</label>
          <select
            id={`${titleId}-building`}
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
      {account.role !== 'Admin' && (
        <ShiftChoice
          name={`${titleId}-shift`}
          value={draft.shift}
          blockOf={blockOf}
          disabled={isLoading}
          onChange={(shift) => set({ shift })}
        />
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="accounts-buttons">
        <button type="submit" className="app-button app-button--primary" disabled={isLoading}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </form>
  );
}
