import { useState, type FormEvent } from 'react';
import { useBuildingNames } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import {
  cleanerShiftBlock,
  createRequest,
  emptyDraft,
  supervisorShiftBlock,
  type CreateDraft
} from './accountRules';
import { useCreateAccountMutation, type AccountRole } from './accountsApi';

import type { CreateAccountFormProps } from './accountFormTypes';

export function useCreateAccountForm({ accounts, areas }: CreateAccountFormProps) {
  const names = useBuildingNames();
  const [draft, setDraft] = useState<CreateDraft>(() => emptyDraft());
  const [problem, setProblem] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [createAccount, { isLoading }] = useCreateAccountMutation();
  const set = (patch: Partial<CreateDraft>) => setDraft((current) => ({ ...current, ...patch }));
  const chooseRole = (role: AccountRole) => setDraft((current) => ({
    ...emptyDraft(role),
    displayName: current.displayName,
  }));
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

  return { names, draft, chooseRole, set, problem, notice, isLoading, activeAreas, blockOf, chooseArea, chooseBuilding, submit };
}
