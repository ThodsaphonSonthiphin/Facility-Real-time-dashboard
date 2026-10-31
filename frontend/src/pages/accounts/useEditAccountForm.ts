import { useState, type FormEvent } from 'react';
import { useBuildingNames } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import {
  cleanerShiftBlock,
  editDraftOf,
  supervisorShiftBlock,
  updateRequest,
  type EditDraft
} from './accountRules';
import { useUpdateAccountMutation } from './accountsApi';

import type { EditAccountFormProps } from './accountFormTypes';

export function useEditAccountForm({ account, accounts, areas, onDone }: EditAccountFormProps) {
  const names = useBuildingNames();
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

  return { names, draft, set, problem, isLoading, areaChoices, blockOf, titleId, chooseArea, chooseBuilding, submit };
}
