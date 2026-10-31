import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import type { AreaFormProps } from './AreaForm';
import { useCreateAreaMutation, useUpdateAreaMutation } from './areaFormApi';
import { draftOf, draftProblem, formBody, type AreaDraft } from './areaFormRules';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
export function useAreaForm({ detail, onSaved }: AreaFormProps) {
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
  const renamed = detail !== undefined && draft.name.trim() !== detail.area.name;
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
      if (renamed) {
        onSaved?.(saved);
        navigate(`/admin/print?area=${saved.area.id}`);
      } else if (onSaved) {
        onSaved(saved);
      } else {
        navigate(`/admin/areas/${saved.area.id}`);
      }
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

 return { renamed, isNew, areaId, draft, set, problem, sending, areaIsOff, losesNightRounds, submit };
}
