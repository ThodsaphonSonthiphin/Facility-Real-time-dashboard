import { useState, type FormEvent } from 'react';
import { baseApi } from '../../shared/api/baseApi';
import { useBuildingNames } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useAppSelector } from '../../store/hooks';
import {
  areaMatchesBuilding,
  areasForCleaner,
  cleanerShiftBlock,
  editDraftOf,
  supervisorShiftBlock,
  updateRequest,
  type EditDraft
} from './accountRules';
import { useUpdateAccountMutation } from './accountsApi';

import type { EditAccountFormProps } from './accountFormTypes';

export function useEditAccountForm({ account, accounts, areas, buildings, onDone }: EditAccountFormProps) {
  const names = useBuildingNames();
  const [draft, setDraft] = useState<EditDraft>(() => editDraftOf(account, areas));
  const [problem, setProblem] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [updateAccount, { isLoading: profileLoading }] = useUpdateAccountMutation();
  const me = useAppSelector((state) => state.auth.currentUser?.id);
  const isLoading = useAppSelector((state) =>
    Object.values(state[baseApi.reducerPath].mutations).some(
      (m) => m?.status === 'pending' && ['updateAccount', 'changePhone', 'resetPassword'].includes(m.endpointName ?? ''),
    ),
  );
  const set = (patch: Partial<EditDraft>) => setDraft((current) => ({ ...current, ...patch }));
  // The current Area and Building stay on offer even if deactivated since (plan 5 final-review F3)
  const areaChoices = areas.filter((area) => area.isActive || area.id === account.areaId);
  const buildingChoices = buildings.filter(
    (building) => building.isActive !== false || building.id === account.buildingId || building.id === account.homeBuildingId,
  );
  const visibleAreaChoices = account.role === 'Cleaner'
    ? areasForCleaner(areaChoices, draft.cleanerKind, draft.homeBuildingId)
    : areaChoices;
  const area = areaChoices.find((option) => String(option.id) === draft.areaId);
  const buildingId = draft.buildingId ? Number(draft.buildingId) : undefined;
  const blockOf = (shift: Shift) =>
    account.role === 'Cleaner'
      ? cleanerShiftBlock(area, shift, account.id)
      : supervisorShiftBlock(accounts, buildingId, shift, account.id);
  const titleId = `accounts-edit-${account.id}`;

  const chooseCleanerKind = (kind: 'Regular' | 'Relief') => {
    setDraft((current) => ({ ...current, cleanerKind: kind, areaId: '', homeBuildingId: '' }));
  };
  const chooseHomeBuilding = (value: string) => {
    setDraft((current) => ({
      ...current,
      homeBuildingId: value,
      areaId: current.cleanerKind !== 'Relief' && !areaMatchesBuilding(areaChoices, current.areaId, value) ? '' : current.areaId,
    }));
  };
  const chooseArea = (areaId: string) => {
    const next = areaChoices.find((option) => String(option.id) === areaId);
    setDraft((current) => ({
      ...current,
      areaId,
      homeBuildingId: next ? String(next.buildingId) : current.homeBuildingId,
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

  return {
    names, draft, set, problem, notice, setNotice, me, profileLoading, isLoading,
    buildingChoices, visibleAreaChoices, blockOf, titleId, chooseCleanerKind, chooseHomeBuilding, chooseArea, chooseBuilding, submit,
  };
}
