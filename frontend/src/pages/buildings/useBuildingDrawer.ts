import { useState, type FormEvent } from 'react';
import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { draftProblem, stillInside } from './buildingRules';
import { useCreateBuildingMutation, useRenameBuildingMutation, useSetBuildingActiveMutation } from './buildingsApi';
import { useConfirmationAction } from '../../shared/components/useConfirmationDialog';
export function useBuildingDrawer(building: ManagedBuilding | undefined, onDone: (message: string) => void) {
  const [code, setCode] = useState('');
  const [name, setName] = useState(building?.name ?? '');
  const [problem, setProblem] = useState<string | null>(null);
  const [switchProblem, setSwitchProblem] = useState<string | null>(null);
  const [createBuilding, created] = useCreateBuildingMutation();
  const [renameBuilding, renamed] = useRenameBuildingMutation();
  const [setBuildingActive, switching] = useSetBuildingActiveMutation();
  const sending = created.isLoading || renamed.isLoading || switching.isLoading;
  const left = building?.isActive ? stillInside(building) : null;
  const titleId = 'building-editor-title';
  const confirmation = useConfirmationAction();

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const found = draftProblem(code, name, !building);
    setProblem(found);
    if (found) {
      return;
    }
    try {
      if (building) {
        const saved = await renameBuilding({ buildingId: building.id, name: name.trim() }).unwrap();
        onDone(`บันทึกตึก ${saved.code} ${saved.name} แล้ว`);
      } else {
        const saved = await createBuilding({ code: code.trim(), name: name.trim() }).unwrap();
        onDone(`เพิ่มตึก ${saved.code} ${saved.name} แล้ว`);
      }
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  const toggle = async (current: ManagedBuilding) => {
    setSwitchProblem(null);
    try {
      const saved = await setBuildingActive({ buildingId: current.id, active: !current.isActive }).unwrap();
      onDone(`${saved.isActive ? 'เปิด' : 'ปิด'}ใช้งานตึก ${saved.code} แล้ว`);
    } catch (error) {
      if (current.isActive) throw error;
      setSwitchProblem(getErrorMessage(error));
    }
  };

  const requestToggle = () => { if (!building) return; if (building.isActive) confirmation.ask(() => toggle(building)); else void toggle(building); };
  return { code, setCode, name, setName, problem, switchProblem, sending, left, titleId, submit, confirmation, requestToggle };
}
